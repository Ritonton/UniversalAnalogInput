# Universal Analog Input - Cross-Platform Build Script
param(
    [switch]$Release,
    [switch]$SkipRust,
    [switch]$SkipCSharp, 
    [switch]$Clean,
    [switch]$Help
)

# Colors for output (using PowerShell native colors)
function Write-ColorText($Text, $Color) {
    Write-Host $Text -ForegroundColor $Color
}

function Write-Header($Text) {
    Write-ColorText $Text "Cyan"
}

function Write-Success($Text) {
    Write-ColorText "[OK] $Text" "Green"
}

function Write-Warning($Text) {
    Write-ColorText "[WARN] $Text" "Yellow"
}

function Write-Error($Text) {
    Write-ColorText "[ERROR] $Text" "Red"
}

if ($Help) {
    Write-Header "Universal Analog Input - Build Script Help"
    Write-Host ""
    Write-ColorText "Usage:" "Yellow"
    Write-Host "  .\build.ps1 [options]"
    Write-Host ""
    Write-ColorText "Options:" "Yellow"
    Write-Host "  -Release      Build in release mode (default: debug)"
    Write-Host "  -SkipRust     Skip Rust library build"
    Write-Host "  -SkipCSharp   Skip C# application build"
    Write-Host "  -Clean        Clean previous builds before building"
    Write-Host "  -Help         Show this help message"
    Write-Host ""
    Write-ColorText "Examples:" "Yellow"
    Write-Host "  .\build.ps1                   (Build everything in debug)"
    Write-Host "  .\build.ps1 -Release          (Build everything in release)"
    Write-Host "  .\build.ps1 -Clean -Release   (Clean build in release)"
    Write-Host "  .\build.ps1 -SkipRust         (Build only C# part)"
    Write-Host ""
    exit 0
}

Write-Header "========================================"
Write-Header "Universal Analog Input - Build Script"
Write-Header "========================================"

$BuildType = if ($Release) { "release" } else { "debug" }
$Config = if ($Release) { "Release" } else { "Debug" }

Write-ColorText "Build Configuration:" "Yellow"
Write-Host "  Build Type: $BuildType"
Write-Host "  Skip Rust: $SkipRust"
Write-Host "  Skip C#: $SkipCSharp"
Write-Host "  Clean Build: $Clean"
Write-Host ""

# Check prerequisites
Write-Header "[0/6] Checking prerequisites..."

if (!(Get-Command cargo -ErrorAction SilentlyContinue)) {
    Write-Error "Cargo not found. Please install Rust from https://rustup.rs/"
    exit 1
}

if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet not found. Please install .NET 9 SDK"
    exit 1
}

Write-Success "Prerequisites check passed"

# Get project root directory
$ProjectRoot = Split-Path $PSScriptRoot -Parent

# Clean build if requested
if ($Clean) {
    Write-Header "[1/6] Cleaning previous builds..."
    
    Push-Location $ProjectRoot
    
    if (Test-Path "target") { Remove-Item -Path "target" -Recurse -Force }
    if (Test-Path "native/target") { Remove-Item -Path "native/target" -Recurse -Force }
    if (Test-Path "ui/UniversalAnalogInputUI/bin") { Remove-Item -Path "ui/UniversalAnalogInputUI/bin" -Recurse -Force }
    if (Test-Path "ui/UniversalAnalogInputUI/obj") { Remove-Item -Path "ui/UniversalAnalogInputUI/obj" -Recurse -Force }
    
    Pop-Location
    Write-Success "Clean completed"
}

# Build Rust library
if (!$SkipRust) {
    Write-Header "[2/6] Building Rust native library..."
    Push-Location $ProjectRoot
    
    if ($Release) {
        Write-Host "Building release version..."
        cargo build --release
    } else {
        Write-Host "Building debug version..."
        cargo build
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build Rust library"
        Pop-Location
        exit 1
    }
    
    Pop-Location
    Write-Success "Rust library built successfully"
} else {
    Write-Warning "Skipping Rust build"
}

# Generate C headers
Write-Header "[3/6] Generating C headers..."
Push-Location $ProjectRoot

try {
    cbindgen --config native/cbindgen.toml --crate universal-analog-input --output ui/native_bindings.h 2>$null
} catch {
    Write-Warning "cbindgen failed, headers may be outdated"
}

Pop-Location
Write-Success "Headers generated"

# Copy DLL to C# project
Write-Header "[4/6] Copying Rust DLL to C# project..."

$DllPath = if ($Release) { "target/release/universal_analog_input.dll" } else { "target/debug/universal_analog_input.dll" }
$FullDllPath = Join-Path $ProjectRoot $DllPath

if (Test-Path $FullDllPath) {
    $TargetPath = Join-Path $ProjectRoot "ui/UniversalAnalogInputUI/universal_analog_input.dll"
    Copy-Item $FullDllPath $TargetPath -Force
    Write-Success "Copied $BuildType DLL to C# project"
} else {
    Write-Error "DLL not found at $DllPath"
    exit 1
}

# Build C# application
if (!$SkipCSharp) {
    Write-Header "[5/6] Building C# WinUI 3 application..."
    $UiPath = Join-Path $ProjectRoot "ui"
    Push-Location $UiPath
    
    Write-Host "Restoring NuGet packages..."
    dotnet restore UniversalAnalogInputUI.sln --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to restore NuGet packages"
        Pop-Location
        exit 1
    }
    
    Write-Host "Building C# application ($Config)..."
    dotnet build UniversalAnalogInputUI.sln -c $Config --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to build C# application"
        Pop-Location
        exit 1
    }
    
    Pop-Location
    Write-Success "C# application built successfully"
} else {
    Write-Warning "Skipping C# build"
}

# Copy DLL to output directories
Write-Header "[6/6] Copying DLL to output directories..."

$OutputDir = Join-Path $ProjectRoot "ui/UniversalAnalogInputUI/bin/$Config/net9.0-windows10.0.19041.0/win-x64"

if (Test-Path $FullDllPath) {
    # Copy to .NET output directory
    if (!(Test-Path $OutputDir)) {
        New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null
    }
    $OutputDllPath = Join-Path $OutputDir "universal_analog_input.dll"
    Copy-Item $FullDllPath $OutputDllPath -Force
    Write-Success "DLL copied to .NET output directory: $OutputDir"
    
    # UI project directory copy was already done in step 4
    Write-Success "DLL also available in UI project directory for compatibility"
}

# Success message
Write-Host ""
Write-ColorText "========================================" "Green"
Write-ColorText "[SUCCESS] Build completed successfully!" "Green"
Write-ColorText "========================================" "Green"
Write-Host ""
Write-ColorText "Build Summary:" "Cyan"
Write-Host "  Configuration: $Config"
Write-Host "  Rust DLL: $DllPath"
Write-Host "  C# Output: $OutputDir"
Write-Host ""
Write-ColorText "To run the application:" "Cyan"
Write-ColorText "  cd ui" "Yellow"
Write-ColorText "  dotnet run --project UniversalAnalogInputUI" "Yellow"
Write-Host ""
Write-ColorText "Or run from Visual Studio:" "Cyan"
Write-ColorText '  Open ui/UniversalAnalogInputUI.sln and press F5' "Yellow"
Write-Host ""