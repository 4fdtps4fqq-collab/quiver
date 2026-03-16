$ErrorActionPreference = "SilentlyContinue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$stateFile = Join-Path $root "temp\dev-run\processes.json"

if (Test-Path $stateFile) {
    $items = Get-Content $stateFile | ConvertFrom-Json
    foreach ($item in $items) {
        Stop-Process -Id $item.pid -Force
    }

    Remove-Item $stateFile -Force
}

docker compose -f (Join-Path $root "infra\docker-compose.yml") stop | Out-Null

Write-Host "Ambiente local parado."
