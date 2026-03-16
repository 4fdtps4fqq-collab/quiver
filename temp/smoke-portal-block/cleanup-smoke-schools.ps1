$ErrorActionPreference = 'Stop'

$schoolIds = @(
    '6eec44f0-f1d3-4ec6-ac5c-1f195bc9160c',
    '06958deb-27a4-4d4d-99cf-22577fc7ae7e',
    'bac5e9a6-89df-4b67-b5b4-c8df3425ebc5',
    '45c1dec7-ffe6-4d70-a39d-9118dadffcb4',
    'ef05b7c2-ec05-43df-98f5-e18de9af5db7'
)

function Invoke-Psql {
    param(
        [Parameter(Mandatory = $true)][string]$Database,
        [Parameter(Mandatory = $true)][string]$Sql
    )

    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
        Set-Content -Path $tempFile -Value $Sql -Encoding UTF8
        Get-Content -Path $tempFile | & docker exec -i postgres psql -U postgres -d $Database
    }
    finally {
        Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    }
}

foreach ($schoolId in $schoolIds) {
    Invoke-Psql -Database 'kiteflow_finance' -Sql ('delete from accounts_receivable_payments where "SchoolId" = ''{0}''; delete from accounts_receivable_entries where "SchoolId" = ''{0}''; delete from revenue_entries where "SchoolId" = ''{0}''; delete from expense_entries where "SchoolId" = ''{0}'';' -f $schoolId) | Out-Null
    Invoke-Psql -Database 'kiteflow_equipment' -Sql ('delete from lesson_equipment_checkout_items where "CheckoutId" in (select "Id" from lesson_equipment_checkouts where "SchoolId" = ''{0}''); delete from lesson_equipment_checkouts where "SchoolId" = ''{0}''; delete from equipment_usage_logs where "SchoolId" = ''{0}''; delete from maintenance_records where "SchoolId" = ''{0}''; delete from maintenance_rules where "SchoolId" = ''{0}''; delete from equipment_items where "SchoolId" = ''{0}''; delete from gear_storages where "SchoolId" = ''{0}'';' -f $schoolId) | Out-Null
    Invoke-Psql -Database 'kiteflow_academics' -Sql ('delete from lessons where "SchoolId" = ''{0}''; delete from enrollment_balance_ledger where "SchoolId" = ''{0}''; delete from enrollments where "SchoolId" = ''{0}''; delete from courses where "SchoolId" = ''{0}''; delete from instructors where "SchoolId" = ''{0}''; delete from student_portal_notifications where "SchoolId" = ''{0}''; delete from students where "SchoolId" = ''{0}'';' -f $schoolId) | Out-Null
    Invoke-Psql -Database 'kiteflow_schools' -Sql ('delete from user_profiles where "SchoolId" = ''{0}''; delete from school_settings where "SchoolId" = ''{0}''; delete from schools where "Id" = ''{0}'';' -f $schoolId) | Out-Null
    Invoke-Psql -Database 'kiteflow_identity' -Sql ('delete from refresh_sessions where "UserAccountId" in (select "Id" from user_accounts where "SchoolId" = ''{0}''); delete from user_accounts where "SchoolId" = ''{0}'';' -f $schoolId) | Out-Null
}

Write-Output 'Smoke tenants removidos.'
