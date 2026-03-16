param(
    [string]$Password = "Smoke123!"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$logDir = Join-Path $root "temp\platform-core-smoke"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$started = @()
$stamp = Get-Date -Format "yyyyMMddHHmmss"
$slug = "smoke-core-$stamp"
$email = "owner.$stamp@kiteflow.local"

function Test-HttpAvailable {
    param(
        [string]$Url
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
    } catch {
        return $false
    }
}

function Get-PortFromUrl {
    param(
        [string]$Url
    )

    return ([System.Uri]$Url).Port
}

function Test-PortListening {
    param(
        [int]$Port
    )

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

function Ensure-ServiceProcess {
    param(
        [string]$Name,
        [string]$Workdir,
        [string]$Url
    )

    if (Test-HttpAvailable -Url "$Url/swagger") {
        Write-Host "Reaproveitando servico ativo: $Name"
        return
    }

    $port = Get-PortFromUrl -Url $Url
    if (Test-PortListening -Port $port) {
        Write-Host "A porta $port ja esta em uso. Aguardando o servico responder: $Name"
        Wait-ForHttp -Url "$Url/swagger" -TimeoutSeconds 20
        return
    }

    Start-ServiceProcess -Name $Name -Workdir $Workdir -Url $Url
    Wait-ForHttp -Url "$Url/swagger"
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

function Invoke-Json {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body,
        [hashtable]$Headers
    )

    $json = if ($null -ne $Body) { $Body | ConvertTo-Json -Depth 8 } else { $null }

    if ($Headers) {
        return Invoke-RestMethod -Uri $Url -Method $Method -ContentType "application/json" -Headers $Headers -Body $json
    }

    return Invoke-RestMethod -Uri $Url -Method $Method -ContentType "application/json" -Body $json
}

try {
    Ensure-ServiceProcess -Name "identity" -Workdir (Join-Path $root "src\backend\services\Identity\KiteFlow.Services.Identity.Api") -Url "http://localhost:7001"
    Ensure-ServiceProcess -Name "schools" -Workdir (Join-Path $root "src\backend\services\Schools\KiteFlow.Services.Schools.Api") -Url "http://localhost:7002"
    Ensure-ServiceProcess -Name "academics" -Workdir (Join-Path $root "src\backend\services\Academics\KiteFlow.Services.Academics.Api") -Url "http://localhost:7003"
    Ensure-ServiceProcess -Name "equipment" -Workdir (Join-Path $root "src\backend\services\Equipment\KiteFlow.Services.Equipment.Api") -Url "http://localhost:7004"
    Ensure-ServiceProcess -Name "finance" -Workdir (Join-Path $root "src\backend\services\Finance\KiteFlow.Services.Finance.Api") -Url "http://localhost:7005"
    Ensure-ServiceProcess -Name "reporting" -Workdir (Join-Path $root "src\backend\services\Reporting\KiteFlow.Services.Reporting.Api") -Url "http://localhost:7006"
    Ensure-ServiceProcess -Name "gateway" -Workdir (Join-Path $root "src\backend\gateway\KiteFlow.Gateway") -Url "http://localhost:7000"

    $registration = Invoke-Json -Method "POST" -Url "http://localhost:7000/api/v1/onboarding/register-owner" -Body @{
        legalName = "Smoke Core Escola $stamp"
        displayName = "Smoke Core $stamp"
        ownerFullName = "Owner Smoke $stamp"
        email = $email
        password = $Password
        ownerPhone = "11999990000"
        slug = $slug
        timezone = "America/Sao_Paulo"
        currencyCode = "BRL"
        themePrimary = "#0B3C5D"
        themeAccent = "#2ED4A7"
        bookingLeadTimeMinutes = 60
        cancellationWindowHours = 24
    } -Headers @{}

    $token = $registration.session.token
    $headers = @{ Authorization = "Bearer $token" }

    $student = Invoke-Json -Method "POST" -Url "http://localhost:7000/academics/api/v1/students" -Body @{
        fullName = "Aluno Smoke $stamp"
        email = "student.$stamp@kiteflow.local"
        phone = "11999990001"
        birthDate = "1998-05-10"
        medicalNotes = "Sem restricoes"
        emergencyContactName = "Contato Smoke"
        emergencyContactPhone = "11999990002"
        firstStandUpAtUtc = (Get-Date).ToUniversalTime().AddDays(-3).ToString("O")
        identityUserId = $null
    } -Headers $headers

    $instructor = Invoke-Json -Method "POST" -Url "http://localhost:7000/academics/api/v1/instructors" -Body @{
        fullName = "Instrutor Smoke $stamp"
        email = "instructor.$stamp@kiteflow.local"
        phone = "11999990003"
        specialties = "Freestyle e progressao"
        identityUserId = $null
    } -Headers $headers

    $course = Invoke-Json -Method "POST" -Url "http://localhost:7000/academics/api/v1/courses" -Body @{
        name = "Curso Smoke $stamp"
        level = 2
        totalHours = 6
        price = 1800
    } -Headers $headers

    $courses = Invoke-RestMethod -Uri "http://localhost:7000/academics/api/v1/courses" -Headers $headers -Method Get
    $courseId = ($courses | Where-Object { $_.name -eq "Curso Smoke $stamp" } | Select-Object -First 1).id

    $enrollment = Invoke-Json -Method "POST" -Url "http://localhost:7000/academics/api/v1/enrollments" -Body @{
        studentId = $student.studentId
        courseId = $courseId
        startedAtUtc = (Get-Date).ToUniversalTime().AddDays(-2).ToString("O")
    } -Headers $headers

    $lesson = Invoke-Json -Method "POST" -Url "http://localhost:7000/academics/api/v1/lessons" -Body @{
        studentId = $student.studentId
        instructorId = $instructor.instructorId
        kind = 2
        status = 3
        enrollmentId = $enrollment.enrollmentId
        singleLessonPrice = $null
        startAtUtc = (Get-Date).ToUniversalTime().AddHours(-2).ToString("O")
        durationMinutes = 90
        notes = "Smoke course lesson"
    } -Headers $headers

    $storage = Invoke-Json -Method "POST" -Url "http://localhost:7000/equipment/api/v1/storages" -Body @{
        name = "Deposito Smoke $stamp"
        locationNote = "Base principal"
    } -Headers $headers

    $equipment = Invoke-Json -Method "POST" -Url "http://localhost:7000/equipment/api/v1/equipment-items" -Body @{
        storageId = $storage.storageId
        name = "Kite Smoke $stamp"
        type = 1
        tagCode = "K-$stamp"
        brand = "North"
        model = "Reach"
        sizeLabel = "9m"
        currentCondition = 2
    } -Headers $headers

    Invoke-Json -Method "POST" -Url "http://localhost:7000/equipment/api/v1/lesson-equipment/$($lesson.lessonId)/checkout" -Body @{
        notesBefore = "Checkout smoke"
        items = @(
            @{
                equipmentId = $equipment.equipmentId
                conditionBefore = 2
                notesBefore = "Bom estado"
            }
        )
    } -Headers $headers | Out-Null

    Invoke-Json -Method "POST" -Url "http://localhost:7000/equipment/api/v1/lesson-equipment/$($lesson.lessonId)/checkin" -Body @{
        notesAfter = "Checkin smoke"
        items = @(
            @{
                equipmentId = $equipment.equipmentId
                conditionAfter = 3
                notesAfter = "Atencao apos uso"
            }
        )
    } -Headers $headers | Out-Null

    Invoke-Json -Method "POST" -Url "http://localhost:7000/equipment/api/v1/maintenance/rules" -Body @{
        equipmentType = 1
        serviceEveryMinutes = 600
        serviceEveryDays = 30
        isActive = $true
    } -Headers $headers | Out-Null

    Invoke-Json -Method "POST" -Url "http://localhost:7000/equipment/api/v1/maintenance/records" -Body @{
        equipmentId = $equipment.equipmentId
        serviceDateUtc = (Get-Date).ToUniversalTime().ToString("O")
        description = "Revisao smoke"
        cost = 150
        performedBy = "Oficina smoke"
        conditionAfterService = 2
    } -Headers $headers | Out-Null

    Invoke-Json -Method "POST" -Url "http://localhost:7000/finance/api/v1/finance/revenues" -Body @{
        sourceType = 3
        sourceId = $null
        category = "Receita de teste"
        amount = 1800
        recognizedAtUtc = (Get-Date).ToUniversalTime().ToString("O")
        description = "Receita smoke"
    } -Headers $headers | Out-Null

    Invoke-Json -Method "POST" -Url "http://localhost:7000/finance/api/v1/finance/expenses" -Body @{
        category = 2
        amount = 150
        occurredAtUtc = (Get-Date).ToUniversalTime().ToString("O")
        description = "Despesa smoke"
        vendor = "Fornecedor smoke"
    } -Headers $headers | Out-Null

    $dashboard = Invoke-RestMethod -Uri "http://localhost:7000/reporting/api/v1/reports/dashboard" -Headers $headers -Method Get
    $enrollments = Invoke-RestMethod -Uri "http://localhost:7000/academics/api/v1/enrollments" -Headers $headers -Method Get
    $equipmentHistory = Invoke-RestMethod -Uri "http://localhost:7000/equipment/api/v1/equipment-items/$($equipment.equipmentId)/history" -Headers $headers -Method Get
    $revenues = Invoke-RestMethod -Uri "http://localhost:7000/finance/api/v1/finance/revenues" -Headers $headers -Method Get
    $expenses = Invoke-RestMethod -Uri "http://localhost:7000/finance/api/v1/finance/expenses" -Headers $headers -Method Get

    $createdEnrollment = $enrollments | Where-Object { $_.id -eq $enrollment.enrollmentId } | Select-Object -First 1

    [PSCustomObject]@{
        SchoolSlug = $slug
        StudentCreated = [bool]$student.studentId
        InstructorCreated = [bool]$instructor.instructorId
        CourseCreated = [bool]$courseId
        EnrollmentRemainingMinutes = $createdEnrollment.remainingMinutes
        LessonCreated = [bool]$lesson.lessonId
        EquipmentUsageLogs = $equipmentHistory.usage.Count
        MaintenanceRecords = $equipmentHistory.maintenance.Count
        RevenueEntries = $revenues.Count
        ExpenseEntries = $expenses.Count
        DashboardStudents = $dashboard.academics.students
        DashboardEquipment = $dashboard.equipment.equipment
        DashboardGrossMargin = $dashboard.finance.grossMargin
    } | Format-List
}
finally {
    try {
        dotnet run --project (Join-Path $root "tools\SmokeTenantCleaner\SmokeTenantCleaner.csproj") -- --execute --prefix $slug | Out-Null
    } catch {
        Write-Warning "Nao foi possivel limpar automaticamente o tenant temporario $slug."
    }

    foreach ($item in $started) {
        Stop-Process -Id $item.Pid -Force -ErrorAction SilentlyContinue
    }
}
