param(
    [string]$AppUrl = "https://app.quivercloud.com.br",
    [string]$OutputRoot = "C:\inetpub\quiver",
    [string]$FrontendGatewayProxyUrl = "http://localhost:7000"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishScript = Join-Path $scriptRoot "publish-iis.ps1"

if (-not (Test-Path $publishScript)) {
    throw "Script base de publicação não encontrado em '$publishScript'."
}

Write-Host ""
Write-Host "Publicando ambiente de produção..."
Write-Host "  App URL:  $AppUrl"
Write-Host "  Saída:    $OutputRoot"
Write-Host "  Proxy:    $FrontendGatewayProxyUrl"

& powershell -NoProfile -ExecutionPolicy Bypass -File $publishScript `
    -OutputRoot $OutputRoot `
    -IdentityPublicLoginUrl "$AppUrl/login" `
    -GatewayAllowedOrigins @($AppUrl) `
    -FrontendGatewayProxyUrl $FrontendGatewayProxyUrl

if ($LASTEXITCODE -ne 0) {
    throw "A publicação de produção falhou com código $LASTEXITCODE."
}

Write-Host ""
Write-Host "Publicação de produção concluída."
Write-Host "Valide:"
Write-Host "  $AppUrl/login"
Write-Host "  $AppUrl/identity/api/v1/auth/login"
