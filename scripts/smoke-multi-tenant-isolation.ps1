param(
    [string]$SystemAdminEmail = "filipe@quiver.com",
    [string]$SystemAdminPassword = "M1H2sy27!pedro01",
    [switch]$KeepData
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$logDir = Join-Path $root "temp\multi-tenant-isolation-smoke"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$started = @()
$stamp = Get-Date -Format "yyyyMMddHHmmss"
$tenantPrefix = "smoke-isolation-$stamp"

$adminPermissions = @(
    "dashboard.view",
    "students.manage",
    "instructors.manage",
    "courses.manage",
    "enrollments.manage",
    "lessons.manage",
    "equipment.manage",
    "maintenance.manage",
    "finance.manage",
    "school.manage"
)

$instructorPermissions = @(
    "dashboard.view",
    "students.manage",
    "courses.manage",
    "lessons.manage",
    "equipment.manage",
    "maintenance.manage"
)

$equipmentTypeSequence = @(1, 2, 6, 4, 3, 5, 1, 2, 6, 4)

function Test-HttpAvailable {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
    } catch {
        return $false
    }
}

function Get-PortFromUrl {
    param([string]$Url)
    return ([System.Uri]$Url).Port
}

function Test-PortListening {
    param([int]$Port)

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
        $connected = $async.AsyncWaitHandle.WaitOne(1500, $false)
        if (-not $connected) {
            return $false
        }

        $client.EndConnect($async)
        return $true
    } catch {
        return $false
    } finally {
        $client.Dispose()
    }
}

function Start-ServiceProcess {
    param(
        [string]$Name,
        [string]$Workdir,
        [string]$Url
    )

    $stdout = Join-Path $logDir "$Name.out.log"
    $stderr = Join-Path $logDir "$Name.err.log"

    $process = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @("run", "--no-build", "--no-launch-profile", "--urls", $Url) `
        -WorkingDirectory $Workdir `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru

    $script:started += [PSCustomObject]@{
        Name = $Name
        Pid = $process.Id
        Url = $Url
    }
}

function Wait-ForHttp {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 40
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return
            }
        } catch {
            Start-Sleep -Milliseconds 800
        }
    }

    throw "Tempo esgotado aguardando $Url"
}

function Ensure-ServiceProcess {
    param(
        [string]$Name,
        [string]$Workdir,
        [string]$Url
    )

    if (Test-HttpAvailable -Url "$Url/swagger") {
        Write-Host "Reaproveitando serviço ativo: $Name"
        return
    }

    $port = Get-PortFromUrl -Url $Url
    if (Test-PortListening -Port $port) {
        Write-Host "A porta $port já está em uso. Aguardando o serviço responder: $Name"
        Wait-ForHttp -Url "$Url/swagger" -TimeoutSeconds 20
        return
    }

    Start-ServiceProcess -Name $Name -Workdir $Workdir -Url $Url
    Wait-ForHttp -Url "$Url/swagger"
}

function ConvertFrom-JsonSafely {
    param([string]$Raw)

    if ([string]::IsNullOrWhiteSpace($Raw)) {
        return $null
    }

    try {
        return $Raw | ConvertFrom-Json
    } catch {
        return $Raw
    }
}

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body,
        [hashtable]$Headers
    )

    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 12 } else { $null }

    try {
        $response = Invoke-WebRequest `
            -Uri $Url `
            -Method $Method `
            -ContentType "application/json" `
            -Headers $Headers `
            -Body $json `
            -UseBasicParsing

        return [PSCustomObject]@{
            StatusCode = [int]$response.StatusCode
            Body = ConvertFrom-JsonSafely $response.Content
            Raw = $response.Content
        }
    } catch {
        if (-not $_.Exception.Response) {
            throw
        }

        $response = $_.Exception.Response
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
        $raw = $reader.ReadToEnd()
        $reader.Dispose()

        return [PSCustomObject]@{
            StatusCode = [int]$response.StatusCode
            Body = ConvertFrom-JsonSafely $raw
            Raw = $raw
        }
    }
}

