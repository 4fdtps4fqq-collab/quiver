param()

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$stateDir = Join-Path $root "temp\student-portal-demo"
$credentialsFile = Join-Path $stateDir "credentials.txt"
$statusFile = Join-Path $stateDir "last-status.txt"
$errorFile = Join-Path $stateDir "last-error.txt"
$slugFile = Join-Path $stateDir "demo-slug.txt"

New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
Remove-Item -Path $credentialsFile, $errorFile -Force -ErrorAction SilentlyContinue

function Set-Status {
    param([string]$Message)

    $Message | Set-Content -Path $statusFile -Encoding utf8
    Write-Host $Message
}

function Test-HttpReady {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -UseBasicParsing $Url -TimeoutSec 2
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
    } catch {
        return $false
    }
}

function Ensure-ServiceReady {
    param(
        [string]$Name,
        [string]$Url
    )

    if (-not (Test-HttpReady $Url)) {
        throw "O servico $Name nao respondeu em $Url. Suba primeiro o ambiente com .\scripts\start-dev.ps1 -SkipBuild."
    }
}

function Invoke-Json {
    param(
        [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE")]
        [string]$Method,
        [string]$Url,
        [object]$Body,
        [hashtable]$Headers
    )

    $params = @{
        Method = $Method
        Uri = $Url
        ContentType = "application/json"
        ErrorAction = "Stop"
    }

    if ($Headers) {
        $params.Headers = $Headers
    }

    if ($PSBoundParameters.ContainsKey("Body") -and $null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 8)
    }

    Invoke-RestMethod @params
}

