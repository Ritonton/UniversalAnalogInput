@echo off
setlocal enabledelayedexpansion

echo ========================================
echo Universal Analog Input - Development Setup
echo ========================================

echo.
echo [1/5] Checking prerequisites...

:: Check Rust installation
where cargo >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: Cargo not found. Please install Rust from https://rustup.rs/
    echo After installation, restart your terminal and run this script again.
    pause
    exit /b 1
) else (
    echo ✓ Rust/Cargo found
)

:: Check .NET installation
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: dotnet not found. Please install .NET 8 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
) else (
    echo ✓ .NET SDK found
)

:: Check cbindgen installation
cargo install --list | find "cbindgen" >nul
if %errorlevel% neq 0 (
    echo.
    echo [2/5] Installing cbindgen...
    cargo install cbindgen
    if %errorlevel% neq 0 (
        echo ERROR: Failed to install cbindgen
        exit /b 1
    )
) else (
    echo ✓ cbindgen found
)

:: Check Visual Studio or Build Tools
echo.
echo [3/5] Checking Windows development tools...
echo Note: For WinUI 3 development, you need Visual Studio 2022 with:
echo   - .NET desktop development workload
echo   - Windows App SDK
echo   - C++ build tools (for native interop)
echo.

:: Create development directories
echo [4/5] Setting up development environment...
if not exist "%~dp0\..\logs" mkdir "%~dp0\..\logs"
if not exist "%~dp0\..\temp" mkdir "%~dp0\..\temp"

echo.
echo [5/5] Running initial build...
call "%~dp0\build.bat"

echo.
echo ========================================
echo Development environment setup complete!
echo ========================================
echo.
echo Useful commands:
echo   scripts\build.bat          - Build entire project
echo   cd native && cargo build  - Build only Rust library  
echo   cd ui && dotnet run        - Run C# application
echo.
echo For Visual Studio development:
echo   Open: ui\UniversalAnalogInputUI.sln
echo.

pause