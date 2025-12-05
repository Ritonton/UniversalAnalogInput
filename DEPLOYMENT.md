# Deployment Guide

## Package Structure

The release package follows this structure for optimal organization:

```
UniversalAnalogInput/
├── UniversalAnalogInput.exe          # Main tray application (Rust core, entry point)
├── README.txt                        # User documentation
├── LICENSE.txt                       # MIT License
│
├── ui/                               # WinUI 3 UI application and dependencies
│   ├── UniversalAnalogInputUI.exe    # Configuration interface
│   ├── UniversalAnalogInputUI.dll
│   ├── *.dll                         # WindowsAppSDK and WinUI dependencies
│   ├── resources.pri                 # WinUI resources
│   └── Assets/                       # UI assets
│       └── shadow.png
│
└── profiles/                         # Default profile template
    └── default.json                  # Copied to AppData on first run
```

## Build Process

### 1. Quick Build (Development)

```powershell
# Build both Rust core and C# UI
.\scripts\build.ps1
```

This compiles:
- Rust core engine → `native/target/debug/uai-tray.exe`
- C# WinUI interface → `ui/UniversalAnalogInputUI/bin/Debug/`

### 2. Package for Distribution

```powershell
# Create self-contained package with .NET runtime included
.\scripts\package.ps1 -SelfContained

# Or framework-dependent (requires .NET 9 on user machine)
.\scripts\package.ps1
```

Output: `artifacts/UniversalAnalogInput-<timestamp>/`

### 3. Create Installer (Recommended)

**Inno Setup Installer** (Coming Soon)

The installer will automatically:
- Install UniversalAnalogInput application
- Download and install ViGEm Bus Driver if not present
- Download and install Wooting Analog SDK with plugin support
- Create Start Menu shortcuts
- Add uninstaller to Windows Programs & Features

**Manual ZIP Distribution**

```powershell
# Create release ZIP from packaged artifacts
.\scripts\create-release.ps1 -Version "1.0.0"
```

Output: `UniversalAnalogInput-v1.0.0.zip`

## Installation Methods

### Method 1: Inno Setup Installer (Recommended - Coming Soon)

**For end users:**
1. Download `UniversalAnalogInput-Setup-v1.0.0.exe`
2. Run the installer
3. Follow the installation wizard
4. Dependencies (ViGEm, Wooting SDK) are installed automatically
5. Launch from Start Menu or system tray

**Installer Features:**
- Automatic dependency installation
- Start Menu integration
- Clean uninstallation
- Version management

### Method 2: Manual ZIP Installation (Current)

**For end users:**
1. Ensure prerequisites are installed:
   - ViGEm Bus Driver: https://github.com/nefarius/ViGEmBus/releases
   - Wooting Analog SDK: https://github.com/WootingKb/wooting-analog-sdk
