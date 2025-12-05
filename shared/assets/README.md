# Shared Assets

This directory contains assets shared between the Rust tray application and the C# WinUI 3 UI.

## Icon

**File:** `icon.ico`

**Usage:**
- **Tray Application (Rust):** Embedded via `build.rs` using `winres` at compile time
- **UI Application (C#):** Linked via `.csproj` using `<Content Include="..\..\shared\assets\icon.ico">`

**Format:** Standard Windows ICO format with multiple resolutions (16x16, 32x32, 48x48, 256x256)

## Updating the Icon

When updating `icon.ico`:
1. Replace the file in `shared/assets/`
2. Rebuild both projects:
   - Rust: `cargo build --release`
   - C#: `dotnet build`
3. The icon will be automatically embedded in both executables

## Build Integration

The icon is automatically included in:
- `uai-tray.exe` - Via Rust build script
- `UniversalAnalogInputUI.exe` - Via MSBuild Content linking
- Package output - Via `scripts/package.ps1`
