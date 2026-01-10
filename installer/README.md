# Universal Analog Input - Installer

Inno Setup installer for Universal Analog Input with automatic dependency management.

## Quick Build

```powershell
# Build installer (includes preparation)
.\scripts\build-installer.ps1 -Version "1.0.0"

# Or step by step
.\scripts\prepare-installer.ps1    # Prepare files
.\scripts\build-installer.ps1 -Version "1.0.0" -SkipPreparation
```

**Output:** `artifacts\installer\UniversalAnalogInput-Setup-v1.0.0.exe`

## Prerequisites

1. **Inno Setup 6.4+** - Download from https://github.com/jrsoftware/issrc/releases/latest
2. **Built Application** - Run `.\scripts\package.ps1` first

## What It Installs

- Core application files
- ViGEm Bus Driver (auto-detected, silent install if missing)
- Wooting Analog SDK (auto-detected, silent install if missing)
- Start menu shortcuts
- Optional desktop shortcut
- Optional Windows startup entry

## Directory Structure

```
installer/
├── setup.iss              # Inno Setup script
└── dependencies/          # Downloaded installers (auto-created)
    ├── ViGEmBus_*.exe
    └── wooting-analog-sdk-*.msi

artifacts/
├── package/               # Built app (from package.ps1)
└── installer/             # Compiled installers
    └── UniversalAnalogInput-Setup-v*.exe
```

## Scripts

### prepare-installer.ps1
Prepares all files for the installer.

**Options:**
- `-SkipBuild` - Use existing package
- `-SkipDependencies` - Use existing dependencies
- `-Force` - Re-download dependencies

**What it does:**
1. Builds application via `package.ps1`
2. Downloads ViGEm Bus Driver from GitHub
3. Downloads Wooting Analog SDK from GitHub
4. Verifies prerequisites

### build-installer.ps1
Compiles the Inno Setup installer.

**Options:**
- `-Version "x.x.x"` - Set version (default: "1.0.0")
- `-SkipPreparation` - Don't run prepare-installer.ps1

**What it does:**
1. Runs prepare-installer.ps1 (unless skipped)
2. Updates version in setup.iss
3. Compiles installer with Inno Setup
4. Verifies output

## Silent Installation

```powershell
# Full silent install
UniversalAnalogInput-Setup-v1.0.0.exe /VERYSILENT /SUPPRESSMSGBOXES

# Silent with custom directory
UniversalAnalogInput-Setup-v1.0.0.exe /VERYSILENT /DIR="C:\Custom\Path"

# Core only (no dependencies)
UniversalAnalogInput-Setup-v1.0.0.exe /VERYSILENT /COMPONENTS="core"
```

## Troubleshooting

**Inno Setup not found:**
- Install from https://github.com/jrsoftware/issrc/releases/latest
- Or add `iscc.exe` to PATH

**Dependency download failed:**
- Manually download from GitHub releases
- Place in `installer\dependencies\`
- ViGEm: https://github.com/nefarius/ViGEmBus/releases/latest
- Wooting SDK: https://github.com/WootingKb/wooting-analog-sdk/releases/latest

**Package not found:**
- Run `.\scripts\package.ps1` first

## Customization

Edit `installer\setup.iss` to modify:
- Application name/version/publisher
- Installation paths
- Components
- Dependencies

See [Inno Setup documentation](https://jrsoftware.org/ishelp/) for details.
