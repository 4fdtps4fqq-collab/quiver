[CmdletBinding()]
param(
    [string]$ContainerName,
    [switch]$KeepSystemAdmin = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Test-CommandExists {
    param([string]$Name)

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Invoke-DockerPsql {
    param(
        [string]$TargetContainer,
        [string]$Database,
        [string]$Sql
    )

    $escapedSql = $Sql.Replace('"', '\"')
    $command = "docker exec $TargetContainer psql -v ON_ERROR_STOP=1 -U postgres -d $Database -c ""$escapedSql"""

    if ($DryRun) {
        Write-Host "[dry-run] $command"
        return ""
    }

    return Invoke-Expression $command
}

function Get-DockerContainerScore {
    param([string]$TargetContainer)

    try {
        $result = docker exec $TargetContainer psql -t -A -U postgres -d kiteflow_identity -c "select case when to_regclass('public.user_accounts') is null then '-1|-1' else (select count(*)::text from public.user_accounts) || '|' || (select count(*)::text from public.user_accounts where ""Role"" = 1) end;"
        if ([string]::IsNullOrWhiteSpace($result)) {
            return -1
        }

        $parts = $result.Trim().Split('|')
        if ($parts.Length -ne 2) {
            return -1
        }

        $userCount = [int]$parts[0]
        $systemAdminCount = [int]$parts[1]

        if ($userCount -lt 0) {
            return -1
        }

        return ($userCount * 10) + $systemAdminCount
    }
    catch {
        return -1
    }
}

function Resolve-ContainerName {
    param([string]$RequestedContainer)

    if (-not [string]::IsNullOrWhiteSpace($RequestedContainer)) {
        return $RequestedContainer
    }

    $candidateNames = @("postgres", "kiteflow-postgres")
    $runningContainers = docker ps --format "{{.Names}}"
    $bestName = $null
    $bestScore = -1

    foreach ($candidate in $candidateNames) {
        if ($runningContainers -notcontains $candidate) {
            continue
        }

        $score = Get-DockerContainerScore -TargetContainer $candidate
        if ($score -gt $bestScore) {
            $bestScore = $score
            $bestName = $candidate
        }
    }

    if ($null -eq $bestName) {
        throw "Nao foi possivel localizar um container PostgreSQL ativo com os bancos do projeto. Informe -ContainerName explicitamente."
    }

    return $bestName
}

if (-not (Test-CommandExists -Name "docker")) {
    throw "Docker CLI nao encontrado no PATH."
}

$resolvedContainer = Resolve-ContainerName -RequestedContainer $ContainerName

Write-Host "Container alvo: $resolvedContainer"
Write-Host "Preservar SystemAdmin: $KeepSystemAdmin"
Write-Host "Modo dry-run: $DryRun"

$cleanupStatements = @(
    @{
        Database = "kiteflow_schools"
        Description = "Schools"
        Sql = @"
TRUNCATE TABLE public.school_settings, public.user_profiles, public.schools RESTART IDENTITY CASCADE;
"@
    },
    @{
        Database = "kiteflow_academics"
        Description = "Academics"
        Sql = @"
TRUNCATE TABLE public.student_portal_notifications, public.schedule_blocks, public.lessons, public.enrollment_balance_ledger, public.enrollments, public.courses, public.instructors, public.students RESTART IDENTITY CASCADE;
"@
    },
    @{
        Database = "kiteflow_equipment"
        Description = "Equipment"
        Sql = @"
TRUNCATE TABLE public.maintenance_records, public.maintenance_rules, public.equipment_kit_items, public.equipment_kits, public.equipment_reservation_items, public.equipment_reservations, public.equipment_usage_logs, public.lesson_equipment_checkout_items, public.lesson_equipment_checkouts, public.equipment_items, public.gear_storages RESTART IDENTITY CASCADE;
"@
    },
    @{
        Database = "kiteflow_finance"
        Description = "Finance"
        Sql = @"
TRUNCATE TABLE public.financial_reconciliation_records, public.accounts_payable_payments, public.accounts_payable_entries, public.accounts_receivable_payments, public.accounts_receivable_entries, public.revenue_entries, public.expense_entries, public.financial_categories, public.cost_centers RESTART IDENTITY CASCADE;
"@
    },
    @{
        Database = "kiteflow_reporting"
        Description = "Reporting"
        Sql = @"
TRUNCATE TABLE public.report_snapshots RESTART IDENTITY CASCADE;
"@
    }
)

foreach ($statement in $cleanupStatements) {
    Write-Host "Limpando $($statement.Description)..."
    Invoke-DockerPsql -TargetContainer $resolvedContainer -Database $statement.Database -Sql $statement.Sql | Out-Host
}

$identitySql = @"
TRUNCATE TABLE public.refresh_sessions, public.password_reset_tokens, public.authentication_audit_events, public.user_invitations RESTART IDENTITY CASCADE;
"@

if ($KeepSystemAdmin) {
    $identitySql += @"
DELETE FROM public.user_accounts WHERE "Role" <> 1;
"@
}
else {
    $identitySql += @"
TRUNCATE TABLE public.user_accounts RESTART IDENTITY CASCADE;
"@
}

Write-Host "Limpando Identity..."
Invoke-DockerPsql -TargetContainer $resolvedContainer -Database "kiteflow_identity" -Sql $identitySql | Out-Host

$verificationQueries = @(
    @{
        Database = "kiteflow_identity"
        Description = "Identity"
        Sql = 'select count(*) as users, sum(case when "Role" = 1 then 1 else 0 end) as system_admins from public.user_accounts;'
    },
    @{
        Database = "kiteflow_schools"
        Description = "Schools"
        Sql = "select (select count(*) from public.schools) as schools, (select count(*) from public.user_profiles) as user_profiles;"
    },
    @{
        Database = "kiteflow_academics"
        Description = "Academics"
        Sql = "select (select count(*) from public.students) as students, (select count(*) from public.instructors) as instructors, (select count(*) from public.courses) as courses, (select count(*) from public.enrollments) as enrollments, (select count(*) from public.lessons) as lessons;"
    },
    @{
        Database = "kiteflow_equipment"
        Description = "Equipment"
        Sql = "select (select count(*) from public.gear_storages) as storages, (select count(*) from public.equipment_items) as items, (select count(*) from public.maintenance_records) as maintenance_records, (select count(*) from public.equipment_reservations) as reservations;"
    },
    @{
        Database = "kiteflow_finance"
        Description = "Finance"
        Sql = "select (select count(*) from public.revenue_entries) as revenues, (select count(*) from public.expense_entries) as expenses, (select count(*) from public.accounts_receivable_entries) as receivables, (select count(*) from public.accounts_payable_entries) as payables;"
    },
    @{
        Database = "kiteflow_reporting"
        Description = "Reporting"
        Sql = "select count(*) as snapshots from public.report_snapshots;"
    }
)

Write-Host ""
Write-Host "Resumo pos-limpeza"

foreach ($query in $verificationQueries) {
    Write-Host ""
    Write-Host "[$($query.Description)]"
    Invoke-DockerPsql -TargetContainer $resolvedContainer -Database $query.Database -Sql $query.Sql | Out-Host
}

Write-Host ""
Write-Host "Limpeza concluida."
