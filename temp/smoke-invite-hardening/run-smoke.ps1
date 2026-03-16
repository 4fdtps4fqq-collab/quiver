$ErrorActionPreference = "Stop"

$gatewayBase = "http://localhost:7000"
$adminEmail = "admin@quiver.local"
$adminPassword = "Admin123!"
$createdSchoolIds = New-Object System.Collections.Generic.List[string]

function Invoke-JsonRequest {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [string]$Token = $null
    )

    $headers = @{}
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }

    if ($null -ne $Body) {
        return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers -ContentType "application/json" -Body ($Body | ConvertTo-Json -Depth 8)
    }

    return Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers
}

function Invoke-JsonRequestExpectError {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null,
        [string]$Token = $null
    )

    try {
        Invoke-JsonRequest -Method $Method -Url $Url -Body $Body -Token $Token | Out-Null
        throw "Era esperado um erro para $Method $Url, mas a chamada foi concluída com sucesso."
    }
    catch {
        $response = $_.Exception.Response
        if (-not $response) {
            throw
        }

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
        }
    }
}

function Read-TemporaryPasswordFromOutbox {
    param([string]$Path)

    $content = Get-Content -Path $Path -Raw
    $line = $content -split "`r?`n" | Where-Object { $_ -like "Senha tempor*" } | Select-Object -First 1
    return ($line -split ":\s*", 2)[1].Trim()
}

function Remove-SmokeSchoolData {
    param([string[]]$SchoolIds)

    foreach ($schoolId in $SchoolIds) {
        @"
DELETE FROM student_portal_notifications WHERE "SchoolId" = '$schoolId';
DELETE FROM lessons WHERE "SchoolId" = '$schoolId';
DELETE FROM enrollment_balance_ledger WHERE "SchoolId" = '$schoolId';
DELETE FROM enrollments WHERE "SchoolId" = '$schoolId';
DELETE FROM students WHERE "SchoolId" = '$schoolId';
DELETE FROM instructors WHERE "SchoolId" = '$schoolId';
DELETE FROM courses WHERE "SchoolId" = '$schoolId';
"@ | docker exec -i postgres psql -U postgres -d kiteflow_academics -v ON_ERROR_STOP=1 | Out-Null

        @"
DELETE FROM user_profiles WHERE "SchoolId" = '$schoolId';
DELETE FROM school_settings WHERE "SchoolId" = '$schoolId';
DELETE FROM schools WHERE "Id" = '$schoolId';
"@ | docker exec -i postgres psql -U postgres -d kiteflow_schools -v ON_ERROR_STOP=1 | Out-Null

        @"
DELETE FROM user_invitations WHERE "SchoolId" = '$schoolId';
DELETE FROM refresh_sessions
WHERE "UserAccountId" IN (
    SELECT "Id" FROM user_accounts WHERE "SchoolId" = '$schoolId'
);
DELETE FROM user_accounts WHERE "SchoolId" = '$schoolId';
"@ | docker exec -i postgres psql -U postgres -d kiteflow_identity -v ON_ERROR_STOP=1 | Out-Null
    }
}

try {
    $adminSession = Invoke-JsonRequest -Method POST -Url "$gatewayBase/identity/api/v1/auth/login" -Body @{
        email = $adminEmail
        password = $adminPassword
        deviceName = "Smoke Invite Hardening"
    }

    $suffix = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $ownerEmail = "owner.invite.$suffix@quiver.local"
    $school = Invoke-JsonRequest -Method POST -Url "$gatewayBase/api/v1/system/schools" -Token $adminSession.token -Body @{
        legalName = "Escola Convite $suffix Ltda"
        displayName = "Escola Convite $suffix"
        ownerFullName = "Owner Convite $suffix"
        ownerEmail = $ownerEmail
        ownerPhone = "(27) 99999-3333"
        slug = "escola-convite-$suffix"
        timezone = "America/Sao_Paulo"
        currencyCode = "BRL"
    }

    $createdSchoolIds.Add($school.schoolId) | Out-Null
    $temporaryPassword = Read-TemporaryPasswordFromOutbox -Path $school.outboxFilePath

    $ownerTempSession = Invoke-JsonRequest -Method POST -Url "$gatewayBase/identity/api/v1/auth/login" -Body @{
        email = $ownerEmail
        password = $temporaryPassword
        deviceName = "Owner Invite Temp"
    }

    $ownerSession = Invoke-JsonRequest -Method POST -Url "$gatewayBase/identity/api/v1/auth/change-password" -Token $ownerTempSession.token -Body @{
        currentPassword = $temporaryPassword
        newPassword = "OwnerInvite123!"
        deviceName = "Owner Invite Updated"
    }

    $inviteeEmail = "student.invite.$suffix@quiver.local"
    $invitation = Invoke-JsonRequest -Method POST -Url "$gatewayBase/api/v1/school-users/invitations" -Token $ownerSession.token -Body @{
        email = $inviteeEmail
        fullName = "Aluno Convite $suffix"
        role = 4
        phone = "(27) 98888-4444"
        expiresInDays = 7
    }

    $accepted = Invoke-JsonRequest -Method POST -Url "$gatewayBase/api/v1/school-users/invitations/accept" -Body @{
        token = ($invitation.inviteLink -split "invite=")[1]
        password = "AlunoInvite123!"
    }

    $studentPortalSession = Invoke-JsonRequest -Method POST -Url "$gatewayBase/identity/api/v1/auth/login" -Body @{
        email = $inviteeEmail
        password = "AlunoInvite123!"
        deviceName = "Student Invite Session"
    }

    $portalOverview = Invoke-JsonRequest -Method GET -Url "$gatewayBase/academics/api/v1/student-portal/overview" -Token $studentPortalSession.token
    $forbiddenFinancialStatuses = Invoke-JsonRequestExpectError -Method GET -Url "$gatewayBase/finance/api/v1/finance/students/financial-statuses" -Token $studentPortalSession.token

    [pscustomobject]@{
        InvitationAccepted = $accepted.invitationAccepted
        StudentRole = $accepted.session.role
        PortalStudentName = $portalOverview.student.fullName
        ActiveEnrollments = $portalOverview.summary.activeEnrollments
        StudentFinanceStatusesStatus = $forbiddenFinancialStatuses.StatusCode
    } | ConvertTo-Json -Depth 5
}
finally {
    Remove-SmokeSchoolData -SchoolIds $createdSchoolIds
}
