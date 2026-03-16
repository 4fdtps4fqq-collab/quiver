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

        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $bodyText = $reader.ReadToEnd()

        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body = $bodyText
        }
    }
}

function Read-TemporaryPasswordFromOutbox {
    param([string]$Path)

    $content = Get-Content -Path $Path -Raw
    $line = $content -split "`r?`n" | Where-Object { $_ -like "Senha tempor*" } | Select-Object -First 1
    if (-not $line) {
        throw "Não foi possível localizar a senha temporária em $Path."
    }

    return ($line -split ":\s*", 2)[1].Trim()
}

function Remove-SmokeSchoolData {
    param([string[]]$SchoolIds)

    if (-not $SchoolIds -or $SchoolIds.Count -eq 0) {
        return
    }

    foreach ($schoolId in $SchoolIds) {
        @"
DELETE FROM students WHERE "SchoolId" = '$schoolId';
DELETE FROM instructors WHERE "SchoolId" = '$schoolId';
"@ | docker exec -i postgres psql -U postgres -d kiteflow_academics -v ON_ERROR_STOP=1 | Out-Null

        @"
DELETE FROM user_profiles WHERE "SchoolId" = '$schoolId';
DELETE FROM school_settings WHERE "SchoolId" = '$schoolId';
DELETE FROM schools WHERE "Id" = '$schoolId';
"@ | docker exec -i postgres psql -U postgres -d kiteflow_schools -v ON_ERROR_STOP=1 | Out-Null

        @"
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
        deviceName = "Smoke SaaS Hardening"
    }

    function New-SmokeSchool {
        param(
            [string]$Suffix,
            [string]$OwnerEmail
        )

        $school = Invoke-JsonRequest -Method POST -Url "$gatewayBase/api/v1/system/schools" -Token $adminSession.token -Body @{
            legalName = "Escola SaaS $Suffix Ltda"
            displayName = "Escola SaaS $Suffix"
            ownerFullName = "Owner $Suffix"
            ownerEmail = $OwnerEmail
            ownerPhone = "(27) 99999-0000"
            slug = "escola-saas-$Suffix"
            timezone = "America/Sao_Paulo"
            currencyCode = "BRL"
        }

        $createdSchoolIds.Add($school.schoolId) | Out-Null
        $temporaryPassword = Read-TemporaryPasswordFromOutbox -Path $school.outboxFilePath

        return [pscustomobject]@{
            SchoolId = $school.schoolId
            OwnerUserId = $school.ownerUserId
            OutboxFilePath = $school.outboxFilePath
            TemporaryPassword = $temporaryPassword
            OwnerEmail = $OwnerEmail
        }
    }

    $suffixA = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    Write-Host "Criando escolas de smoke..."
    $schoolA = New-SmokeSchool -Suffix "A$suffixA" -OwnerEmail "owner.a.$suffixA@quiver.local"
    $schoolB = New-SmokeSchool -Suffix "B$suffixA" -OwnerEmail "owner.b.$suffixA@quiver.local"

    Write-Host "Fazendo login temporário do owner A..."
    $ownerLoginA = Invoke-JsonRequest -Method POST -Url "$gatewayBase/identity/api/v1/auth/login" -Body @{
        email = $schoolA.OwnerEmail
        password = $schoolA.TemporaryPassword
        deviceName = "Owner A Temp Login"
    }

    Write-Host "Validando bloqueio antes da troca de senha..."
    $blockedCurrent = Invoke-JsonRequestExpectError -Method GET -Url "$gatewayBase/schools/api/v1/schools/current" -Token $ownerLoginA.token

    Write-Host "Trocando a senha do owner A..."
    $ownerSessionA = Invoke-JsonRequest -Method POST -Url "$gatewayBase/identity/api/v1/auth/change-password" -Token $ownerLoginA.token -Body @{
        currentPassword = $schoolA.TemporaryPassword
        newPassword = "OwnerA123!"
        deviceName = "Owner A Updated Session"
    }

    Write-Host "Validando acesso após a troca de senha..."
    $ownerCurrentSchoolA = Invoke-JsonRequest -Method GET -Url "$gatewayBase/schools/api/v1/schools/current" -Token $ownerSessionA.token

    Write-Host "Fazendo login temporário do owner B..."
    $ownerLoginB = Invoke-JsonRequest -Method POST -Url "$gatewayBase/identity/api/v1/auth/login" -Body @{
        email = $schoolB.OwnerEmail
        password = $schoolB.TemporaryPassword
        deviceName = "Owner B Temp Login"
    }

    Write-Host "Trocando a senha do owner B..."
    $ownerSessionB = Invoke-JsonRequest -Method POST -Url "$gatewayBase/identity/api/v1/auth/change-password" -Token $ownerLoginB.token -Body @{
        currentPassword = $schoolB.TemporaryPassword
        newPassword = "OwnerB123!"
        deviceName = "Owner B Updated Session"
    }

    $studentEmail = "student.shared.$suffixA@quiver.local"
    $instructorEmail = "instructor.shared.$suffixA@quiver.local"

    Write-Host "Criando aluno ativo na escola A..."
    Invoke-JsonRequest -Method POST -Url "$gatewayBase/academics/api/v1/students" -Token $ownerSessionA.token -Body @{
        fullName = "Aluno Compartilhado"
        email = $studentEmail
        phone = "(27) 99999-1111"
    } | Out-Null

    Write-Host "Validando conflito de aluno ativo entre escolas..."
    $studentConflict = Invoke-JsonRequestExpectError -Method POST -Url "$gatewayBase/academics/api/v1/students" -Token $ownerSessionB.token -Body @{
        fullName = "Aluno Compartilhado B"
        email = $studentEmail
        phone = "(27) 99999-2222"
    }

    Write-Host "Criando instrutor ativo na escola A..."
    Invoke-JsonRequest -Method POST -Url "$gatewayBase/academics/api/v1/instructors" -Token $ownerSessionA.token -Body @{
        fullName = "Instrutor Compartilhado"
        email = $instructorEmail
        phone = "(27) 98888-1111"
        specialties = "Wave"
        hourlyRate = 220
    } | Out-Null

    Write-Host "Validando conflito de instrutor ativo entre escolas..."
    $instructorConflict = Invoke-JsonRequestExpectError -Method POST -Url "$gatewayBase/academics/api/v1/instructors" -Token $ownerSessionB.token -Body @{
        fullName = "Instrutor Compartilhado B"
        email = $instructorEmail
        phone = "(27) 98888-2222"
        specialties = "Freestyle"
        hourlyRate = 240
    }

    [pscustomobject]@{
        CreatedSchools = $createdSchoolIds.Count
        OwnerMustChangePassword = $ownerLoginA.mustChangePassword
        BlockedBeforePasswordChangeStatus = $blockedCurrent.StatusCode
        PasswordChangeReturnedMustChangePassword = $ownerSessionA.mustChangePassword
        SchoolsCurrentAfterPasswordChange = $ownerCurrentSchoolA.displayName
        StudentCrossSchoolConflictStatus = $studentConflict.StatusCode
        InstructorCrossSchoolConflictStatus = $instructorConflict.StatusCode
        OwnerOutboxFile = $schoolA.OutboxFilePath
    } | ConvertTo-Json -Depth 6
}
finally {
    Remove-SmokeSchoolData -SchoolIds $createdSchoolIds
}