try {
    Set-Status "Validando servicos necessarios para a demo do portal do aluno..."

    $requiredServices = @(
        @{ Name = "identity"; Url = "http://localhost:7001/" },
        @{ Name = "schools"; Url = "http://localhost:7002/" },
        @{ Name = "academics"; Url = "http://localhost:7003/" },
        @{ Name = "frontend"; Url = "http://localhost:5174/" }
    )

    foreach ($service in $requiredServices) {
        Ensure-ServiceReady -Name $service.Name -Url $service.Url
    }

    Set-Status "Limpando demos antigas do portal do aluno..."
    dotnet run --no-build --project (Join-Path $root "tools\SmokeTenantCleaner\SmokeTenantCleaner.csproj") -- `
        --execute `
        --host localhost `
        --port 5432 `
        --username postgres `
        --password postgres `
        --prefix demo-student-portal- | Out-Null

    Set-Status "Criando tenant e credenciais da demo do portal do aluno..."

    $suffix = Get-Date -Format "yyyyMMddHHmmss"
    $schoolId = [guid]::NewGuid()
    $ownerId = [guid]::NewGuid()
    $slug = "demo-student-portal-$suffix"
    $ownerEmail = "owner.demo.$suffix@kiteflow.local"
    $studentEmail = "aluno.demo.$suffix@kiteflow.local"
    $ownerPassword = "Owner123!"
    $studentPassword = "Aluno123!"

    Invoke-Json -Method POST -Url "http://localhost:7001/api/v1/auth/bootstrap-user" -Body @{
        userId = $ownerId
        schoolId = $schoolId
        email = $ownerEmail
        password = $ownerPassword
        role = 2
    } | Out-Null

    Invoke-Json -Method POST -Url "http://localhost:7002/api/v1/onboarding/register-school" -Body @{
        schoolId = $schoolId
        ownerIdentityUserId = $ownerId
        legalName = "KiteFlow Demo Portal LTDA"
        displayName = "Portal do Aluno Demo"
        ownerFullName = "Owner Demo Portal"
        ownerPhone = "11999990000"
        slug = $slug
        timezone = "America/Sao_Paulo"
        currencyCode = "BRL"
        bookingLeadTimeMinutes = 60
        cancellationWindowHours = 24
    } | Out-Null

    $ownerLogin = Invoke-Json -Method POST -Url "http://localhost:7001/api/v1/auth/login" -Body @{
        email = $ownerEmail
        password = $ownerPassword
    }
    $ownerHeaders = @{ Authorization = "Bearer $($ownerLogin.token)" }

    Invoke-Json -Method POST -Url "http://localhost:7001/api/v1/auth/bootstrap-user" -Body @{
        schoolId = $schoolId
        email = $studentEmail
        password = $studentPassword
        role = 4
    } | Out-Null

    $student = Invoke-Json -Method POST -Url "http://localhost:7003/api/v1/students" -Headers $ownerHeaders -Body @{
        fullName = "Aluno Demo Portal"
        email = $studentEmail
        phone = "11988887777"
        medicalNotes = "Treino com foco em waterstart e consistencia de borda."
        emergencyContactName = "Contato Demo"
        emergencyContactPhone = "11977776666"
        firstStandUpAtUtc = (Get-Date).ToUniversalTime().AddDays(-12).ToString("o")
    }

    $instructor = Invoke-Json -Method POST -Url "http://localhost:7003/api/v1/instructors" -Headers $ownerHeaders -Body @{
        fullName = "Instrutor Demo Portal"
        email = "instrutor.demo.$suffix@kiteflow.local"
        specialties = "Waterstart, transicao e navegacao inicial"
    }

    $course = Invoke-Json -Method POST -Url "http://localhost:7003/api/v1/courses" -Headers $ownerHeaders -Body @{
        name = "Discovery Session Demo"
        level = 1
        totalLessons = 6
        price = 1800
    }

    $enrollment = Invoke-Json -Method POST -Url "http://localhost:7003/api/v1/enrollments" -Headers $ownerHeaders -Body @{
        studentId = $student.studentId
        courseId = $course.courseId
    }

    $pastLessonAt = (Get-Date).ToUniversalTime().AddDays(-4)
    Invoke-Json -Method POST -Url "http://localhost:7003/api/v1/lessons" -Headers $ownerHeaders -Body @{
        studentId = $student.studentId
        instructorId = $instructor.instructorId
        kind = 2
        status = 3
        enrollmentId = $enrollment.enrollmentId
        startAtUtc = $pastLessonAt.ToString("o")
        durationMinutes = 90
        notes = "Aula realizada para demonstrar progresso no portal."
    } | Out-Null

    $studentLogin = Invoke-Json -Method POST -Url "http://localhost:7001/api/v1/auth/login" -Body @{
        email = $studentEmail
        password = $studentPassword
    }
    $studentHeaders = @{ Authorization = "Bearer $($studentLogin.token)" }

    $futureLessonAt = (Get-Date).ToUniversalTime().AddDays(2)
    Invoke-Json -Method POST -Url "http://localhost:7003/api/v1/student-portal/lessons/course" -Headers $studentHeaders -Body @{
        enrollmentId = $enrollment.enrollmentId
        instructorId = $instructor.instructorId
        startAtUtc = $futureLessonAt.ToString("o")
        durationMinutes = 90
        notes = "Aula futura disponivel para teste de remarcacao e cancelamento."
    } | Out-Null

    $summary = @(
        "Portal do aluno pronto para teste.",
        "URL:        http://localhost:5174/login",
        "Login:      $studentEmail",
        "Senha:      $studentPassword",
        "Tenant:     $slug"
    )

    $summary | Set-Content -Path $credentialsFile -Encoding utf8
    $slug | Set-Content -Path $slugFile -Encoding utf8
    Set-Status "Credenciais da demo geradas com sucesso em $credentialsFile"

    Write-Host ""
    foreach ($line in $summary) {
        Write-Host $line
    }
    Write-Host ""
    Write-Host "Arquivo gerado em: $credentialsFile"
}
catch {
    $message = @(
        "Falha ao gerar a demo do portal do aluno."
        "Data: $(Get-Date -Format o)"
        "Erro: $($_.Exception.Message)"
    ) -join [Environment]::NewLine

    $message | Set-Content -Path $errorFile -Encoding utf8
    $message | Set-Content -Path $statusFile -Encoding utf8
    Write-Host ""
    Write-Host $message -ForegroundColor Red
    Write-Host ""
    throw
}
