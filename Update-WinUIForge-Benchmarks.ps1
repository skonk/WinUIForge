param(
    [switch]$BuildForge,
    [switch]$OpenBenchmarks
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $MyInvocation.MyCommand.Path
$branch = "winui-forge/m4-benchmark"

Write-Host "WinUI Forge benchmark updater" -ForegroundColor Cyan
Write-Host "Repository: $repo"
Write-Host "Branch:     $branch"
Write-Host ""

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git was not found on PATH."
}

$dirtyBenchmarks = @(
    & git -C $repo status --porcelain -- "benchmarks/workshop-dashboard-v1" "benchmarks/workshop-storage-settings-v1"
)

if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect the WinUI Forge repository."
}

if ($dirtyBenchmarks.Count -gt 0) {
    Write-Host "Benchmark files have local changes:" -ForegroundColor Yellow
    $dirtyBenchmarks | ForEach-Object { Write-Host "  $_" }
    throw "Commit or stash those benchmark changes before updating so Forge does not overwrite visual edits."
}

Write-Host "Fetching latest benchmark work..." -ForegroundColor Cyan
& git -C $repo fetch origin $branch --prune
if ($LASTEXITCODE -ne 0) { throw "git fetch failed." }

& git -C $repo switch $branch
if ($LASTEXITCODE -ne 0) { throw "Could not switch to $branch." }

& git -C $repo pull --ff-only origin $branch
if ($LASTEXITCODE -ne 0) { throw "git pull --ff-only failed." }

$dashboard = Join-Path $repo "benchmarks\workshop-dashboard-v1\Screen.xaml"
$settings = Join-Path $repo "benchmarks\workshop-storage-settings-v1\Screen.xaml"

Write-Host ""
Write-Host "Benchmarks updated." -ForegroundColor Green
Write-Host "Dashboard:"
Write-Host "  $dashboard"
Write-Host "Storage & Settings:"
Write-Host "  $settings"

if ($BuildForge) {
    Write-Host ""
    Write-Host "Building and launching Forge..." -ForegroundColor Cyan
    & (Join-Path $repo "build-port.ps1") -Configuration Release -Run
    if ($LASTEXITCODE -ne 0) { throw "Forge build failed." }
} else {
    Write-Host ""
    Write-Host "Forge rebuild skipped." -ForegroundColor DarkGray
    Write-Host "If Forge is already running, use Open XAML... or Reload XAML."
    Write-Host "Use -BuildForge when you want the latest Forge UI/features as well."
}

if ($OpenBenchmarks) {
    Start-Process explorer.exe -ArgumentList (Split-Path -Parent $settings)
}
