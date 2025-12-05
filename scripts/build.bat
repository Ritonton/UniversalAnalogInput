@echo off
setlocal enabledelayedexpansion

rem ============================================================
rem Universal Analog Input - Build Script (fixed flow)
rem ============================================================

rem ---------- Resolve ROOT_DIR (repo root = script\..)
set "SCRIPT_DIR=%~dp0"
set "ROOT_DIR=%SCRIPT_DIR%.."
pushd "%ROOT_DIR%" >nul 2>nul || (
  echo [ERROR] Unable to cd to ROOT_DIR: %ROOT_DIR%
  exit /b 1
)

goto :main

:: ---------------------- SUBROUTINES BELOW ----------------------

:echoInfo
  if not defined C_BLUE (echo(%~1) else (for /f "delims=" %%E in ('echo prompt $E^| cmd') do set "ESC=%%E")
  echo(%C_BLUE%%~1%C_RESET%
  goto :eof

:echoOk
  echo(%C_GREEN%%~1%C_RESET%
  goto :eof

:echoWarn
  echo(%C_YELLOW%%~1%C_RESET%
  goto :eof

:echoErr
  1>&2 echo(%C_RED%%~1%C_RESET%
  goto :eof

:die
  call :echoErr "[FAIL] %~1"
  popd >nul 2>nul
  exit /b 1

:show_help
echo.
echo Universal Analog Input - Build Script Help
echo.
echo Usage:
echo   build.bat [options]
echo.
echo Options:
echo   --release             Build in release mode              (default: debug)
echo   --arch=x64|x86|arm64  Target architecture                (default: x64)
echo   --skip-rust           Skip Rust library build
echo   --skip-csharp         Skip C# application build
echo   --clean               Clean previous builds before building
echo   --run                 Run the app after a successful build
echo   --log                 Write a full log to build.log
echo   --help                Show this help message
echo.
echo Examples:
echo   build.bat                               ^(Build everything in debug, x64^)
echo   build.bat --release --arch=arm64        ^(Release build for ARM64^)
echo   build.bat --clean --release --run       ^(Clean, build release, then run^)
echo   build.bat --skip-rust --arch=x86        ^(Build only C# for x86^)
echo.
exit /b 0

:: ---------------------- MAIN FLOW ----------------------
:main
rem ---------- Defaults
set "BUILD_TYPE=debug"
set "SKIP_RUST=false"
set "SKIP_CSHARP=false"
set "CLEAN_BUILD=false"
set "RUN_AFTER=false"
set "ARCH=x64"
set "ENABLE_LOG=false"
set "HAS_HELP=false"

rem ---------- Color/ANSI setup (best effort)
set "USE_ANSI=false"
if defined WT_SESSION set "USE_ANSI=true"
if defined ANSICON set "USE_ANSI=true"
if /i "%USE_ANSI%"=="true" (
  for /f "delims=" %%E in ('echo prompt $E^| cmd') do set "ESC=%%E"
  set "C_BLUE=%ESC%[94m"
  set "C_GREEN=%ESC%[92m"
  set "C_YELLOW=%ESC%[93m"
  set "C_RED=%ESC%[91m"
  set "C_RESET=%ESC%[0m"
) else (
  set "C_BLUE="
  set "C_GREEN="
  set "C_YELLOW="
  set "C_RED="
  set "C_RESET="
)

rem ---------- Parse args
:parse_args
if "%~1"=="" goto :args_done

if /i "%~1"=="--help"         set "HAS_HELP=true"       & shift & goto :parse_args
if /i "%~1"=="--release"      set "BUILD_TYPE=release"  & shift & goto :parse_args
if /i "%~1"=="--skip-rust"    set "SKIP_RUST=true"      & shift & goto :parse_args
if /i "%~1"=="--skip-csharp"  set "SKIP_CSHARP=true"    & shift & goto :parse_args
if /i "%~1"=="--clean"        set "CLEAN_BUILD=true"    & shift & goto :parse_args
if /i "%~1"=="--run"          set "RUN_AFTER=true"      & shift & goto :parse_args
if /i "%~1"=="--log"          set "ENABLE_LOG=true"     & shift & goto :parse_args

echo(%~1 | findstr /i /r "^--arch=" >nul
if not errorlevel 1 (
  for /f "tokens=1,2 delims==" %%A in ("%~1") do set "ARCH=%%B"
  if /i "%ARCH%"=="x86"   set "ARCH=x86"
  if /i "%ARCH%"=="x64"   set "ARCH=x64"
  if /i "%ARCH%"=="arm64" set "ARCH=arm64"
  shift & goto :parse_args
)

call :echoWarn "[WARN] Unknown option: %~1"
shift
goto :parse_args

:args_done
if /i "%HAS_HELP%"=="true" goto :show_help

rem ---------- Logging (optional)
set "LOG_FILE=%ROOT_DIR%\build.log"
if /i "%ENABLE_LOG%"=="true" (
  call :echoInfo "[LOG] Logging enabled -> %LOG_FILE%"
  (echo ===== %DATE% %TIME% ^| Windows build start =====) > "%LOG_FILE%"
) else (
  if exist "%LOG_FILE%" del "%LOG_FILE%" >nul 2>nul
)
set "TEE=>nul 2>nul"
if /i "%ENABLE_LOG%"=="true" set "TEE=>>"%LOG_FILE%" 2>>&1"

rem ---------- Header
call :echoInfo "========================================"
call :echoInfo "Universal Analog Input - Build Script"
call :echoInfo "========================================"
echo(Build Configuration:
echo(  Build Type : %BUILD_TYPE%
echo(  Arch       : %ARCH%
echo(  Skip Rust  : %SKIP_RUST%
echo(  Skip C#    : %SKIP_CSHARP%
echo(  Clean      : %CLEAN_BUILD%
echo(  Run        : %RUN_AFTER%
echo(  Log        : %ENABLE_LOG%
echo.

rem ---------- Prereqs
call :echoInfo "[0/6] Checking prerequisites..."
where cargo >nul 2>nul || call :die "Cargo not found. Install Rust via https://rustup.rs/"
where dotnet >nul 2>nul || call :die "dotnet not found. Install .NET 9 SDK."
where cbindgen >nul 2>nul && (
  call :echoOk "OK cbindgen found"
) || (
  call :echoWarn "[WARN] cbindgen not found. Headers won't be refreshed."
)
call :echoOk "OK Prerequisites check passed"

rem ---------- Clean
if /i "%CLEAN_BUILD%"=="true" (
  echo.
  call :echoInfo "[1/6] Cleaning previous builds..."
  for %%D in (
    "target"
    "native\target"
    "ui\UniversalAnalogInputUI\bin"
    "ui\UniversalAnalogInputUI\obj"
  ) do (
    if exist "%%~D" rmdir /s /q "%%~D" %TEE%
  )
  call :echoOk "OK Clean completed"
)

rem ---------- Build Rust
if /i "%SKIP_RUST%"=="false" (
  echo.
  call :echoInfo "[2/6] Building Rust native library..."
  if /i "%BUILD_TYPE%"=="release" (
    echo(Building Rust (release)...
    cargo build --release %TEE% || call :die "Failed to build Rust library (release)"
  ) else (
    echo(Building Rust (debug)...
    cargo build %TEE% || call :die "Failed to build Rust library (debug)"
  )
  call :echoOk "OK Rust library built successfully"
) else (
  call :echoWarn "[SKIP] Skipping Rust build"
)

rem ---------- Generate C headers (best effort)
echo.
call :echoInfo "[3/6] Generating C headers..."
if exist "native\cbindgen.toml" (
  where cbindgen >nul 2>nul && (
    cbindgen --config native/cbindgen.toml --crate universal-analog-input --output ui/native_bindings.h %TEE% || (
      call :echoWarn "[WARN] cbindgen failed (headers may be outdated)"
    )
  ) || (
    call :echoWarn "[WARN] cbindgen not installed (headers unchanged)"
  )
) else (
  call :echoWarn "[WARN] cbindgen config not found (native/cbindgen.toml)"
)
call :echoOk "OK Headers step done"

rem ---------- Compute DLL path
if /i "%BUILD_TYPE%"=="release" (
  set "DLL_PATH=target\release\universal_analog_input.dll"
) else (
  set "DLL_PATH=target\debug\universal_analog_input.dll"
)
if not exist "%DLL_PATH%" (
  if /i "%SKIP_RUST%"=="true" (
    call :die "DLL not found: %DLL_PATH%. You skipped Rust build; ensure it already exists."
  ) else (
    call :die "DLL not found after Rust build: %DLL_PATH%"
  )
)

rem ---------- Build C# (WinUI 3)
if /i "%SKIP_CSHARP%"=="true" goto :skip_csharp

echo.
call :echoInfo "[4/6] Building C# WinUI 3 application..."
pushd "ui" >nul || call :die "Cannot cd to ui directory"

echo(Restoring NuGet packages...
dotnet restore UniversalAnalogInputUI.sln --verbosity quiet %TEE% || (
  popd >nul
  call :die "Failed to restore NuGet packages"
)

if /i "%BUILD_TYPE%"=="release" (
  set "CONFIG=Release"
  echo(Building C# (Release)...
  dotnet build UniversalAnalogInputUI.sln -c Release --verbosity quiet %TEE% || (
    popd >nul
    call :die "Failed to build C# application (Release)"
  )
) else (
  set "CONFIG=Debug"
  echo(Building C# (Debug)...
  dotnet build UniversalAnalogInputUI.sln -c Debug --verbosity quiet %TEE% || (
    popd >nul
    call :die "Failed to build C# application (Debug)"
  )
)

popd >nul
call :echoOk "OK C# application built successfully"
goto :csharp_done

:skip_csharp
call :echoWarn "[SKIP] Skipping C# build"
if /i "%BUILD_TYPE%"=="release" (set "CONFIG=Release") else (set "CONFIG=Debug")

:csharp_done

rem ---------- Compute C# output dir
set "TFM=net9.0-windows10.0.19041.0"
if /i "%ARCH%"=="x64"   set "RID=win-x64"
if /i "%ARCH%"=="x86"   set "RID=win-x86"
if /i "%ARCH%"=="arm64" set "RID=win-arm64"
set "OUTPUT_DIR=ui\UniversalAnalogInputUI\bin\%CONFIG%\%TFM%\%RID%"

echo.
call :echoInfo "[5/6] Ensuring output directory..."
if not exist "%OUTPUT_DIR%" (
  mkdir "%OUTPUT_DIR%" %TEE% || call :die "Cannot create output directory: %OUTPUT_DIR%"
)
call :echoOk "OK Output directory ready"

rem ---------- Copy DLL to output directories
echo.
call :echoInfo "[6/6] Copying Rust DLL to output directories..."

rem Copy to .NET output directory
copy /y "%DLL_PATH%" "%OUTPUT_DIR%\" %TEE% || call :echoWarn "[WARN] Failed to copy DLL to .NET output directory"
if exist "%OUTPUT_DIR%\universal_analog_input.dll" (
  call :echoOk "OK DLL copied to %OUTPUT_DIR%"
) else (
  call :echoWarn "[WARN] DLL missing in .NET output dir"
)

rem Also copy to UI project directory for compatibility
set "UI_PROJECT_DIR=ui\UniversalAnalogInputUI"
copy /y "%DLL_PATH%" "%UI_PROJECT_DIR%\" %TEE% >nul 2>&1
if exist "%UI_PROJECT_DIR%\universal_analog_input.dll" (
  call :echoOk "OK DLL also copied to %UI_PROJECT_DIR%"
) else (
  call :echoWarn "[WARN] DLL copy to UI project dir failed"
)

rem ---------- Summary
echo.
call :echoOk "========================================"
call :echoOk "SUCCESS Build completed successfully!"
call :echoOk "========================================"
echo.
echo(Build Summary:
echo(  Configuration : %CONFIG%
echo(  Architecture  : %ARCH%
echo(  Rust DLL      : %DLL_PATH%
echo(  C# Output     : %OUTPUT_DIR%
echo.

rem ---------- Optional run
if /i "%RUN_AFTER%"=="true" (
  call :echoInfo "Launching app..."
  pushd "ui" >nul || call :die "Cannot cd to ui for running"
  dotnet run --project UniversalAnalogInputUI --no-restore -c %CONFIG% %TEE% || (
    popd >nul
    call :die "dotnet run failed"
  )
  popd >nul
)

popd >nul
exit /b 0
