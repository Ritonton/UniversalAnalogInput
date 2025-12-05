Param(
    [string]$OutputRoot = "artifacts",
    [string]$Runtime = "win-x64",
    [string[]]$KeepSatelliteLanguages = @("en-US"),
    [switch]$SelfContained,
    [switch]$Clean
)

function Write-Info($Message) {
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success($Message) {
    Write-Host "[ OK ] $Message" -ForegroundColor Green
}

function Write-ErrorAndExit($Message) {
    Write-Host "[ERR ] $Message" -ForegroundColor Red
    exit 1
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot
Set-Location $projectRoot

if (!(Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-ErrorAndExit "Cargo not found. Install rustup / Rust toolchain."
}

if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-ErrorAndExit "dotnet not found. Install .NET SDK (9.0 recommended)."
}

if ($Clean -and (Test-Path "target")) { Remove-Item target -Recurse -Force }
if ($Clean -and (Test-Path "ui/UniversalAnalogInputUI/bin")) { Remove-Item "ui/UniversalAnalogInputUI/bin" -Recurse -Force }
if ($Clean -and (Test-Path "ui/UniversalAnalogInputUI/obj")) { Remove-Item "ui/UniversalAnalogInputUI/obj" -Recurse -Force }

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packageRoot = Join-Path $projectRoot $OutputRoot
if (!(Test-Path $packageRoot)) { New-Item -ItemType Directory -Path $packageRoot | Out-Null }
$packageDir = Join-Path $packageRoot "UniversalAnalogInput-$timestamp"
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

Write-Info "Building Rust (release)..."
if ($LASTEXITCODE -ne 0) { $null = 0 } # reset
cargo build --release
if ($LASTEXITCODE -ne 0) {
    Write-ErrorAndExit "Rust build failed."
}

$rustTarget = "target/release"
$trayExePath = Join-Path $projectRoot "$rustTarget/uai-tray.exe"
$trayPdbPath = Join-Path $projectRoot "$rustTarget/uai-tray.pdb"
if (!(Test-Path $trayExePath)) {
    Write-ErrorAndExit "Tray executable was not generated (uai-tray.exe missing)."
}

Write-Info "dotnet publish (Release, runtime $Runtime)..."
$uiDir = Join-Path $packageDir "ui"
New-Item -ItemType Directory -Path $uiDir -Force | Out-Null
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }
$publishArgs = @(
    "publish", "ui/UniversalAnalogInputUI/UniversalAnalogInputUI.csproj",
    "-c", "Release",
    "-r", $Runtime,
    "-o", $uiDir,
    "--self-contained", $selfContainedValue
)
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-ErrorAndExit "dotnet publish failed."
}

Write-Info "Organizing deployment structure..."

# 1. Copy tray exe to root (renamed to UniversalAnalogInput.exe)
Copy-Item $trayExePath (Join-Path $packageDir "UniversalAnalogInput.exe") -Force
Write-Host "  [OK] Tray executable (root): UniversalAnalogInput.exe" -ForegroundColor Green

# 2. Copy PDB files for debugging (optional)
if (Test-Path $trayPdbPath) {
    Copy-Item $trayPdbPath $packageDir -Force
}

# 3. Create profiles folder with default profile
$profilesDir = Join-Path $packageDir "profiles"
New-Item -ItemType Directory -Path $profilesDir -Force | Out-Null
$defaultProfilePath = Join-Path $projectRoot "shared\configs\default_profile.json"
if (Test-Path $defaultProfilePath) {
    Copy-Item $defaultProfilePath (Join-Path $profilesDir "default.json") -Force
    Write-Host "  [OK] Default profile: profiles\default.json" -ForegroundColor Green
} else {
    Write-Host "  [INFO] Default profile not found (will be created at runtime)" -ForegroundColor Yellow
}

# 4. Create README.txt
$readmePath = Join-Path $packageDir "README.txt"
$readmeContent = @'
Universal Analog Input - Analog Keyboard to Gamepad Mapper
===========================================================

INSTALLATION:
1. Extract all files to a folder of your choice
2. Run UniversalAnalogInput.exe (the tray application will start)
3. The UI will launch automatically on first run

USAGE:
- The tray icon in the system tray indicates the app is running
- Right-click the tray icon to access the menu:
  * Show UI: Open the configuration interface
  * Profiles: Quick profile switching (coming soon)
  * Exit: Close the application

REQUIREMENTS:
- Windows 10 version 1903 (build 19041) or higher
- Analog keyboard (Wooting or plugin-supported)
- Wooting analog SDK
- ViGEm Bus Driver

FOLDERS:
- ui/        : WinUI 3 configuration interface and dependencies
- profiles/  : Default profile template (copied to AppData on first run)

USER DATA LOCATIONS:
- Profiles: %APPDATA%\UniversalAnalogInput\profiles\
- Logs: %LOCALAPPDATA%\UniversalAnalogInput\crash.log
- Settings: %LOCALAPPDATA%\UniversalAnalogInput\settings.txt
- Theme: %LOCALAPPDATA%\UniversalAnalogInput\theme.txt

For support and updates, visit:
https://github.com/Ritonton/UniversalAnalogInput

© 2024 - Licensed under MIT
'@
$readmeContent | Out-File -FilePath $readmePath -Encoding UTF8
Write-Host "  [OK] Documentation: README.txt" -ForegroundColor Green

Write-Success "`nBuild complete. Package ready for distribution:"
Write-Host "`nPackage structure:" -ForegroundColor Cyan
Write-Host "  $packageDir\" -ForegroundColor White
Write-Host "  |-- UniversalAnalogInput.exe  (tray)" -ForegroundColor Yellow
Write-Host "  |-- README.txt" -ForegroundColor Gray
Write-Host "  |-- ui\" -ForegroundColor White
Write-Host "  |   |-- UniversalAnalogInputUI.exe" -ForegroundColor Yellow
Write-Host "  |   '-- *.dll (WinUI + WindowsAppSDK)" -ForegroundColor Gray
Write-Host "  '-- profiles\" -ForegroundColor White
Write-Host "      '-- default.json" -ForegroundColor Gray
Write-Host "`nThe package can now be zipped and distributed." -ForegroundColor Green