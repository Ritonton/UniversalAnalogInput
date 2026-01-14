# Changelog

All notable changes to Universal Analog Input are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.0.1] - 2026-01-14

### Added

#### Error Monitoring & Crash Reporting
- **Sentry Integration** - Automatic crash reporting to improve stability and fix bugs faster
  - Enabled by default in official releases (disabled in source builds)
  - Captures application crashes, stack traces, and system information
  - Session tracking for release health monitoring
  - Zero collection of personal information (send_default_pii: false)
  - Automatic session persistence for abnormal termination detection

#### Legal Documentation & Compliance
- **PRIVACY_POLICY.md** - Comprehensive GDPR-compliant privacy policy
  - Clear distinction between open-source (source build) and official release versions
  - Complete data collection transparency
  - User rights and GDPR compliance information
  - Data Processing Agreement (DPA) reference

- **TERMS_OF_SERVICE.md** - Complete terms of service
  - Section 3: Detailed Sentry integration explanation
  - Clear instructions for disabling crash reporting
  - User data rights and control
  - Data Processing Agreement reference

- **THIRD_PARTY_LICENSES.md** - Updated with Sentry and dotenvy
  - All dependencies listed with licenses
  - External services documentation
  - License compatibility summary

#### Deployment & Release Management
- **DEPLOYMENT.md** - Comprehensive release checklist
  - Pre-release verification steps
  - Sentry configuration procedures
  - Legal compliance checklist
  - Testing procedures
  - Post-release monitoring guidelines

#### Installer Integration
- **installer/TERMS_OF_SERVICE.txt** - Simple, readable terms for installer
  - Plain text format for Inno Setup display
  - Clear sections explaining Sentry
  - Instructions for disabling crash reporting
  - All legal information accessible to users

- **Inno Setup Configuration** - Legal page in installer
  - Displays terms before installation
  - User must accept before proceeding
  - Copies legal documentation to installation directory
  - Includes production Sentry configuration (.env file)

#### Configuration Files
- **deploy/.env** - Production Sentry configuration
  - Contains public send-only DSN keys
  - Production environment setting
  - Ignored in .gitignore for security

### Changed

#### Documentation Updates
- **README.md** - Added comprehensive Privacy & Telemetry section
  - Clear distinction: "Built from Source" vs "Official Releases"
  - Instructions for disabling Sentry in both scenarios
  - How to control Sentry via environment variables
  - Links to legal documents

- **LICENSE** - Updated copyright years to 2025-2026

### Technical Details

#### Sentry Configuration (Rust & C#)
- **Native (Rust)**: `sentry` crate with automatic panic capturing
  - Auto session tracking enabled
  - Static backtrace collection
  - Environment-based DSN configuration via dotenvy

- **UI (C#)**: Sentry.Serilog integration with WinUI 3
  - Manual crash hook for DispatcherQueueTimer exceptions
  - Session persistence via CacheDirectoryPath
  - Proper Flush() calls on application termination
  - Debug logging for startup sequence

#### Data Privacy
- No personally identifiable information (PII) collected
- Keyboard input NOT captured
- Game names/file paths NOT included
- Only crash data, stack traces, version, and OS info sent
- HTTPS encryption for all data transmission
- 30-day default retention (configurable via Sentry)

#### User Control
- **Disable Sentry (Official Release)**:
  1. Edit `%LOCALAPPDATA%\UniversalAnalogInput\.env`
  2. Comment out or delete DSN lines
  3. Or delete entire `.env` file

- **Disable Sentry (Source Build)**:
  1. Don't set `UI_SENTRY_DSN` and `NATIVE_SENTRY_DSN` environment variables
  2. Or leave them empty/unset

### Fixed

- Console output in debug mode now displays correctly
- Exception handling in DispatcherQueueTimer now properly captures and logs crashes
- Session persistence now correctly detects abnormal terminations

### Compliance

- ✅ GDPR compliant (Article 28 - Data Processor relationship)
- ✅ User rights documented (access, rectification, erasure, portability)
- ✅ Data Processing Agreement referenced
- ✅ Legitimate interests documented (GDPR Article 6(1)(f))
- ✅ CCPA compliant (California Privacy Rights)
- ✅ Transparent data handling practices
- ✅ MIT License maintained

### Dependencies Added

#### Rust
- `sentry` ^0.32 - Error monitoring and crash reporting
- `dotenvy` ^0.15 - Environment variable loading

#### C#
- `Sentry.Serilog` ^5.14 - Sentry integration for .NET
- `DotNetEnv` ^3.1.2 - Environment variable loading

---

## [1.0.0] - 2025-12-05

### Initial Release

- Core analog input mapping engine (120+ FPS capable)
- WinUI 3 configuration interface
- Wooting Analog SDK integration
- ViGEm virtual gamepad emulation
- Profile management with sub-profiles
- Custom response curves with Hermite interpolation
- Dead zone configuration
- Hotkey system for profile switching
- Real-time input visualization
- Profile import/export functionality
- Multi-language support (English, French)
- System tray operation
- Performance metrics monitoring

---

## Version History

| Version | Release Date | Status | Key Features |
|---------|--------------|--------|--------------|
| 1.0.1   | 2026-01-14   | Latest | Sentry integration, Legal docs, GDPR compliance |
| 1.0.0   | 2025-12-05   | Archive | Core functionality, UI, Profiles, Hotkeys |

---

## Upgrade Guide

### From 1.0.0 to 1.0.1

**Breaking Changes**: None

**New Features**:
- Automatic crash reporting (can be disabled)
- New legal documents included in installation
- Improved error tracking

**Installation**:
1. Uninstall version 1.0.0
2. Install version 1.0.1
3. Accept new Terms of Service during installation
4. Review Privacy Policy (included in installation folder)

**Note**: Sentry is **enabled by default** in v1.0.1. If you prefer no crash reporting:
1. Open `%LOCALAPPDATA%\UniversalAnalogInput\.env`
2. Comment out or delete the `UI_SENTRY_DSN` and `NATIVE_SENTRY_DSN` lines
3. Restart the application

---

## Documentation

For more information, see:
- [README.md](README.md) - Feature overview and usage
- [PRIVACY_POLICY.md](PRIVACY_POLICY.md) - Data handling and user rights
- [TERMS_OF_SERVICE.md](TERMS_OF_SERVICE.md) - Legal terms and conditions
- [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) - Dependency licenses
- [DEPLOYMENT.md](DEPLOYMENT.md) - Release and deployment procedures

---

**Maintained by**: Henri DELEMAZURE
**License**: MIT
**Repository**: https://github.com/Ritonton/UniversalAnalogInput