2. Extract ZIP to desired location (e.g., `C:\Program Files\UniversalAnalogInput\`)
3. Run `UniversalAnalogInput.exe`
4. System tray icon appears
5. Configuration UI launches automatically on first run

**Requirements:**
- Windows 10 version 1903 (build 19041) or higher
- Analog keyboard (Wooting or plugin-supported)
- ViGEm Bus Driver (must be pre-installed)
- Wooting Analog SDK with plugin support (must be pre-installed)

## Architecture Benefits

### Clean Structure
- **Single entry point**: `UniversalAnalogInput.exe` at the root (Rust tray app)
- **Isolated UI**: All WinUI dependencies contained in `ui/` subdirectory
- **No DLL pollution**: Root directory stays clean
- **Clear separation**: Core engine, UI, user data, and logs are separated

### IPC Architecture
- **Rust Core** (`UniversalAnalogInput.exe`): Runs as system tray application
  - Handles analog input processing
  - Manages gamepad emulation (ViGEm)
  - Stores profiles and mappings
  - Provides IPC server (Named Pipes)

- **C# UI** (`ui/UniversalAnalogInputUI.exe`): Launches on-demand
  - WinUI 3 configuration interface
  - Connects to Rust core via IPC client
  - Real-time input visualization
  - Profile and mapping management

### Runtime Behavior

**Installation Directory** (e.g., `C:\Program Files\UniversalAnalogInput\`):
- Core searches for UI in: `./ui/UniversalAnalogInputUI.exe`
- Default profile template: `./profiles/default.json` (copied to AppData on first run)

**User Data Directory** (`%APPDATA%\UniversalAnalogInput\`):
- User profiles: `C:\Users\<Username>\AppData\Roaming\UniversalAnalogInput\profiles\*.json`

**Application Data Directory** (`%LOCALAPPDATA%\UniversalAnalogInput\`):
- Crash logs: `C:\Users\<Username>\AppData\Local\UniversalAnalogInput\crash.log`
- Settings: `C:\Users\<Username>\AppData\Local\UniversalAnalogInput\settings.txt`
- Theme: `C:\Users\<Username>\AppData\Local\UniversalAnalogInput\theme.txt`

**IPC Communication**:
- Windows Named Pipes: `\\.\pipe\universal_analog_input`

### Development vs Production

**Development mode** (debug builds):
- Rust: `cargo run --bin uai-tray`
- UI searched in: `<project>/ui/UniversalAnalogInputUI/bin/Debug/...`
- Fast iteration with hot reload
- Debug symbols included

**Production mode** (release builds):
- Rust: Optimized binary with static CRT linking
- UI searched in: `./ui/UniversalAnalogInputUI.exe`
- Optimized binaries, minimal size
- No debug symbols

## Distribution Checklist

### For ZIP Distribution (Current)

- [ ] Update version in `native/Cargo.toml`
- [ ] Update version in `ui/UniversalAnalogInputUI/UniversalAnalogInputUI.csproj`
- [ ] Run `.\scripts\package.ps1 -Clean -SelfContained`
- [ ] Test the packaged executables:
  - [ ] Core launches and tray icon appears
  - [ ] UI opens from tray menu
  - [ ] IPC communication works
  - [ ] Mapping engine functions correctly
  - [ ] Profiles save/load properly
- [ ] Create ZIP: `.\scripts\create-release.ps1 -Version "x.y.z"`
- [ ] Test ZIP extraction and installation
- [ ] Upload to GitHub Releases with release notes

### For Installer Distribution (Future)

- [ ] Update version numbers
- [ ] Build release package
- [ ] Test package
- [ ] Compile Inno Setup installer script
- [ ] Test installer:
  - [ ] Fresh install
  - [ ] Upgrade from previous version
  - [ ] Uninstall
  - [ ] Dependency detection/installation
- [ ] Sign installer (optional, for trusted publisher status)
- [ ] Upload to GitHub Releases

## Technical Notes

### Static Linking
- **Rust executables** use static MSVC runtime (`+crt-static`)
- No dependency on `vcruntime140.dll` or Visual C++ Redistributables
- Slightly larger binaries but better portability

### Self-Contained Mode
- Includes complete .NET 9.0 runtime
- No user installation of .NET required
- UI works on any Windows 10+ system

### Framework-Dependent Mode (Alternative)
- Requires .NET 9.0 Runtime on user machine
- Smaller package size (~5 MB UI folder)
- Suitable for managed environments with .NET pre-installed

### Language Folders
- Build scripts automatically remove non-English language resources
- Only `en-US` retained to minimize package size
- Saves ~5-10 MB per language removed

### Icon Embedding
- Icons embedded directly in both executables
- No external `.ico` files required
- Professional appearance in Task Manager and Explorer

### Data Storage Locations

The application uses standard Windows directories for user data:

**Roaming Data** (`%APPDATA%\UniversalAnalogInput\`):
- Purpose: User profiles that should sync across machines in domain environments
- Location: `C:\Users\<Username>\AppData\Roaming\UniversalAnalogInput\`
- Contents:
  - `profiles/*.json` - User-created game profiles and mappings

**Local Data** (`%LOCALAPPDATA%\UniversalAnalogInput\`):
- Purpose: Machine-specific settings and logs
- Location: `C:\Users\<Username>\AppData\Local\UniversalAnalogInput\`
- Contents:
  - `crash.log` - Application crash logs and error tracking
  - `settings.txt` - Application settings
  - `theme.txt` - UI theme preference (Light/Dark/System)

**Installation Directory**:
- Purpose: Application binaries and default templates
- Location: User-chosen (e.g., `C:\Program Files\UniversalAnalogInput\`)
- Contents:
  - Executables and DLLs
  - `profiles/default.json` - Default profile template (copied to AppData on first run)

## Version Management

Version numbers are managed in two locations:

1. **Rust Core**: `native/Cargo.toml`
   ```toml
   [package]
   version = "1.0.0"
   ```

2. **C# UI**: `ui/UniversalAnalogInputUI/UniversalAnalogInputUI.csproj`
   ```xml
   <PropertyGroup>
     <Version>1.0.0</Version>
   </PropertyGroup>
   ```

The UI displays its version on the Settings page via reflection.

## Distribution Platforms

### GitHub Releases (Primary)
- Source code (automatic)
- Pre-built binaries (ZIP or installer)
- Release notes and changelog
- Links to dependencies (ViGEm, Wooting SDK)

## Security Considerations

### Code Signing (Recommended for Future)
- Sign executables with code signing certificate
- Prevents Windows SmartScreen warnings
- Establishes trusted publisher status
- Required for Microsoft Store distribution

### Dependencies
- ViGEm driver is signed by Nefarius
- Wooting SDK is open source (MPL-2.0)
- All dependencies verified in `THIRD_PARTY_LICENSES.md`

## Troubleshooting

### Common Issues

**"ViGEm driver not found"**
- Install ViGEm Bus Driver before running
- Installer will handle this automatically (future)

**"Wooting SDK not detected"**
- Install Wooting Analog SDK with plugin support
- Check SDK installation path

**"UI doesn't launch"**
- Verify `ui/` folder exists alongside executable
- Check `ui/UniversalAnalogInputUI.exe` is present
- Review logs in `%LOCALAPPDATA%\UniversalAnalogInput\crash.log`

**"IPC connection failed"**
- Ensure only one instance of core is running
- Check Windows Firewall isn't blocking Named Pipes
- Review logs for connection errors in `%LOCALAPPDATA%\UniversalAnalogInput\crash.log`

**"Profiles not saving"**
- Check write permissions for `%APPDATA%\UniversalAnalogInput\profiles\`
- Verify user has access to AppData directories
- Review crash logs for I/O errors

**"Settings/Theme not persisting"**
- Check write permissions for `%LOCALAPPDATA%\UniversalAnalogInput\`
- Ensure `settings.txt` and `theme.txt` are not read-only
- Try running as administrator to reset permissions

---

**Last Updated**: 2025-12-05