function Invoke-ApiExpect {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body,
        [hashtable]$Headers,
        [int[]]$ExpectedStatusCodes
    )

    $response = Invoke-Api -Method $Method -Url $Url -Body $Body -Headers $Headers
    if ($ExpectedStatusCodes -notcontains $response.StatusCode) {
        throw "Chamada $Method $Url retornou status $($response.StatusCode). Esperado: $($ExpectedStatusCodes -join ', '). Corpo: $($response.Raw)"
    }

    return $response
}

function Get-TemporaryPasswordFromOutbox {
    param([string]$OutboxFilePath)

    $line = Get-Content -Path $OutboxFilePath | Where-Object { $_ -like "Senha tempor*:*" } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($line)) {
        throw "Não foi possível extrair a senha temporária do arquivo $OutboxFilePath"
    }

    return (($line -split ":\s*", 2)[1]).Trim()
}

function Login-User {
    param(
        [string]$Email,
        [string]$Password
    )

    $response = Invoke-ApiExpect `
        -Method "POST" `
        -Url "http://localhost:7000/identity/api/v1/auth/login" `
        -Body @{
            email = $Email
            password = $Password
            deviceName = "multi-tenant-smoke"
        } `
        -Headers @{} `
        -ExpectedStatusCodes @(200)

    return $response
}

function Change-Password {
    param(
        [string]$Token,
        [string]$CurrentPassword,
        [string]$NewPassword
    )

    $response = Invoke-ApiExpect `
        -Method "POST" `
        -Url "http://localhost:7000/identity/api/v1/auth/change-password" `
        -Body @{
            currentPassword = $CurrentPassword
            newPassword = $NewPassword
            deviceName = "multi-tenant-smoke"
        } `
        -Headers @{ Authorization = "Bearer $Token" } `
        -ExpectedStatusCodes @(200)

    return $response.Body
}

