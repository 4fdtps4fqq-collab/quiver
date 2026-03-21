param(
    [string]$Configuration = "Release",
    [string]$OutputRoot = "",
    [switch]$SkipIisConfiguration
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = "C:\inetpub\quiver"
}

function Assert-LastExitCode {
    param([string]$Step)
    if ($LASTEXITCODE -ne 0) {
        throw "$Step falhou com código $LASTEXITCODE."
    }
}

function Ensure-Directory {
    param([string]$Path)
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Test-IsAdministrator {
    $currentIdentity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($currentIdentity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Reset-DirectoryContents {
    param([string]$Path)

    Ensure-Directory -Path $Path

    Get-ChildItem -Path $Path -Force -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction Stop
}

function Write-FrontendWebConfig {
    param([string]$Path)

    $content = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="SPA Routes" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/index.html" />
        </rule>
      </rules>
    </rewrite>
    <staticContent>
      <mimeMap fileExtension=".webmanifest" mimeType="application/manifest+json" />
    </staticContent>
  </system.webServer>
</configuration>
'@

    Set-Content -Path $Path -Value $content -Encoding UTF8
}

function Ensure-AppPool {
    param(
        [string]$Name
    )

    if (-not (Test-Path "IIS:\AppPools\$Name")) {
        New-WebAppPool -Name $Name | Out-Null
    }

    Set-ItemProperty "IIS:\AppPools\$Name" -Name managedRuntimeVersion -Value ""
    Set-ItemProperty "IIS:\AppPools\$Name" -Name managedPipelineMode -Value 0
}

function Ensure-Website {
    param(
        [string]$Name,
        [string]$PhysicalPath,
        [int]$Port,
        [string]$AppPool
    )

    if (-not (Test-Path "IIS:\Sites\$Name")) {
        New-Website -Name $Name -PhysicalPath $PhysicalPath -Port $Port -ApplicationPool $AppPool | Out-Null
        return
    }

    Set-ItemProperty "IIS:\Sites\$Name" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$Name" -Name applicationPool -Value $AppPool

    $httpBindings = Get-WebBinding -Name $Name -Protocol "http" -ErrorAction SilentlyContinue
    $hasExpectedBinding = $false

    foreach ($binding in @($httpBindings)) {
        if ($binding.bindingInformation -like "*:$($Port):*") {
            $hasExpectedBinding = $true
        }
    }

    if (-not $hasExpectedBinding) {
        New-WebBinding -Name $Name -Protocol "http" -Port $Port | Out-Null
    }
}

$projects = @(
    @{ Name = "identity";  Project = "src\backend\services\Identity\KiteFlow.Services.Identity.Api\KiteFlow.Services.Identity.Api.csproj"; Site = "Quiver.Identity"; Port = 7001 },
    @{ Name = "schools";   Project = "src\backend\services\Schools\KiteFlow.Services.Schools.Api\KiteFlow.Services.Schools.Api.csproj"; Site = "Quiver.Schools"; Port = 7002 },
    @{ Name = "academics"; Project = "src\backend\services\Academics\KiteFlow.Services.Academics.Api\KiteFlow.Services.Academics.Api.csproj"; Site = "Quiver.Academics"; Port = 7003 },
    @{ Name = "equipment"; Project = "src\backend\services\Equipment\KiteFlow.Services.Equipment.Api\KiteFlow.Services.Equipment.Api.csproj"; Site = "Quiver.Equipment"; Port = 7004 },
    @{ Name = "finance";   Project = "src\backend\services\Finance\KiteFlow.Services.Finance.Api\KiteFlow.Services.Finance.Api.csproj"; Site = "Quiver.Finance"; Port = 7005 },
    @{ Name = "reporting"; Project = "src\backend\services\Reporting\KiteFlow.Services.Reporting.Api\KiteFlow.Services.Reporting.Api.csproj"; Site = "Quiver.Reporting"; Port = 7006 },
    @{ Name = "gateway";   Project = "src\backend\gateway\KiteFlow.Gateway\KiteFlow.Gateway.csproj"; Site = "Quiver.Gateway"; Port = 7000 }
)

Ensure-Directory -Path $OutputRoot

foreach ($item in $projects) {
    $projectPath = Join-Path $root $item.Project
    $outputPath  = Join-Path $OutputRoot $item.Name

    Write-Host ""
    Write-Host "Publicando $($item.Name)..."
    Reset-DirectoryContents -Path $outputPath

    dotnet publish $projectPath -c $Configuration -o $outputPath --no-restore
    Assert-LastExitCode "Publish de $($item.Name)"
}

$frontendPath = Join-Path $root "src\frontend\app"
$frontendOutput = Join-Path $OutputRoot "frontend"

Write-Host ""
Write-Host "Buildando frontend..."

Push-Location $frontendPath

npm run build
Assert-LastExitCode "npm run build do frontend"

Pop-Location

Reset-DirectoryContents -Path $frontendOutput
Copy-Item -Path (Join-Path $frontendPath "dist\*") -Destination $frontendOutput -Recurse -Force
Write-FrontendWebConfig -Path (Join-Path $frontendOutput "web.config")

if (-not $SkipIisConfiguration) {
    Write-Host ""
    Write-Host "Configurando IIS local..."

    if (-not (Test-IsAdministrator)) {
        throw "A configuração do IIS precisa de PowerShell em modo Administrador. Os artefatos já foram publicados em '$OutputRoot'. Abra um PowerShell elevado e execute novamente o script."
    }

    Import-Module WebAdministration

    foreach ($item in $projects) {
        $outputPath = Join-Path $OutputRoot $item.Name
        $poolName = "$($item.Site).AppPool"

        Ensure-AppPool -Name $poolName
        Ensure-Website -Name $item.Site -PhysicalPath $outputPath -Port $item.Port -AppPool $poolName
    }

    $frontendSite = "Quiver.Frontend"
    $frontendPool = "$frontendSite.AppPool"
    Ensure-AppPool -Name $frontendPool
    Ensure-Website -Name $frontendSite -PhysicalPath $frontendOutput -Port 5174 -AppPool $frontendPool
}

Write-Host ""
Write-Host "Publicação concluída com sucesso."
Write-Host "Saída em: $OutputRoot"
Write-Host ""
Write-Host "URLs esperadas:"
Write-Host "  Frontend:  http://localhost:5174"
Write-Host "  Gateway:   http://localhost:7000"
Write-Host "  Identity:  http://localhost:7001"
Write-Host "  Schools:   http://localhost:7002"
Write-Host "  Academics: http://localhost:7003"
Write-Host "  Equipment: http://localhost:7004"
Write-Host "  Finance:   http://localhost:7005"
Write-Host "  Reporting: http://localhost:7006"
