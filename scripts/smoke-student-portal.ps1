param(
    [string]$Email = "ppetersen@gmail.com",
    [string]$Password = "1234"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$logDir = Join-Path $root "temp\student-portal-smoke"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$started = @()

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

try {
    Ensure-ServiceProcess -Name "identity" -Workdir (Join-Path $root "src\backend\services\Identity\KiteFlow.Services.Identity.Api") -Url "http://localhost:7001"
    Ensure-ServiceProcess -Name "schools" -Workdir (Join-Path $root "src\backend\services\Schools\KiteFlow.Services.Schools.Api") -Url "http://localhost:7002"
    Ensure-ServiceProcess -Name "academics" -Workdir (Join-Path $root "src\backend\services\Academics\KiteFlow.Services.Academics.Api") -Url "http://localhost:7003"
    Ensure-ServiceProcess -Name "gateway" -Workdir (Join-Path $root "src\backend\gateway\KiteFlow.Gateway") -Url "http://localhost:7000"

    $loginBody = @{
        email = $Email
        password = $Password
    } | ConvertTo-Json

    $login = Invoke-RestMethod `
        -Uri "http://localhost:7000/identity/api/v1/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body $loginBody

    $authHeaders = @{
        Authorization = "Bearer $($login.token)"
    }

    $me = Invoke-RestMethod -Uri "http://localhost:7000/identity/api/v1/auth/me" -Headers $authHeaders -Method Get

    $refreshBody = @{
        refreshToken = $login.refreshToken
        deviceName = "Smoke Student Portal"
    } | ConvertTo-Json

    $refresh = Invoke-RestMethod `
        -Uri "http://localhost:7000/identity/api/v1/auth/refresh" `
        -Method Post `
        -ContentType "application/json" `
        -Body $refreshBody

    $refreshedHeaders = @{
        Authorization = "Bearer $($refresh.token)"
    }

    $overview = Invoke-RestMethod -Uri "http://localhost:7000/academics/api/v1/student-portal/overview" -Headers $refreshedHeaders -Method Get
    $history = Invoke-RestMethod -Uri "http://localhost:7000/academics/api/v1/student-portal/history" -Headers $refreshedHeaders -Method Get
    $notifications = Invoke-RestMethod -Uri "http://localhost:7000/academics/api/v1/student-portal/notifications" -Headers $refreshedHeaders -Method Get
    $profile = Invoke-RestMethod -Uri "http://localhost:7000/academics/api/v1/student-portal/profile" -Headers $refreshedHeaders -Method Get
    $schoolProfileLoaded = $false
    try {
        Invoke-RestMethod -Uri "http://localhost:7000/schools/api/v1/schools/me" -Headers $refreshedHeaders -Method Get | Out-Null
        $schoolProfileLoaded = $true
    } catch {
        $schoolProfileLoaded = $false
    }

    [PSCustomObject]@{
        LoginEmail = $me.email
        Role = $me.role
        MustChangePassword = $me.mustChangePassword
        RefreshTokenRotated = [bool]$refresh.refreshToken
        StudentName = $overview.student.fullName
        ActiveEnrollments = $overview.summary.activeEnrollments
        UpcomingLessons = $overview.summary.totalUpcomingLessons
        ProfileCompleteness = $overview.summary.profileCompleteness
        HistoryItems = $history.items.Count
        Notifications = $notifications.items.Count
        UnreadNotifications = $notifications.unreadCount
        SchoolProfileLoaded = $schoolProfileLoaded
    } | Format-List
}
finally {
    foreach ($item in $started) {
        Stop-Process -Id $item.Pid -Force -ErrorAction SilentlyContinue
    }
}
