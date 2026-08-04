param(
    [switch]$Local
)

$ErrorActionPreference = 'Stop'

$projectPath = 'D:\GitHub\hello-backend'
$encorePath = 'D:\app\Encore\bin\encore.exe'
$cloudBaseUrl = 'https://staging-hello-8shi.encr.app'
$cloudDashboardUrl = "$cloudBaseUrl/admin/dashboard"

try {
    if (-not $Local) {
        Write-Host 'Checking the Encore cloud backend...' -ForegroundColor Cyan
        $overview = Invoke-RestMethod -Uri "$cloudBaseUrl/api/v1/admin/overview" -TimeoutSec 20
        Write-Host 'Cloud backend is online.' -ForegroundColor Green
        Write-Host "Players: $($overview.totalPlayers)  Online now: $($overview.onlineUsers)  Sessions: $($overview.completedSessions)"
        Write-Host 'Opening the LTC dashboard in your browser.' -ForegroundColor Cyan
        Start-Process $cloudDashboardUrl
        Write-Host ''
        Write-Host 'Unity already connects to this cloud backend. You may close this window.' -ForegroundColor Yellow
        Read-Host 'Press Enter to close' | Out-Null
        exit 0
    }

    if (-not (Test-Path -LiteralPath $encorePath)) {
        throw "Encore was not found at: $encorePath"
    }
    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw "Backend project was not found at: $projectPath"
    }
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Local PostgreSQL development requires Docker Desktop. Use StartBackend.ps1 without -Local to use the cloud backend.'
    }
    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker Desktop is installed but not running. Start Docker Desktop and try again.'
    }

    $env:ENCORE_INSTALL = 'D:\app\Encore'
    $env:Path = "D:\app\Encore\bin;$env:Path"
    Set-Location -LiteralPath $projectPath
    Write-Host 'Starting the local LTC backend with Docker PostgreSQL...' -ForegroundColor Cyan
    & $encorePath run
}
catch {
    Write-Host ''
    Write-Host "Startup failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ''
Read-Host 'Press Enter to close' | Out-Null
