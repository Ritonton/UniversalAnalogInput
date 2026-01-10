# Universal Analog Input - Installer Build Script
# =================================================
# Compiles the Inno Setup installer
#
# Usage: .\scripts\build-installer.ps1 [-Version "1.0.0"]

Param(
    [string]$Version = "1.0.0",
    [switch]$SkipPreparation
)

$ErrorActionPreference = "Stop"

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

Write-Host @"

====================================================================
   Universal Analog Input - Installer Builder                      
====================================================================

"@ -ForegroundColor Cyan

# ============================================================================
# Step 1: Run preparation script
# ============================================================================

if (-not $SkipPreparation) {
    Write-Info "Running preparation script..."
    $prepareScript = Join-Path $scriptRoot "prepare-installer.ps1"

    if (-not (Test-Path $prepareScript)) {
        Write-ErrorAndExit "Preparation script not found: $prepareScript"
    }

    & $prepareScript

    if ($LASTEXITCODE -ne 0) {
        Write-ErrorAndExit "Preparation failed"
    }

    Write-Host ""
}
else {
    # If preparation is skipped, ensure artifacts\package exists
    # by copying from the latest timestamped package
    $packageDir = Join-Path $projectRoot "artifacts\package"

    if (-not (Test-Path $packageDir)) {
        Write-Info "Package directory not found, searching for latest build..."

        $packageRoot = Join-Path $projectRoot "artifacts"
        $latestPackage = Get-ChildItem -Path $packageRoot -Directory -Filter "UniversalAnalogInput-*" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($latestPackage) {
            Write-Info "Found package: $($latestPackage.Name)"
            Write-Info "Copying to standard location..."
            Copy-Item $latestPackage.FullName $packageDir -Recurse -Force
            Write-Success "Package ready"
            Write-Host ""
        }
        else {
            Write-ErrorAndExit "No package found. Run without -SkipPreparation or run .\scripts\package.ps1 first"
        }
    }
}

# ============================================================================
# Step 2: Check for Inno Setup
# ============================================================================

Write-Info "Checking for Inno Setup compiler..."

$iscc = Get-Command "iscc" -ErrorAction SilentlyContinue

if (-not $iscc) {
    # Try common installation paths
    $commonPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\iscc.exe",
        "${env:ProgramFiles}\Inno Setup 6\iscc.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\iscc.exe",
        "${env:ProgramFiles}\Inno Setup 5\iscc.exe"
    )

    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $iscc = Get-Command $path
            break
        }
    }

    if (-not $iscc) {
        Write-ErrorAndExit @"
Inno Setup compiler not found!

Please install Inno Setup 6.4 or later from:
  https://jrsoftware.org/isdl.php

Or add iscc.exe to your PATH.
"@
    }
}

Write-Success "Found Inno Setup: $($iscc.Source)"
Write-Host ""

# ============================================================================
# Step 3: Update version in setup script
# ============================================================================

Write-Info "Updating version to $Version..."

$setupScript = Join-Path $projectRoot "installer\setup.iss"

if (-not (Test-Path $setupScript)) {
    Write-ErrorAndExit "Setup script not found: $setupScript"
}

# Read the setup script
$content = Get-Content $setupScript -Raw

# Update the version
$content = $content -replace '#define AppVersion ".*"', "#define AppVersion `"$Version`""

# Write back
$content | Set-Content $setupScript -NoNewline

Write-Success "Version updated to $Version"
Write-Host ""

# ============================================================================
# Step 4: Compile the installer
# ============================================================================

Write-Info "Compiling installer with Inno Setup..."
Write-Info "This may take a few minutes..."
Write-Host ""

$outputDir = Join-Path $projectRoot "artifacts\installer"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Build the installer
$buildArgs = @(
    "/O$outputDir",
    "/Q",  # Quiet mode
    $setupScript
)

$process = Start-Process -FilePath $iscc.Source -ArgumentList $buildArgs -Wait -PassThru -NoNewWindow

if ($process.ExitCode -ne 0) {
    Write-ErrorAndExit "Installer compilation failed with exit code $($process.ExitCode)"
}

Write-Host ""

# ============================================================================
# Step 5: Verify output
# ============================================================================

Write-Info "Verifying installer..."

$installerName = "UniversalAnalogInput-Setup-v$Version.exe"
$installerPath = Join-Path $outputDir $installerName

if (-not (Test-Path $installerPath)) {
    Write-ErrorAndExit "Installer was not created: $installerPath"
}

$installerSize = (Get-Item $installerPath).Length / 1MB

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  INSTALLER BUILT SUCCESSFULLY!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Installer Details:" -ForegroundColor Cyan
Write-Host "  Name    : $installerName" -ForegroundColor White
Write-Host "  Version : $Version" -ForegroundColor White
Write-Host "  Size    : $([math]::Round($installerSize, 2)) MB" -ForegroundColor White
Write-Host "  Path    : $installerPath" -ForegroundColor White
Write-Host ""
Write-Host "The installer includes:" -ForegroundColor Cyan
Write-Host "  [+] Universal Analog Input application" -ForegroundColor Green
Write-Host "  [+] ViGEm Bus Driver (auto-install if needed)" -ForegroundColor Green
Write-Host "  [+] Wooting Analog SDK (auto-install if needed)" -ForegroundColor Green
Write-Host "  [+] Start menu shortcuts" -ForegroundColor Green
Write-Host "  [+] Optional desktop shortcut" -ForegroundColor Green
Write-Host "  [+] Complete uninstaller" -ForegroundColor Green
Write-Host ""
Write-Host "Ready for distribution!" -ForegroundColor Green
Write-Host ""

# Open the output folder
$openFolder = Read-Host "Open installer folder? (Y/N)"
if ($openFolder -eq "Y" -or $openFolder -eq "y") {
    Start-Process "explorer.exe" -ArgumentList $outputDir
}