function Assert {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

try {
    Ensure-ServiceProcess -Name "identity" -Workdir (Join-Path $root "src\backend\services\Identity\KiteFlow.Services.Identity.Api") -Url "http://localhost:7001"
    Ensure-ServiceProcess -Name "schools" -Workdir (Join-Path $root "src\backend\services\Schools\KiteFlow.Services.Schools.Api") -Url "http://localhost:7002"
    Ensure-ServiceProcess -Name "academics" -Workdir (Join-Path $root "src\backend\services\Academics\KiteFlow.Services.Academics.Api") -Url "http://localhost:7003"
    Ensure-ServiceProcess -Name "equipment" -Workdir (Join-Path $root "src\backend\services\Equipment\KiteFlow.Services.Equipment.Api") -Url "http://localhost:7004"
    Ensure-ServiceProcess -Name "finance" -Workdir (Join-Path $root "src\backend\services\Finance\KiteFlow.Services.Finance.Api") -Url "http://localhost:7005"
    Ensure-ServiceProcess -Name "reporting" -Workdir (Join-Path $root "src\backend\services\Reporting\KiteFlow.Services.Reporting.Api") -Url "http://localhost:7006"
    Ensure-ServiceProcess -Name "gateway" -Workdir (Join-Path $root "src\backend\gateway\KiteFlow.Gateway") -Url "http://localhost:7000"

    $systemAdminLogin = Login-User -Email $SystemAdminEmail -Password $SystemAdminPassword
    $systemAdminSession = $systemAdminLogin.Body
    $systemAdminToken = $systemAdminSession.token
    $systemAdminHeaders = @{ Authorization = "Bearer $systemAdminToken" }

    $schools = @()

    foreach ($schoolIndex in 1..4) {
        $schoolSlug = "$tenantPrefix-s$schoolIndex"
        $displayName = "Smoke Isolation Escola $schoolIndex"
        $ownerEmail = "owner.s$schoolIndex.$stamp@quiver.local"

        $createSchool = Invoke-ApiExpect `
            -Method "POST" `
            -Url "http://localhost:7000/api/v1/system/schools" `
            -Body @{
                legalName = "Smoke Isolation Escola Juridica $schoolIndex"
                displayName = $displayName
                cnpj = $null
                baseBeachName = "Praia Base $schoolIndex"
                baseLatitude = -20.3000 - ($schoolIndex / 1000)
                baseLongitude = -40.2800 - ($schoolIndex / 1000)
                postalCode = "29100-000"
                street = "Avenida Praia $schoolIndex"
                streetNumber = "$schoolIndex"
                addressComplement = "Base operacional"
                neighborhood = "Centro"
                city = "Vila Velha"
                state = "ES"
                ownerFullName = "Owner Escola $schoolIndex"
                ownerEmail = $ownerEmail
                ownerCpf = ("1111111111{0}" -f $schoolIndex)
                ownerPhone = ("2799999000{0}" -f $schoolIndex)
                ownerPostalCode = "29100-000"
                ownerStreet = "Rua Owner $schoolIndex"
                ownerStreetNumber = "$schoolIndex"
                ownerAddressComplement = "Casa"
                ownerNeighborhood = "Centro"
                ownerCity = "Vila Velha"
                ownerState = "ES"
                slug = $schoolSlug
                timezone = "America/Sao_Paulo"
                currencyCode = "BRL"
                logoDataUrl = $null
                themePrimary = "#0B3C5D"
                themeAccent = "#2ED4A7"
                bookingLeadTimeMinutes = 60
                cancellationWindowHours = 24
            } `
            -Headers $systemAdminHeaders `
            -ExpectedStatusCodes @(200)

        $temporaryPassword = Get-TemporaryPasswordFromOutbox -OutboxFilePath $createSchool.Body.outboxFilePath
        $ownerFirstLogin = Login-User -Email $ownerEmail -Password $temporaryPassword
        $ownerFirstSession = $ownerFirstLogin.Body
        $ownerPassword = "Owner$schoolIndex!Smoke123"
        $ownerSession = Change-Password -Token $ownerFirstSession.token -CurrentPassword $temporaryPassword -NewPassword $ownerPassword
        $ownerHeaders = @{ Authorization = "Bearer $($ownerSession.token)" }

        $schoolRecord = [ordered]@{
            Index = $schoolIndex
            Slug = $schoolSlug
            DisplayName = $displayName
            SchoolId = $createSchool.Body.schoolId
            OwnerEmail = $ownerEmail
            OwnerPassword = $ownerPassword
            OwnerToken = $ownerSession.token
            OwnerHeaders = $ownerHeaders
            Collaborators = @()
            Admins = @()
            Instructors = @()
            Students = @()
            Equipment = @()
            Revenues = @()
            Expenses = @()
            ExpectedRevenue = [decimal]0
            ExpectedExpense = [decimal]0
        }

        foreach ($adminIndex in 1..3) {
            $adminEmail = "admin.$schoolIndex.$adminIndex.$stamp@quiver.local"
            $adminPassword = "Admin$schoolIndex$adminIndex!123"
            $createdAdmin = Invoke-ApiExpect `
                -Method "POST" `
                -Url "http://localhost:7000/api/v1/school-users" `
                -Body @{
                    fullName = "Administrativo $schoolIndex-$adminIndex"
                    email = $adminEmail
                    password = $adminPassword
                    role = 5
                    permissions = $adminPermissions
                    phone = ("2791111{0}{1}{2}" -f $schoolIndex, $adminIndex, "00")
                    isActive = $true
                    mustChangePassword = $false
                } `
                -Headers $ownerHeaders `
                -ExpectedStatusCodes @(200, 201)

            $adminLogin = Login-User -Email $adminEmail -Password $adminPassword
            $adminSession = $adminLogin.Body
            $adminRecord = [ordered]@{
                FullName = "Administrativo $schoolIndex-$adminIndex"
                Email = $adminEmail
                Password = $adminPassword
                Token = $adminSession.token
                IdentityUserId = $createdAdmin.Body.identityUserId
            }
            $schoolRecord.Admins += $adminRecord
            $schoolRecord.Collaborators += $adminRecord
        }

        foreach ($instructorIndex in 1..5) {
            $instructorEmail = "instructor.$schoolIndex.$instructorIndex.$stamp@quiver.local"
            $instructorPassword = "Instr$schoolIndex$instructorIndex!123"
            $createdInstructorUser = Invoke-ApiExpect `
                -Method "POST" `
                -Url "http://localhost:7000/api/v1/school-users" `
                -Body @{
                    fullName = "Instrutor $schoolIndex-$instructorIndex"
                    email = $instructorEmail
                    password = $instructorPassword
                    role = 3
                    permissions = $instructorPermissions
                    phone = ("2792222{0}{1}{2}" -f $schoolIndex, $instructorIndex, "00")
                    isActive = $true
                    mustChangePassword = $false
                } `
                -Headers $ownerHeaders `
                -ExpectedStatusCodes @(200, 201)

            $createdInstructor = Invoke-ApiExpect `
                -Method "POST" `
                -Url "http://localhost:7000/academics/api/v1/instructors" `
                -Body @{
                    fullName = "Instrutor $schoolIndex-$instructorIndex"
                    email = $instructorEmail
                    phone = ("2792222{0}{1}{2}" -f $schoolIndex, $instructorIndex, "00")
                    specialties = "Freeride, ondas e segurança"
                    hourlyRate = 180 + ($schoolIndex * 10) + $instructorIndex
                    identityUserId = $createdInstructorUser.Body.identityUserId
                } `
                -Headers $ownerHeaders `
                -ExpectedStatusCodes @(201)

            $instructorRecord = [ordered]@{
                FullName = "Instrutor $schoolIndex-$instructorIndex"
                Email = $instructorEmail
                IdentityUserId = $createdInstructorUser.Body.identityUserId
                InstructorId = $createdInstructor.Body.instructorId
            }
            $schoolRecord.Instructors += $instructorRecord
            $schoolRecord.Collaborators += $instructorRecord
        }

        foreach ($studentIndex in 1..10) {
            $studentEmail = "student.$schoolIndex.$studentIndex.$stamp@quiver.local"
            $createdStudent = Invoke-ApiExpect `
                -Method "POST" `
                -Url "http://localhost:7000/academics/api/v1/students" `
                -Body @{
                    fullName = "Aluno $schoolIndex-$studentIndex"
                    email = $studentEmail
                    phone = ("2793333{0}{1}{2}" -f $schoolIndex, $studentIndex, "00")
                    postalCode = "29100-000"
                    street = "Rua Aluno $studentIndex"
                    streetNumber = "$studentIndex"
                    addressComplement = "Casa"
                    neighborhood = "Centro"
                    city = "Vila Velha"
                    state = "ES"
                    birthDate = "1995-01-{0}" -f ($studentIndex.ToString().PadLeft(2, '0'))
                    medicalNotes = "Sem restrições"
                    emergencyContactName = "Contato $studentIndex"
                    emergencyContactPhone = ("2794444{0}{1}{2}" -f $schoolIndex, $studentIndex, "00")
                    identityUserId = $null
                } `
                -Headers $ownerHeaders `
                -ExpectedStatusCodes @(201)

            $schoolRecord.Students += [ordered]@{
                FullName = "Aluno $schoolIndex-$studentIndex"
                Email = $studentEmail
                StudentId = $createdStudent.Body.studentId
            }
        }

        $storage = Invoke-ApiExpect `
            -Method "POST" `
            -Url "http://localhost:7000/equipment/api/v1/storages" `
            -Body @{
                name = "Depósito $schoolIndex"
                locationNote = "Base operacional $schoolIndex"
            } `
            -Headers $ownerHeaders `
            -ExpectedStatusCodes @(201)

        foreach ($equipmentIndex in 1..10) {
            $equipmentType = $equipmentTypeSequence[$equipmentIndex - 1]
            $createdEquipment = Invoke-ApiExpect `
                -Method "POST" `
                -Url "http://localhost:7000/equipment/api/v1/equipment-items" `
                -Body @{
                    storageId = $storage.Body.storageId
                    name = "Equipamento $schoolIndex-$equipmentIndex"
                    type = $equipmentType
                    tagCode = "EQ-$schoolIndex-$equipmentIndex"
                    brand = "Quiver"
                    model = "Modelo $equipmentIndex"
                    sizeLabel = "T$equipmentIndex"
                    currentCondition = 2
                } `
                -Headers $ownerHeaders `
                -ExpectedStatusCodes @(201)

            $schoolRecord.Equipment += [ordered]@{
                EquipmentId = $createdEquipment.Body.equipmentId
                Name = "Equipamento $schoolIndex-$equipmentIndex"
            }
        }

        foreach ($revenueIndex in 1..3) {
            $revenueAmount = [decimal](1000 * $schoolIndex + 100 * $revenueIndex)
            $createdRevenue = Invoke-ApiExpect `
                -Method "POST" `
                -Url "http://localhost:7000/finance/api/v1/finance/revenues" `
                -Body @{
                    sourceType = 3
                    sourceId = $null
                    category = "Receita Escola $schoolIndex"
                    amount = $revenueAmount
                    recognizedAtUtc = (Get-Date).ToUniversalTime().AddDays(-$revenueIndex).ToString("O")
                    description = "Receita $schoolIndex-$revenueIndex"
                } `
                -Headers $ownerHeaders `
                -ExpectedStatusCodes @(201)

            $schoolRecord.ExpectedRevenue += $revenueAmount
            $schoolRecord.Revenues += [ordered]@{
                RevenueId = $createdRevenue.Body.revenueId
                Description = "Receita $schoolIndex-$revenueIndex"
                Amount = $revenueAmount
            }
        }

        foreach ($expenseIndex in 1..2) {
            $expenseAmount = [decimal](250 * $schoolIndex + 25 * $expenseIndex)
            $createdExpense = Invoke-ApiExpect `
                -Method "POST" `
                -Url "http://localhost:7000/finance/api/v1/finance/expenses" `
                -Body @{
                    category = 2
                    amount = $expenseAmount
                    occurredAtUtc = (Get-Date).ToUniversalTime().AddDays(-$expenseIndex).ToString("O")
                    description = "Despesa $schoolIndex-$expenseIndex"
                    vendor = "Fornecedor $schoolIndex"
                } `
                -Headers $ownerHeaders `
                -ExpectedStatusCodes @(201)

            $schoolRecord.ExpectedExpense += $expenseAmount
            $schoolRecord.Expenses += [ordered]@{
                ExpenseId = $createdExpense.Body.expenseId
                Description = "Despesa $schoolIndex-$expenseIndex"
                Amount = $expenseAmount
            }
        }

        $schools += [PSCustomObject]$schoolRecord
    }

    foreach ($school in $schools) {
        $ownerHeaders = $school.OwnerHeaders
        $studentsResponse = Invoke-ApiExpect -Method "GET" -Url "http://localhost:7000/academics/api/v1/students" -Body $null -Headers $ownerHeaders -ExpectedStatusCodes @(200)
        $instructorsResponse = Invoke-ApiExpect -Method "GET" -Url "http://localhost:7000/academics/api/v1/instructors" -Body $null -Headers $ownerHeaders -ExpectedStatusCodes @(200)
        $usersResponse = Invoke-ApiExpect -Method "GET" -Url "http://localhost:7000/api/v1/school-users" -Body $null -Headers $ownerHeaders -ExpectedStatusCodes @(200)
        $equipmentResponse = Invoke-ApiExpect -Method "GET" -Url "http://localhost:7000/equipment/api/v1/equipment-items" -Body $null -Headers $ownerHeaders -ExpectedStatusCodes @(200)
        $revenuesResponse = Invoke-ApiExpect -Method "GET" -Url "http://localhost:7000/finance/api/v1/finance/revenues" -Body $null -Headers $ownerHeaders -ExpectedStatusCodes @(200)
        $expensesResponse = Invoke-ApiExpect -Method "GET" -Url "http://localhost:7000/finance/api/v1/finance/expenses" -Body $null -Headers $ownerHeaders -ExpectedStatusCodes @(200)
        $overviewResponse = Invoke-ApiExpect -Method "GET" -Url "http://localhost:7000/finance/api/v1/finance/overview" -Body $null -Headers $ownerHeaders -ExpectedStatusCodes @(200)

        Assert ($studentsResponse.Body.Count -eq 10) "A escola $($school.DisplayName) não retornou 10 alunos."
        Assert ($instructorsResponse.Body.Count -eq 5) "A escola $($school.DisplayName) não retornou 5 instrutores."
        Assert ($usersResponse.Body.Count -eq 9) "A escola $($school.DisplayName) não retornou 9 colaboradores/owner."
        Assert ($equipmentResponse.Body.Count -eq 10) "A escola $($school.DisplayName) não retornou 10 equipamentos."
        Assert ($revenuesResponse.Body.Count -eq 3) "A escola $($school.DisplayName) não retornou 3 receitas."
        Assert ($expensesResponse.Body.Count -eq 2) "A escola $($school.DisplayName) não retornou 2 despesas."
        Assert ([decimal]$overviewResponse.Body.totalRevenue -eq $school.ExpectedRevenue) "A receita total da escola $($school.DisplayName) não está isolada."
        Assert ([decimal]$overviewResponse.Body.manualExpenseTotal -eq $school.ExpectedExpense) "A despesa total da escola $($school.DisplayName) não está isolada."

        foreach ($otherSchool in ($schools | Where-Object { $_.SchoolId -ne $school.SchoolId })) {
            Assert (-not ($studentsResponse.Body | Where-Object { $_.fullName -like "Aluno $($otherSchool.Index)-*" })) "Vazamento de alunos de $($otherSchool.DisplayName) para $($school.DisplayName)."
            Assert (-not ($instructorsResponse.Body | Where-Object { $_.fullName -like "Instrutor $($otherSchool.Index)-*" })) "Vazamento de instrutores de $($otherSchool.DisplayName) para $($school.DisplayName)."
            Assert (-not ($usersResponse.Body | Where-Object { $_.email -like "*.${($otherSchool.Index)}.*" })) "Vazamento de colaboradores de $($otherSchool.DisplayName) para $($school.DisplayName)."
            Assert (-not ($equipmentResponse.Body | Where-Object { $_.name -like "Equipamento $($otherSchool.Index)-*" })) "Vazamento de equipamentos de $($otherSchool.DisplayName) para $($school.DisplayName)."
            Assert (-not ($revenuesResponse.Body | Where-Object { $_.description -like "Receita $($otherSchool.Index)-*" })) "Vazamento de receitas de $($otherSchool.DisplayName) para $($school.DisplayName)."
            Assert (-not ($expensesResponse.Body | Where-Object { $_.description -like "Despesa $($otherSchool.Index)-*" })) "Vazamento de despesas de $($otherSchool.DisplayName) para $($school.DisplayName)."
        }
    }

    for ($i = 0; $i -lt $schools.Count; $i++) {
        for ($j = 0; $j -lt $schools.Count; $j++) {
            if ($i -eq $j) {
                continue
            }

            $sourceSchool = $schools[$i]
            $targetSchool = $schools[$j]
            $targetHeaders = $targetSchool.OwnerHeaders

            $studentFromSource = $sourceSchool.Students[0]
            $instructorFromSource = $sourceSchool.Instructors[0]
            $equipmentFromSource = $sourceSchool.Equipment[0]
            $revenueFromSource = $sourceSchool.Revenues[0]
            $expenseFromSource = $sourceSchool.Expenses[0]
            $adminFromSource = $sourceSchool.Admins[0]

            $studentUpdate = Invoke-ApiExpect `
                -Method "PUT" `
                -Url "http://localhost:7000/academics/api/v1/students/$($studentFromSource.StudentId)" `
                -Body @{
                    fullName = $studentFromSource.FullName
                    email = $studentFromSource.Email
                    phone = "27955550000"
                    postalCode = "29100-000"
                    street = "Rua Teste"
                    streetNumber = "1"
                    addressComplement = "Casa"
                    neighborhood = "Centro"
                    city = "Vila Velha"
                    state = "ES"
                    identityUserId = $null
                    birthDate = "1995-01-01"
                    medicalNotes = "Teste"
                    emergencyContactName = "Teste"
                    emergencyContactPhone = "27955550001"
                    firstStandUpAtUtc = $null
                    isActive = $true
                } `
                -Headers $targetHeaders `
                -ExpectedStatusCodes @(404)

            $instructorUpdate = Invoke-ApiExpect `
                -Method "PUT" `
                -Url "http://localhost:7000/academics/api/v1/instructors/$($instructorFromSource.InstructorId)" `
                -Body @{
                    fullName = $instructorFromSource.FullName
                    email = $instructorFromSource.Email
                    phone = "27955550002"
                    specialties = "Teste"
                    hourlyRate = 250
                    identityUserId = $instructorFromSource.IdentityUserId
                    isActive = $true
                } `
                -Headers $targetHeaders `
                -ExpectedStatusCodes @(404)

            $equipmentHistory = Invoke-ApiExpect `
                -Method "GET" `
                -Url "http://localhost:7000/equipment/api/v1/equipment-items/$($equipmentFromSource.EquipmentId)/history" `
                -Body $null `
                -Headers $targetHeaders `
                -ExpectedStatusCodes @(404)

            $revenueUpdate = Invoke-ApiExpect `
                -Method "PUT" `
                -Url "http://localhost:7000/finance/api/v1/finance/revenues/$($revenueFromSource.RevenueId)" `
                -Body @{
                    sourceType = 3
                    sourceId = $null
                    category = "Teste cruzado"
                    amount = 1
                    recognizedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
                    description = "Nao deveria atualizar"
                } `
                -Headers $targetHeaders `
                -ExpectedStatusCodes @(404)

            $expenseUpdate = Invoke-ApiExpect `
                -Method "PUT" `
                -Url "http://localhost:7000/finance/api/v1/finance/expenses/$($expenseFromSource.ExpenseId)" `
                -Body @{
                    category = 2
                    amount = 1
                    occurredAtUtc = (Get-Date).ToUniversalTime().ToString("O")
                    description = "Nao deveria atualizar"
                    vendor = "Teste cruzado"
                } `
                -Headers $targetHeaders `
                -ExpectedStatusCodes @(404)

            $schoolUserUpdate = Invoke-ApiExpect `
                -Method "PUT" `
                -Url "http://localhost:7000/api/v1/school-users/$($adminFromSource.IdentityUserId)" `
                -Body @{
                    profileId = $adminFromSource.IdentityUserId
                    fullName = $adminFromSource.FullName
                    role = 5
                    permissions = $adminPermissions
                    phone = "27966660000"
                    isActive = $true
                    mustChangePassword = $false
                } `
                -Headers @{ Authorization = "Bearer $($targetSchool.Admins[0].Token)" } `
                -ExpectedStatusCodes @(404)

            Assert ($studentUpdate.StatusCode -eq 404) "Falha no isolamento de aluno entre $($sourceSchool.DisplayName) e $($targetSchool.DisplayName)."
            Assert ($instructorUpdate.StatusCode -eq 404) "Falha no isolamento de instrutor entre $($sourceSchool.DisplayName) e $($targetSchool.DisplayName)."
            Assert ($equipmentHistory.StatusCode -eq 404) "Falha no isolamento de equipamento entre $($sourceSchool.DisplayName) e $($targetSchool.DisplayName)."
            Assert ($revenueUpdate.StatusCode -eq 404) "Falha no isolamento de receita entre $($sourceSchool.DisplayName) e $($targetSchool.DisplayName)."
            Assert ($expenseUpdate.StatusCode -eq 404) "Falha no isolamento de despesa entre $($sourceSchool.DisplayName) e $($targetSchool.DisplayName)."
            Assert ($schoolUserUpdate.StatusCode -eq 404) "Falha no isolamento de colaborador entre $($sourceSchool.DisplayName) e $($targetSchool.DisplayName)."
        }
    }

    $summary = foreach ($school in $schools) {
        [PSCustomObject]@{
            Escola = $school.DisplayName
            Colaboradores = 9
            Instrutores = 5
            Administrativos = 3
            Alunos = 10
            Equipamentos = 10
            Receitas = $school.ExpectedRevenue
            Despesas = $school.ExpectedExpense
        }
    }

    Write-Host ""
    Write-Host "Smoke multi-tenant concluído com sucesso."
    Write-Host ""
    $summary | Format-Table -AutoSize

    Write-Host ""
    Write-Host "Validações executadas:"
    Write-Host "- isolamento de alunos entre escolas"
    Write-Host "- isolamento de instrutores entre escolas"
    Write-Host "- isolamento de colaboradores entre escolas"
    Write-Host "- isolamento de equipamentos entre escolas"
    Write-Host "- isolamento de receitas e despesas entre escolas"
    Write-Host "- conferência dos totais financeiros por tenant"
}
finally {
    if (-not $KeepData) {
        try {
            dotnet run --project (Join-Path $root "tools\SmokeTenantCleaner\SmokeTenantCleaner.csproj") -- --execute --prefix $tenantPrefix | Out-Null
        } catch {
            Write-Warning "Não foi possível limpar automaticamente os tenants temporários com prefixo $tenantPrefix."
        }
    }

    foreach ($item in $started) {
        Stop-Process -Id $item.Pid -Force -ErrorAction SilentlyContinue
    }
}
