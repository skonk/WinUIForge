param(
    [ValidateSet("Debug","Release")]
    [string]$Configuration = "Release",
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$solution = Join-Path $root "WinUIForge.Port.slnx"

Write-Host "== WinUI Forge port build ==" -ForegroundColor Cyan
Write-Host "Solution: $solution"

$running = Get-Process -Name "WinUIForge.Port.App" -ErrorAction SilentlyContinue
if ($running) {
    throw "WinUIForge.Port.App is currently running. Close the app before rebuilding so the output files are not locked."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found. Install the .NET 10 SDK and rerun."
}

$version = (& dotnet --version).Trim()
Write-Host ".NET SDK: $version"
if (-not $version.StartsWith("10.")) {
    Write-Warning "The WinUI Forge port proof targets .NET 10."
}

& dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

& dotnet run --project (Join-Path $root "tests\WinUIForge.Port.Core.Tests\WinUIForge.Port.Core.Tests.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "WinUIForge.Port.Core.Tests failed." }

& dotnet build $solution -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

$exe = Join-Path $root "src\WinUIForge.Port.App\bin\$Configuration\net10.0-windows10.0.19041.0\win-x64\WinUIForge.Port.App.exe"
if (-not (Test-Path $exe)) {
    throw "Build succeeded but expected executable was not found at $exe"
}

$icon = Join-Path $root "assets\workshop-ui\app-icon\Workshop.ico"
$iconStamper = Join-Path $root "scripts\set-workshop-exe-icon.ps1"

if (-not (Test-Path $icon)) {
    throw "Workshop icon was not found at $icon"
}

if (-not (Test-Path $iconStamper)) {
    throw "Workshop executable icon stamper was not found at $iconStamper"
}

Write-Host ""
Write-Host "Stamping Workshop icon into executable resources..." -ForegroundColor Cyan
& $iconStamper -ExePath $exe -IconPath $icon
if ($LASTEXITCODE -ne 0) { throw "Workshop executable icon stamping failed." }

Write-Host ""
Write-Host "Port proof build succeeded." -ForegroundColor Green
Write-Host $exe

if ($Run) {
    Start-Process $exe
}
