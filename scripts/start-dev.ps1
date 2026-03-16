param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$logDir = Join-Path $root "temp\dev-run"
$stateFile = Join-Path $logDir "processes.json"

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Assert-LastExitCode {
    param(
        [string]$Step
    )

    if ($LASTEXITCODE -ne 0) {
        throw "$Step falhou com código $LASTEXITCODE."
    }
}

function Wait-ForService {
    param(
        [string]$Name,
        [string]$Url,
        [int]$Pid,
        [string]$StdErrPath,
        [int]$TimeoutSeconds = 45
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)

    while ((Get-Date) -lt $deadline) {
        $process = Get-Process -Id $Pid -ErrorAction SilentlyContinue
        if (-not $process) {
            $errorPreview = ""
            if (Test-Path $StdErrPath) {
                $errorPreview = (Get-Content -Path $StdErrPath -Tail 20 -ErrorAction SilentlyContinue) -join [Environment]::NewLine
            }

            throw "O serviço $Name encerrou antes de ficar pronto.`n$errorPreview"
        }

        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 5 -UseBasicParsing
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return
            }
        } catch {
            Start-Sleep -Milliseconds 700
        }
    }

    throw "O serviço $Name não respondeu em $TimeoutSeconds segundos em $Url."
}

Write-Host "Subindo infraestrutura local..."
docker compose -f (Join-Path $root "infra\docker-compose.yml") up -d | Out-Null
Assert-LastExitCode "A infraestrutura local"

if (-not $SkipBuild) {
    Write-Host "Compilando backend..."
    $projectsToBuild = @(
        "src\backend\building-blocks\KiteFlow.BuildingBlocks\KiteFlow.BuildingBlocks.csproj",
        "src\backend\services\Identity\KiteFlow.Services.Identity.Api\KiteFlow.Services.Identity.Api.csproj",
        "src\backend\services\Schools\KiteFlow.Services.Schools.Api\KiteFlow.Services.Schools.Api.csproj",
        "src\backend\services\Academics\KiteFlow.Services.Academics.Api\KiteFlow.Services.Academics.Api.csproj",
        "src\backend\services\Equipment\KiteFlow.Services.Equipment.Api\KiteFlow.Services.Equipment.Api.csproj",
        "src\backend\services\Finance\KiteFlow.Services.Finance.Api\KiteFlow.Services.Finance.Api.csproj",
        "src\backend\services\Reporting\KiteFlow.Services.Reporting.Api\KiteFlow.Services.Reporting.Api.csproj",
        "src\backend\gateway\KiteFlow.Gateway\KiteFlow.Gateway.csproj"
    )

    foreach ($project in $projectsToBuild) {
        dotnet build (Join-Path $root $project) --no-restore | Out-Null
        Assert-LastExitCode "A compilação de $project"
    }
}

$services = @(
    @{ Name = "identity"; Url = "http://localhost:7001"; Workdir = Join-Path $root "src\backend\services\Identity\KiteFlow.Services.Identity.Api"; Command = "dotnet"; Args = @("run", "--no-build", "--urls", "http://localhost:7001") },
    @{ Name = "schools"; Url = "http://localhost:7002"; Workdir = Join-Path $root "src\backend\services\Schools\KiteFlow.Services.Schools.Api"; Command = "dotnet"; Args = @("run", "--no-build", "--urls", "http://localhost:7002") },
    @{ Name = "academics"; Url = "http://localhost:7003"; Workdir = Join-Path $root "src\backend\services\Academics\KiteFlow.Services.Academics.Api"; Command = "dotnet"; Args = @("run", "--no-build", "--urls", "http://localhost:7003") },
    @{ Name = "equipment"; Url = "http://localhost:7004"; Workdir = Join-Path $root "src\backend\services\Equipment\KiteFlow.Services.Equipment.Api"; Command = "dotnet"; Args = @("run", "--no-build", "--urls", "http://localhost:7004") },
    @{ Name = "finance"; Url = "http://localhost:7005"; Workdir = Join-Path $root "src\backend\services\Finance\KiteFlow.Services.Finance.Api"; Command = "dotnet"; Args = @("run", "--no-build", "--urls", "http://localhost:7005") },
    @{ Name = "reporting"; Url = "http://localhost:7006"; Workdir = Join-Path $root "src\backend\services\Reporting\KiteFlow.Services.Reporting.Api"; Command = "dotnet"; Args = @("run", "--no-build", "--urls", "http://localhost:7006") },
    @{ Name = "gateway"; Url = "http://localhost:7000"; Workdir = Join-Path $root "src\backend\gateway\KiteFlow.Gateway"; Command = "dotnet"; Args = @("run", "--no-build", "--urls", "http://localhost:7000") },
    @{ Name = "frontend"; Url = "http://localhost:5174"; Workdir = Join-Path $root "src\frontend\app"; Command = "cmd"; Args = @("/c", "npm", "run", "dev", "--", "--host", "0.0.0.0", "--port", "5174") }
)

$running = @()
foreach ($service in $services) {
    $stdout = Join-Path $logDir "$($service.Name).out.log"
    $stderr = Join-Path $logDir "$($service.Name).err.log"

    $process = Start-Process `
        -FilePath $service.Command `
        -ArgumentList $service.Args `
        -WorkingDirectory $service.Workdir `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        -PassThru

    $running += [PSCustomObject]@{
        name = $service.Name
        pid = $process.Id
        url = $service.Url
        workdir = $service.Workdir
        stderr = $stderr
    }
}

$running | ConvertTo-Json | Set-Content -Path $stateFile

foreach ($service in $running) {
    Wait-ForService -Name $service.name -Url $service.url -Pid $service.pid -StdErrPath $service.stderr
}

Write-Host ""
Write-Host "Ambiente iniciado."
Write-Host "Frontend:  http://localhost:5174"
Write-Host "Gateway:   http://localhost:7000"
Write-Host "Swagger:"
Write-Host "  http://localhost:7001/swagger"
Write-Host "  http://localhost:7002/swagger"
Write-Host "  http://localhost:7003/swagger"
Write-Host "  http://localhost:7004/swagger"
Write-Host "  http://localhost:7005/swagger"
Write-Host "  http://localhost:7006/swagger"
Write-Host ""
Write-Host "Logs: $logDir"
Write-Host "Para parar tudo: .\\scripts\\stop-dev.ps1"
