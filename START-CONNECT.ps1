# ============================================================
# Connect Launcher (openbot-desktop.exe)
# ============================================================
param(
  [switch]$Dev,
  [switch]$Build,
  [switch]$Schnell
)

$ErrorActionPreference = "Continue"
$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }
$desktop = Join-Path $root "desktop"

$env:CONNECT_PRODUCT_WINDOW = "1"
$env:CONNECT_ROOT = $root
$env:OPENBOT_FORCE_START = "1"

function Test-Port([int]$port) {
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $iar = $tcp.BeginConnect("127.0.0.1", $port, $null, $null)
        $wait = $iar.AsyncWaitHandle.WaitOne(800, $false)
        if ($wait) {
            $tcp.EndConnect($iar)
            $tcp.Close()
            return $true
        }
        $tcp.Close()
        return $false
    } catch {
        return $false
    }
}

function Start-UiIfNeeded {
    $uiOk = (Test-Port 3010) -and (Test-Port 3001)
    if ($uiOk) {
        Write-Host "[Connect] Stack laeuft bereits (Port 3010 + 3001)." -ForegroundColor Green
        return
    }
    Write-Host "[Connect] Starte Backend und App in WSL..." -ForegroundColor Yellow
    
    # 1. PostgreSQL in WSL
    if (-not (Test-Port 5432)) {
        wsl -d Ubuntu bash -c "service postgresql status >/dev/null 2>&1 || service postgresql start"
    }

    # 2. Server (3001) in WSL
    if (-not (Test-Port 3001)) {
        wsl -d Ubuntu bash -c "cd '/mnt/c/Users/Kunc GmbH/Desktop/OpenBot/server' && nohup bun --env-file=../.env src/production-entry.ts > ../.logs/server.log 2>&1 &"
    }

    # 3. App UI (3010) in WSL
    if (-not (Test-Port 3010)) {
        wsl -d Ubuntu bash -c "cd '/mnt/c/Users/Kunc GmbH/Desktop/OpenBot/app' && nohup bun run dev -- --host 0.0.0.0 --port 3010 --strictPort > ../.logs/app.log 2>&1 &"
    }

    # Wait for port 3010
    $retries = 15
    while (-not (Test-Port 3010) -and $retries -gt 0) {
        Start-Sleep -Seconds 1
        $retries--
    }
}

Write-Host ""
Write-Host "[Connect] Starte Connect Desktop (openbot-desktop.exe)..." -ForegroundColor Cyan

$release = Join-Path $desktop "src-tauri\target\release\openbot-desktop.exe"
$debug = Join-Path $desktop "src-tauri\target\debug\openbot-desktop.exe"

$exe = $null
if (Test-Path $release) { $exe = $release }
elseif (Test-Path $debug) { $exe = $debug }

if ($exe) {
    if (-not $Schnell) { Start-UiIfNeeded }
    Write-Host "[Connect] Starte: $exe" -ForegroundColor Green
    Start-Process -FilePath $exe -WorkingDirectory $desktop
    exit 0
}

Write-Host "[Connect] FEHLER: openbot-desktop.exe nicht gefunden." -ForegroundColor Red
exit 1
