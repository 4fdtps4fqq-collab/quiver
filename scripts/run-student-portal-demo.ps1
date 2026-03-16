param(
    [int]$AutoStopAfterSeconds = 0
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$stateDir = Join-Path $root "temp\student-portal-demo"
$stateFile = Join-Path $stateDir "session.json"
$credentialsFile = Join-Path $stateDir "credentials.txt"
$errorFile = Join-Path $stateDir "last-error.txt"
$statusFile = Join-Path $stateDir "last-status.txt"

New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
Remove-Item -Path (Join-Path $stateDir "processes.json") -Force -ErrorAction SilentlyContinue
Remove-Item -Path $errorFile -Force -ErrorAction SilentlyContinue
Remove-Item -Path $statusFile -Force -ErrorAction SilentlyContinue

function Test-HttpReady {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -UseBasicParsing $Url -TimeoutSec 2
        return $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
    } catch {
        return $false
    }
}

function Wait-HttpReady {
    param(
        [string]$Name,
        [string]$Url,
        [int]$Attempts = 120
    )

    Write-Host "Aguardando $Name em $Url ..."
    for ($index = 0; $index -lt $Attempts; $index++) {
        if (Test-HttpReady $Url) {
            Write-Host "  $Name respondeu com sucesso."
            return
        }

        Start-Sleep -Milliseconds 500
    }

    throw "O servico $Name nao respondeu em $Url."
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

function Remove-DemoTenants {
    param([string[]]$Prefixes)

    foreach ($prefix in $Prefixes) {
        dotnet run --project (Join-Path $root "tools\SmokeTenantCleaner\SmokeTenantCleaner.csproj") -- `
            --execute `
            --host localhost `
            --port 5432 `
            --username postgres `
            --password postgres `
            --prefix $prefix | Out-Null
    }
}

$startedEnvironment = $false
$seededSlug = $null

try {
    "Iniciando runner da demo do portal do aluno em $(Get-Date -Format o)" | Set-Content -Path $statusFile -Encoding utf8
    Write-Host "Limpando demos antigas do portal do aluno..."
    Remove-DemoTenants -Prefixes @("demo-student-portal-")

    $requiredServices = @(
        @{ Name = "identity"; Url = "http://localhost:7001/" },
        @{ Name = "schools"; Url = "http://localhost:7002/" },
        @{ Name = "academics"; Url = "http://localhost:7003/" },
        @{ Name = "gateway"; Url = "http://localhost:7000/" },
        @{ Name = "frontend"; Url = "http://localhost:5174/" }
    )

    $allReady = $true
    foreach ($service in $requiredServices) {
        if (-not (Test-HttpReady $service.Url)) {
            $allReady = $false
            break
        }
    }

    if (-not $allReady) {
        Write-Host "Subindo ambiente de desenvolvimento..."
        powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "scripts\start-dev.ps1") -SkipBuild
        $startedEnvironment = $true
    } else {
        Write-Host "Ambiente ja estava ativo. Vou apenas preparar a demo do portal."
    }

    foreach ($service in $requiredServices) {
        Wait-HttpReady -Name $service.Name -Url $service.Url
    }

    Write-Host "Criando tenant de demonstracao do portal do aluno..."

    $suffix = Get-Date -Format "yyyyMMddHHmmss"
    $schoolId = [guid]::NewGuid()
    $ownerId = [guid]::NewGuid()
    $slug = "demo-student-portal-$suffix"
    $seededSlug = $slug
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
        "Tenant:     $slug",
        "",
        "Ao entrar com esse usuario, o sistema redireciona automaticamente para /student.",
        "Voce vai encontrar:",
        "- progresso do treinamento",
        "- matricula ativa",
        "- uma aula futura ja agendada",
        "- acoes para remarcar e cancelar"
    )

    $summary | Set-Content -Path $credentialsFile -Encoding utf8

    $session = [pscustomobject]@{
        slug = $slug
        startedEnvironment = $startedEnvironment
    }
    $session | ConvertTo-Json | Set-Content -Path $stateFile

    Write-Host ""
    foreach ($line in $summary) {
        Write-Host $line
    }
    Write-Host ""
    Write-Host "As credenciais tambem foram salvas em: $credentialsFile"
    Write-Host ""
    "Demo pronta. Credenciais geradas em $credentialsFile" | Set-Content -Path $statusFile -Encoding utf8

    if ($AutoStopAfterSeconds -gt 0) {
        Write-Host "Encerrando automaticamente em $AutoStopAfterSeconds segundos..."
        Start-Sleep -Seconds $AutoStopAfterSeconds
    } else {
        Read-Host "Pressione Enter quando terminar de testar para encerrar a demo"
    }
}
catch {
    $message = @(
        "A demo do portal do aluno falhou."
        "Data: $(Get-Date -Format o)"
        "Erro: $($_.Exception.Message)"
        ""
        "Se o ambiente ja estiver subido, teste manualmente:"
        "1. powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-dev.ps1 -SkipBuild"
        "2. execute novamente este runner"
    ) -join [Environment]::NewLine

    $message | Set-Content -Path $errorFile -Encoding utf8
    $message | Set-Content -Path $statusFile -Encoding utf8
    Write-Host ""
    Write-Host $message -ForegroundColor Red
    Write-Host ""
    throw
}
finally {
    if (Test-Path $stateFile) {
        $session = Get-Content $stateFile | ConvertFrom-Json
        if ($session.slug) {
            Remove-DemoTenants -Prefixes @($session.slug)
        }

        if ($session.startedEnvironment) {
            powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "scripts\stop-dev.ps1") | Out-Null
        }

        Remove-Item $stateFile -Force
    }
}
