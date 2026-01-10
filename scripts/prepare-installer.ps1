# Universal Analog Input - Installer Preparation Script
# ======================================================
# This script prepares everything needed for the Inno Setup installer:
# 1. Builds the application using package.ps1
# 2. Downloads required dependencies (ViGEm, Wooting SDK)
# 3. Organizes files for the installer
#
# Usage: .\scripts\prepare-installer.ps1 [-SkipBuild] [-SkipDependencies]

Param(
    [switch]$SkipBuild,
    [switch]$SkipDependencies,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Info($Message) {
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Success($Message) {
    Write-Host "[ OK ] $Message" -ForegroundColor Green
}

function Write-Warning($Message) {
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-ErrorAndExit($Message) {
    Write-Host "[ERR ] $Message" -ForegroundColor Red
    exit 1
}

function Get-FileFromUrl {
    param(
        [string]$Url,
        [string]$OutputPath
    )

    try {
        Write-Info "Downloading from: $Url"
        Write-Info "Saving to: $OutputPath"

        # Use WebClient for better progress display
        $webClient = New-Object System.Net.WebClient
        $webClient.DownloadFile($Url, $OutputPath)

        if (Test-Path $OutputPath) {
            $size = (Get-Item $OutputPath).Length / 1MB
            Write-Success "Downloaded: $([math]::Round($size, 2)) MB"
            return $true
        }
        else {
            Write-Warning "Download completed but file not found"
            return $false
        }
    }
    catch {
        Write-Warning "Download failed: $_"
        return $false
    }
}

function Get-LatestGitHubRelease {
    param(
        [string]$Repository,
        [string]$AssetPattern
    )

    try {
        Write-Info "Fetching latest release info for $Repository..."

        $apiUrl = "https://api.github.com/repos/$Repository/releases/latest"
        $headers = @{
            "User-Agent" = "UniversalAnalogInput-Installer"
        }

        $release = Invoke-RestMethod -Uri $apiUrl -Headers $headers
        $asset = $release.assets | Where-Object { $_.name -like $AssetPattern } | Select-Object -First 1

        if ($asset) {
            Write-Success "Found: $($asset.name) (v$($release.tag_name))"
            return @{
                Name = $asset.name
                Url = $asset.browser_download_url
                Version = $release.tag_name
            }
        }
        else {
            Write-Warning "No matching asset found for pattern: $AssetPattern"
            return $null
        }
    }
    catch {
        Write-Warning "Failed to fetch release info: $_"
        return $null
    }
}

# ============================================================================
# Main Script
# ============================================================================

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot
Set-Location $projectRoot

Write-Host @"

====================================================================
   Universal Analog Input - Installer Preparation                  
====================================================================

"@ -ForegroundColor Cyan

# ============================================================================
# Step 1: Build the application
# ============================================================================

if (-not $SkipBuild) {
    Write-Info "Step 1/3: Building application..."
    Write-Info "Running package.ps1 with SelfContained mode..."

    $packageScript = Join-Path $scriptRoot "package.ps1"
    if (-not (Test-Path $packageScript)) {
        Write-ErrorAndExit "package.ps1 not found at: $packageScript"
    }

    & $packageScript -SelfContained -OutputRoot "artifacts"

    if ($LASTEXITCODE -ne 0) {
        Write-ErrorAndExit "Application build failed"
    }

    # Find the latest package
    $packageRoot = Join-Path $projectRoot "artifacts"
    $latestPackage = Get-ChildItem -Path $packageRoot -Directory -Filter "UniversalAnalogInput-*" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $latestPackage) {
        Write-ErrorAndExit "No package found after build"
    }

    # Copy to standard location for installer
    $packageDir = Join-Path $projectRoot "artifacts\package"
    if (Test-Path $packageDir) {
        Remove-Item $packageDir -Recurse -Force
    }
    Copy-Item $latestPackage.FullName $packageDir -Recurse -Force

    Write-Success "Application built successfully"
    Write-Host ""
}
else {
    Write-Info "Step 1/3: Skipping build (using existing package)"
    Write-Host ""
}

# ============================================================================
# Step 2: Download dependencies
# ============================================================================

if (-not $SkipDependencies) {
    Write-Info "Step 2/3: Downloading dependencies..."

    $depsDir = Join-Path $projectRoot "installer\dependencies"
    if (-not (Test-Path $depsDir)) {
        New-Item -ItemType Directory -Path $depsDir -Force | Out-Null
    }

    # ------------------------------------------------------------------------
    # Download ViGEm Bus Driver
    # ------------------------------------------------------------------------

    Write-Host ""
    Write-Info "Downloading ViGEm Bus Driver..."

    $vigemInfo = Get-LatestGitHubRelease -Repository "nefarius/ViGEmBus" -AssetPattern "*.exe"

    if ($vigemInfo) {
        $vigemPath = Join-Path $depsDir $vigemInfo.Name

        if ((Test-Path $vigemPath) -and -not $Force) {
            Write-Success "ViGEm already downloaded: $($vigemInfo.Name)"
        }
        else {
            $downloaded = Get-FileFromUrl -Url $vigemInfo.Url -OutputPath $vigemPath

            if (-not $downloaded) {
                Write-Warning "Failed to download ViGEm automatically"
                Write-Host ""
                Write-Host "Please download manually from:" -ForegroundColor Yellow
                Write-Host "  https://github.com/nefarius/ViGEmBus/releases/latest" -ForegroundColor Yellow
                Write-Host "Save the .exe file to: $depsDir" -ForegroundColor Yellow
                Write-Host ""
            }
        }
    }
    else {
        Write-Warning "Could not find ViGEm release information"
        Write-Host ""
        Write-Host "Please download manually from:" -ForegroundColor Yellow
        Write-Host "  https://github.com/nefarius/ViGEmBus/releases/latest" -ForegroundColor Yellow
        Write-Host "Save the .exe file to: $depsDir" -ForegroundColor Yellow
        Write-Host ""
    }

    # ------------------------------------------------------------------------
    # Download Wooting Analog SDK
    # ------------------------------------------------------------------------

    Write-Host ""
    Write-Info "Downloading Wooting Analog SDK..."

    $wootingInfo = Get-LatestGitHubRelease -Repository "WootingKb/wooting-analog-sdk" -AssetPattern "*.msi"

    if ($wootingInfo) {
        $wootingPath = Join-Path $depsDir $wootingInfo.Name

        if ((Test-Path $wootingPath) -and -not $Force) {
            Write-Success "Wooting SDK already downloaded: $($wootingInfo.Name)"
        }
        else {
            $downloaded = Get-FileFromUrl -Url $wootingInfo.Url -OutputPath $wootingPath

            if (-not $downloaded) {
                Write-Warning "Failed to download Wooting SDK automatically"
                Write-Host ""
                Write-Host "Please download manually from:" -ForegroundColor Yellow
                Write-Host "  https://github.com/WootingKb/wooting-analog-sdk/releases/latest" -ForegroundColor Yellow
                Write-Host "Save the .msi file to: $depsDir" -ForegroundColor Yellow
                Write-Host ""
            }
        }
    }
    else {
        Write-Warning "Could not find Wooting SDK release information"
        Write-Host ""
        Write-Host "Please download manually from:" -ForegroundColor Yellow
        Write-Host "  https://github.com/WootingKb/wooting-analog-sdk/releases/latest" -ForegroundColor Yellow
        Write-Host "Save the .msi file to: $depsDir" -ForegroundColor Yellow
        Write-Host ""
    }

    Write-Success "Dependency download complete"
    Write-Host ""
}
else {
    Write-Info "Step 2/3: Skipping dependency download"
    Write-Host ""
}

# ============================================================================
# Step 3: Verify installer prerequisites
# ============================================================================

Write-Info "Step 3/3: Verifying installer prerequisites..."

$errors = @()

# Check for package directory
$packageDir = Join-Path $projectRoot "artifacts\package"
if (-not (Test-Path $packageDir)) {
    $errors += "Package directory not found: $packageDir"
}
else {
    $mainExe = Join-Path $packageDir "UniversalAnalogInput.exe"
    if (-not (Test-Path $mainExe)) {
        $errors += "Main executable not found: $mainExe"
    }

    $uiExe = Join-Path $packageDir "ui\UniversalAnalogInputUI.exe"
    if (-not (Test-Path $uiExe)) {
        $errors += "UI executable not found: $uiExe"
    }
}

# Check for dependencies
$depsDir = Join-Path $projectRoot "installer\dependencies"
$vigemExists = (Get-ChildItem -Path $depsDir -Filter "ViGEmBus*.exe" -ErrorAction SilentlyContinue).Count -gt 0
$wootingExists = (Get-ChildItem -Path $depsDir -Filter "wooting_analog_sdk*.msi" -ErrorAction SilentlyContinue).Count -gt 0

if (-not $vigemExists) {
    $errors += "ViGEm installer not found in: $depsDir"
}

if (-not $wootingExists) {
    $errors += "Wooting SDK installer not found in: $depsDir"
}

# Check for Inno Setup
$innoSetup = Get-Command "iscc" -ErrorAction SilentlyContinue
if (-not $innoSetup) {
    $errors += "Inno Setup compiler (iscc.exe) not found in PATH"
}

# Check for icon
$iconPath = Join-Path $projectRoot "shared\assets\icon.ico"
if (-not (Test-Path $iconPath)) {
    $errors += "Icon file not found: $iconPath"
}

# Check for license
$licensePath = Join-Path $projectRoot "LICENSE"
if (-not (Test-Path $licensePath)) {
    $errors += "LICENSE file not found: $licensePath"
}

Write-Host ""

if ($errors.Count -gt 0) {
    Write-Host "======================================================================" -ForegroundColor Red
    Write-Host "  ERRORS FOUND - Installer cannot be built" -ForegroundColor Red
    Write-Host "======================================================================" -ForegroundColor Red
    Write-Host ""

    foreach ($errorItem in $errors) {
        Write-Host "  X $errorItem" -ForegroundColor Red
    }

    Write-Host ""
    exit 1
}
else {
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host "  SUCCESS - Ready to build installer!" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Green
    Write-Host ""

    Write-Host "  Application package ready" -ForegroundColor Green
    if ($vigemExists) { Write-Host "  ViGEm Bus Driver installer ready" -ForegroundColor Green }
    if ($wootingExists) { Write-Host "  Wooting Analog SDK installer ready" -ForegroundColor Green }
    if ($innoSetup) { Write-Host "  Inno Setup compiler available" -ForegroundColor Green }
    Write-Host ""

    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Review installer\setup.iss if needed" -ForegroundColor White
    Write-Host "  2. Run: .\scripts\build-installer.ps1" -ForegroundColor Yellow
    Write-Host "  3. Find installer in: artifacts\installer\" -ForegroundColor White
    Write-Host ""
}
