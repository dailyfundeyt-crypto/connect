# Connect Schnellstart: natives Fenster ohne jedes Mal neu zu kompilieren.
# Usage:
#   .\START-CONNECT.ps1              # EXE wenn da, sonst Debug-EXE, sonst bun
#   .\START-CONNECT.ps1 -Dev         # immer bun run connect (langsam, Rebuild)
#   .\START-CONNECT.ps1 -Build       # Release bauen
#   .\START-CONNECT.ps1 -Schnell     # nur EXE starten, Stack nicht anfassen

param(
  [switch]$Dev,
  [switch]$Build,
  [switch]$Schnell
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $root) { $root = (Get-Location).Path }
$desktop = Join-Path $root "desktop"

$env:CONNECT_PRODUCT_WINDOW = "1"
$env:CONNECT_ROOT = $root
if (-not $env:OPENBOT_FORCE_START) { $env:OPENBOT_FORCE_START = "1" }

function Test-Port([int]$Port) {
  try {
    $c = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
      Where-Object { $_.LocalAddress -eq '127.0.0.1' -or $_.LocalAddress -eq '::1' }
    return [bool]$c
  } catch { return $false }
}

function Start-UiIfNeeded {
  if ((Test-Port 3010) -and (Test-Port 3001)) {
    Write-Host "[Connect] UI schon da (3010 + 3001)." -ForegroundColor Green
    return
  }
  Write-Host "[Connect] UI fehlt â€” starte App/API im Hintergrund (WSL)..." -ForegroundColor Yellow
  $script = Join-Path $root "scripts\keep-v4.sh"
  if (-not (Test-Path $script)) {
    Write-Host "[Connect] keep-v4.sh fehlt â€” Fenster startet trotzdem (Splash bis UI da ist)." -ForegroundColor Yellow
    return
  }
  $scriptWsl = "/mnt/c/Users/Kunc GmbH/Desktop/OpenBot/scripts/keep-v4.sh"
  Start-Process -FilePath "wsl.exe" -ArgumentList @("-d","Ubuntu","-u","root","--","bash",$scriptWsl) -WindowStyle Hidden
}

Write-Host ""
Write-Host "[Connect] Natives Fenster, Titel Connect, keine Adressleiste." -ForegroundColor Cyan

if (-not (Test-Path (Join-Path $desktop "package.json"))) {
  Write-Host "[Connect] Ordner desktop fehlt. Branch/Clone prÃ¼fen." -ForegroundColor Red
  exit 1
}

if ($Build) {
  if (-not (Get-Command bun -ErrorAction SilentlyContinue)) {
    Write-Host "[Connect] Bun fehlt im PATH." -ForegroundColor Red
    exit 1
  }
  Set-Location $desktop
  bun run connect:package
  exit $LASTEXITCODE
}

$release = Join-Path $desktop "src-tauri\target\release\openbot-desktop.exe"
$debug = Join-Path $desktop "src-tauri\target\debug\openbot-desktop.exe"

if (-not $Dev) {
  $exe = $null
  if (Test-Path $release) { $exe = $release }
  elseif (Test-Path $debug) { $exe = $debug }

  if ($exe) {
    if (-not $Schnell) { Start-UiIfNeeded }
    Write-Host "[Connect] Starte fertige EXE: $exe" -ForegroundColor Green
    Start-Process -FilePath $exe -WorkingDirectory $desktop
    exit 0
  }
}

if (-not (Get-Command bun -ErrorAction SilentlyContinue)) {
  Write-Host "[Connect] Keine EXE und kein Bun â€” zuerst bun install / connect:package." -ForegroundColor Red
  exit 1
}

Write-Host "[Connect] Keine fertige EXE â€” bun run connect (erster Start kann dauern)." -ForegroundColor Yellow
if (-not $Schnell) { Start-UiIfNeeded }
Set-Location $desktop
bun run connect
exit $LASTEXITCODE
