; Universal Analog Input - Inno Setup Installation Script
; =======================================================
; Installer with dependency management for Wooting SDK and ViGEm Bus Driver
;
; Requirements:
; - Inno Setup 6.4 or later
; - Run scripts\prepare-installer.ps1 first to download dependencies
;
; Build command: iscc installer\setup.iss

#define AppName "Universal Analog Input"
#define AppVersion "1.0.1"
#define AppPublisher "Henri DELEMAZURE"
#define AppURL "https://github.com/Ritonton/UniversalAnalogInput"
#define AppExeName "UniversalAnalogInput.exe"
#define AppId "{B8F3E4D1-7A2C-4F9B-8E1D-3C5A6B9D2F7E}"

[Setup]
; Basic application info
AppId={{#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
AppCopyright=Copyright (C) 2025-2026 {#AppPublisher}

; Installation directories
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=no

; Upgrade and modification support
UsePreviousAppDir=yes
UsePreviousGroup=yes
CreateUninstallRegKey=yes
UpdateUninstallLogAppName=yes

; Output configuration
OutputDir=..\artifacts\installer
OutputBaseFilename=UniversalAnalogInput-Setup-v{#AppVersion}
SetupIconFile=..\shared\assets\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=2

; Windows version requirements
MinVersion=10.0.19041
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Privileges - Force installation for all users (required for driver installation)
; NOTE: Admin privileges are required to install ViGEm and Wooting SDK drivers
; Per-user areas are only accessed during uninstallation for cleanup (not during install)
; The application creates user data directories on first run with proper permissions
PrivilegesRequired=admin
AlwaysShowDirOnReadyPage=yes

; UI Configuration
WizardStyle=modern dynamic
WizardSizePercent=110,100

; Custom wizard images
; Light mode images
WizardImageFile=images\wizard-modern.png
WizardSmallImageFile=images\wizard-small.png
; Dark mode images
WizardImageFileDynamicDark=images\wizard-modern-dark.png
WizardSmallImageFileDynamicDark=images\wizard-small-dark.png

; Welcome and Finish page customization
DisableWelcomePage=no
DisableFinishedPage=no

; License/Terms of Service page
LicenseFile=TERMS_OF_SERVICE.txt

; Language
ShowLanguageDialog=auto

; Visual enhancements
SetupLogging=yes
UsePreviousSetupType=yes
UsePreviousTasks=yes

; Uninstall
UninstallDisplayName={#AppName}
UninstallFilesDir={app}\uninstall

; Miscellaneous
AllowNoIcons=yes
AlwaysShowComponentsList=yes
ShowComponentSizes=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Messages]
; Custom welcome and finish messages for modern UI
WelcomeLabel1=Welcome to [name] Setup
WelcomeLabel2=This will install [name/ver] on your computer.%n%nUniversal Analog Input transforms your analog keyboard into a virtual gamepad for smooth control in any game.%n%nNOTE: Non-Wooting keyboard users must also install the Universal Analog Plugin with the SDK (see Components page).%n%nIt is recommended that you close all other applications before continuing.

FinishedHeadingLabel=Completing [name] Setup
FinishedLabel=Setup has finished installing [name] on your computer.%n%n[name] is now ready to use. Click Finish to exit Setup.
FinishedLabelNoIcons=Setup has finished installing [name] on your computer.

; Custom button labels for modern feel
ButtonNext=&Next >
ButtonInstall=&Install
ButtonFinish=&Finish
ButtonCancel=Cancel
ButtonBack=< &Back

; Status messages during installation
StatusExtractFiles=Extracting files...
StatusCreateDirs=Creating directories...
StatusCreateIcons=Creating shortcuts...
StatusCreateIniEntries=Creating configuration entries...
StatusRegisterFiles=Registering system files...
StatusRunProgram=Finalizing installation...

[CustomMessages]
; Custom messages for dependency installation
english.DependencyStatus=Checking dependencies...
english.InstallingViGEm=Installing ViGEm Bus Driver (Virtual Gamepad)...
english.InstallingWooting=Installing Wooting Analog SDK...
english.DependencyComplete=Dependencies installed successfully
english.SkippingInstalled=Already installed, skipping...

french.DependencyStatus=Vérification des dépendances...
french.InstallingViGEm=Installation de ViGEm Bus Driver (Manette Virtuelle)...
french.InstallingWooting=Installation de Wooting Analog SDK...
french.DependencyComplete=Dépendances installées avec succès
french.SkippingInstalled=Déjà installé, ignoré...

[Types]
Name: "full"; Description: "Full installation (Recommended)"
Name: "minimal"; Description: "Minimal installation (Manual dependency setup required)"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "core"; Description: "Core Application Files (always installed)"; Types: full minimal custom; Flags: fixed
Name: "deps"; Description: "Dependencies (required for functionality)"; Types: full custom
Name: "deps\vigem"; Description: "ViGEm Bus Driver - Virtual Gamepad Emulation"; Types: full custom; Flags: checkablealone
Name: "deps\wooting"; Description: "Wooting Analog SDK - Analog Input Support (REQUIRED for all keyboards. Non-Wooting users must also install the Universal Analog Plugin WITH this SDK from: github.com/AnalogSense/universal-analog-plugin)"; Types: full custom; Flags: checkablealone

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Files]
; Main application executable (tray)
Source: "..\artifacts\package\UniversalAnalogInput.exe"; DestDir: "{app}"; Flags: ignoreversion; Components: core

; UI application and all dependencies
Source: "..\artifacts\package\ui\*"; DestDir: "{app}\ui"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: core

; Default profile template
Source: "..\artifacts\package\profiles\*"; DestDir: "{app}\profiles"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: core

; Documentation and Legal
Source: "..\artifacts\package\README.txt"; DestDir: "{app}"; Flags: ignoreversion; Components: core
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion; Components: core
Source: "..\TERMS_OF_SERVICE.md"; DestDir: "{app}"; Flags: ignoreversion; Components: core
Source: "..\PRIVACY_POLICY.md"; DestDir: "{app}"; Flags: ignoreversion; Components: core
Source: "..\THIRD_PARTY_LICENSES.md"; DestDir: "{app}"; Flags: ignoreversion; Components: core

; Sentry Configuration (Crash Reporting)
Source: "..\deploy\.env"; DestDir: "{app}"; DestName: ".env"; Flags: ignoreversion; Components: core

; Dependency installers (downloaded by prepare-installer.ps1)
Source: "dependencies\ViGEmBus_*.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Components: deps\vigem; Check: not IsViGEmInstalled
Source: "dependencies\wooting_analog_sdk-*.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall; Components: deps\wooting; Check: not IsWootingSDKInstalled

[Icons]
; Start Menu shortcuts
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Comment: "Launch {#AppName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{group}\README"; Filename: "{app}\README.txt"

; Desktop shortcut
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Install ViGEm Bus Driver (silent installation)
Filename: "{tmp}\{code:GetViGEmInstaller}"; Parameters: "/qn /norestart"; StatusMsg: "Installing ViGEm Bus Driver..."; Flags: waituntilterminated runhidden; Components: deps\vigem; Check: not IsViGEmInstalled

; Install Wooting Analog SDK (silent installation)
Filename: "msiexec.exe"; Parameters: "/i ""{tmp}\{code:GetWootingInstaller}"" /qn /norestart"; StatusMsg: "Installing Wooting Analog SDK..."; Flags: waituntilterminated runhidden; Components: deps\wooting; Check: not IsWootingSDKInstalled

; Launch application after installation
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop the application before uninstall
Filename: "taskkill"; Parameters: "/F /IM {#AppExeName}"; Flags: runhidden; RunOnceId: "StopTray"
Filename: "taskkill"; Parameters: "/F /IM UniversalAnalogInputUI.exe"; Flags: runhidden; RunOnceId: "StopUI"

[UninstallDelete]
; Clean up user data (optional - prompts user)
Type: filesandordirs; Name: "{localappdata}\UniversalAnalogInput"
Type: filesandordirs; Name: "{userappdata}\UniversalAnalogInput"

[Code]
var
  DependenciesPage: TOutputProgressWizardPage;
  ViGEmInstallerFile: string;
  WootingInstallerFile: string;
  IsUpgrade: Boolean;
  PreviousVersion: string;
  UninstallString: string;

// ============================================================================
// Dependency Detection Functions
// ============================================================================

// Check if ViGEm Bus Driver is installed
// Uses reliable detection methods instead of Services registry key (which persists after uninstall)
function IsViGEmInstalled: Boolean;
var
  RegValue: string;
begin
  Result := False;

  // Method 1: Check official ViGEm version registry key (primary method)
  // On 64-bit systems, it's in WOW6432Node because the installer is 32-bit
  if RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\WOW6432Node\Nefarius Software Solutions e.U.\ViGEm Bus Driver',
    'Version', RegValue) then
  begin
    Log('ViGEm Bus Driver detected via version key (64-bit): ' + RegValue);
    Result := True;
    Exit;
  end;

  // Fallback for 32-bit systems (without WOW6432Node)
  if RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Nefarius Software Solutions e.U.\ViGEm Bus Driver',
    'Version', RegValue) then
  begin
    Log('ViGEm Bus Driver detected via version key (32-bit): ' + RegValue);
    Result := True;
    Exit;
  end;

  // Method 2: Check Windows Uninstall registry (fallback)
  if RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{966606F3-2745-49E9-BF15-5C3EAA4E9077}',
    'DisplayName', RegValue) then
  begin
    Log('ViGEm Bus Driver detected via uninstall key (64-bit): ' + RegValue);
    Result := True;
    Exit;
  end;

  // Check WOW6432Node uninstall location
  if RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{966606F3-2745-49E9-BF15-5C3EAA4E9077}',
    'DisplayName', RegValue) then
  begin
    Log('ViGEm Bus Driver detected via uninstall key (WOW64): ' + RegValue);
    Result := True;
    Exit;
  end;

  Log('ViGEm Bus Driver is not installed');
end;

// Check if Wooting Analog SDK is installed
function IsWootingSDKInstalled: Boolean;
var
  RegValue: string;
begin
  // Check if SDK updater executable exists (most reliable method)
  Result := FileExists(ExpandConstant('{pf}\wooting-analog-sdk\wooting-analog-sdk-updater.exe'));

  if not Result then
  begin
    // Check registry for Wooting SDK installation
    Result := RegQueryStringValue(HKEY_LOCAL_MACHINE,
      'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{Wooting Analog SDK}',
      'DisplayName', RegValue);
  end;

  if not Result then
  begin
    // Try alternative registry location
    Result := RegQueryStringValue(HKEY_LOCAL_MACHINE,
      'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{Wooting Analog SDK}',
      'DisplayName', RegValue);
  end;

  if not Result then
  begin
    // Check if SDK DLL exists in common locations
    Result := FileExists(ExpandConstant('{pf}\Wooting\wooting-analog-sdk\wooting_analog_sdk.dll'));
  end;

  if Result then
    Log('Wooting Analog SDK is already installed')
  else
    Log('Wooting Analog SDK is not installed');
end;

// ============================================================================
// Installation Detection Functions
// ============================================================================

// Check if application is already installed
function IsAppInstalled(var Version: string; var Uninstall: string): Boolean;
var
  RegValue: string;
begin
  Result := False;

  // Check 64-bit registry location
  if RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1',
    'DisplayVersion', RegValue) then
  begin
    Version := RegValue;
    RegQueryStringValue(HKEY_LOCAL_MACHINE,
      'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1',
      'UninstallString', Uninstall);
    Result := True;
    Exit;
  end;

  // Check 32-bit registry location on 64-bit system
  if RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1',
    'DisplayVersion', RegValue) then
  begin
    Version := RegValue;
    RegQueryStringValue(HKEY_LOCAL_MACHINE,
      'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1',
      'UninstallString', Uninstall);
    Result := True;
  end;
end;

// Handle existing installation
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
  MsgResult: Integer;
  ViGEmStatus: string;
  WootingStatus: string;
  Message: string;
begin
  Result := True;
  IsUpgrade := False;

  if IsAppInstalled(PreviousVersion, UninstallString) then
  begin
    IsUpgrade := True;

    // Check dependency status for informative message
    if IsViGEmInstalled then
      ViGEmStatus := 'installed'
    else
      ViGEmStatus := 'NOT installed';

    if IsWootingSDKInstalled then
      WootingStatus := 'installed'
    else
      WootingStatus := 'NOT installed';

    Message := 'Universal Analog Input ' + PreviousVersion + ' is already installed.' + #13#10 + #13#10 +
               'Current dependencies:' + #13#10 +
               '  - ViGEm Bus Driver: ' + ViGEmStatus + #13#10 +
               '  - Wooting Analog SDK: ' + WootingStatus + #13#10 + #13#10 +
               'What would you like to do?' + #13#10 + #13#10 +
               'YES    - Continue to modify/repair installation' + #13#10 +
               '         (you can add missing dependencies or repair files)' + #13#10 + #13#10 +
               'NO     - Uninstall current version first' + #13#10 +
               '         (clean installation)' + #13#10 + #13#10 +
               'CANCEL - Exit setup';

    MsgResult := MsgBox(Message, mbConfirmation, MB_YESNOCANCEL);

    case MsgResult of
      IDYES:
        begin
          // Continue with upgrade/repair - user will see component selection
          Log('User chose to modify/repair installation');
          Log('ViGEm status: ' + ViGEmStatus);
          Log('Wooting SDK status: ' + WootingStatus);
          Result := True;
          // Note: User will proceed to component selection page where they can
          // choose to install missing dependencies
        end;
      IDNO:
        begin
          // Uninstall previous version
          Log('User chose to uninstall previous version');
          if Exec(RemoveQuotes(UninstallString), '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, ErrorCode) then
          begin
            if ErrorCode = 0 then
            begin
              Log('Previous version uninstalled successfully');
              IsUpgrade := False; // Now it's a fresh install
              Result := True;
            end
            else
            begin
              MsgBox('Uninstallation failed with error code ' + IntToStr(ErrorCode) + '.', mbError, MB_OK);
              Result := False;
            end;
          end
          else
          begin
            MsgBox('Failed to launch uninstaller.', mbError, MB_OK);
            Result := False;
          end;
        end;
      IDCANCEL:
        begin
          Log('User cancelled setup');
          Result := False;
        end;
    end;
  end;
end;

// ============================================================================
// Installer Filename Helper Functions
// ============================================================================

// Get ViGEm installer filename from dependencies folder
function GetViGEmInstaller(Param: string): string;
var
  FindRec: TFindRec;
begin
  if ViGEmInstallerFile = '' then
  begin
    if not FindFirst(ExpandConstant('{tmp}\ViGEmBus_*.exe'), FindRec) then
    begin
      Log('Error: ViGEm installer not found');
      Result := '';
    end
    else
    begin
      ViGEmInstallerFile := FindRec.Name;
      FindClose(FindRec);
    end;
  end;
  Result := ViGEmInstallerFile;
end;

// Get Wooting SDK installer filename from dependencies folder
function GetWootingInstaller(Param: string): string;
var
  FindRec: TFindRec;
begin
  if WootingInstallerFile = '' then
  begin
    if not FindFirst(ExpandConstant('{tmp}\wooting_analog_sdk-*.msi'), FindRec) then
    begin
      Log('Error: Wooting SDK installer not found');
      Result := '';
    end
    else
    begin
      WootingInstallerFile := FindRec.Name;
      FindClose(FindRec);
    end;
  end;
  Result := WootingInstallerFile;
end;

// ============================================================================
// Wizard Event Handlers
// ============================================================================

procedure InitializeWizard();
begin
  // Create custom page for dependency installation progress
  DependenciesPage := CreateOutputProgressPage('Installing Dependencies',
    'Please wait while Setup installs required components...');
end;

// Customize wizard based on upgrade/repair mode
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;

  // In upgrade mode, we could skip certain pages if needed
  // For now, show all pages to allow component modification
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ViGEmSelected, WootingSelected: Boolean;
  ViGEmNeeded, WootingNeeded: Boolean;
begin
  Result := '';

  // Check which dependencies are selected
  ViGEmSelected := WizardIsComponentSelected('deps\vigem');
  WootingSelected := WizardIsComponentSelected('deps\wooting');

  // Check which are actually needed
  ViGEmNeeded := ViGEmSelected and not IsViGEmInstalled;
  WootingNeeded := WootingSelected and not IsWootingSDKInstalled;

  // Show warning if no dependencies selected but needed
  if not ViGEmSelected and not WootingSelected then
  begin
    if MsgBox('Warning: No dependencies selected for installation.' + #13#10 + #13#10 +
              'Universal Analog Input requires:' + #13#10 +
              '  - ViGEm Bus Driver (for virtual gamepad emulation)' + #13#10 +
              '  - Wooting Analog SDK (for analog keyboard input)' + #13#10 + #13#10 +
              'The application will not work without these components.' + #13#10 + #13#10 +
              'Do you want to continue with manual installation?',
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := 'Installation cancelled. Please select dependencies or install them manually.';
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // User data directories will be created by the application on first run
    // This avoids permission issues when running installer as admin
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  // Show dependency status on "Ready to Install" page
  if CurPageID = wpReady then
  begin
    if WizardIsComponentSelected('deps\vigem') then
    begin
      if IsViGEmInstalled then
        Log('ViGEm: Already installed (will skip)')
      else
        Log('ViGEm: Will be installed');
    end;

    if WizardIsComponentSelected('deps\wooting') then
    begin
      if IsWootingSDKInstalled then
        Log('Wooting SDK: Already installed (will skip)')
      else
        Log('Wooting SDK: Will be installed');
    end;
  end;
end;

// ============================================================================
// Custom Uninstaller Confirmation
// ============================================================================

function InitializeUninstall(): Boolean;
var
  ViGEmInstalled: Boolean;
  WootingInstalled: Boolean;
  ViGEmUninstallString: string;
  WootingUninstallString: string;
  RemoveUserData: Integer;
  RemoveDependencies: Integer;
  ErrorCode: Integer;
  Message: string;
begin
  Result := True;

  Log('=== Starting Uninstall Process ===');

  // Check if dependencies are installed
  Log('Checking for installed dependencies...');
  ViGEmInstalled := IsViGEmInstalled;
  WootingInstalled := IsWootingSDKInstalled;

  if ViGEmInstalled then
    Log('ViGEm installed: True')
  else
    Log('ViGEm installed: False');

  if WootingInstalled then
    Log('Wooting SDK installed: True')
  else
    Log('Wooting SDK installed: False');

  // Ask about user data
  Log('Asking user about user data removal...');
  RemoveUserData := MsgBox(
    'Do you want to remove user profiles and settings?' + #13#10 + #13#10 +
    'Location: ' + ExpandConstant('{userappdata}\UniversalAnalogInput'),
    mbConfirmation, MB_YESNO or MB_DEFBUTTON2);

  if RemoveUserData = IDNO then
    Log('User data will be preserved')
  else
    Log('User data will be removed');

  // Ask about dependencies if any are installed
  if ViGEmInstalled or WootingInstalled then
  begin
    Log('At least one dependency is installed, asking user...');
    Message := 'Do you also want to uninstall the dependencies?' + #13#10 + #13#10;

    if ViGEmInstalled then
      Message := Message + '  - ViGEm Bus Driver (Virtual Gamepad)' + #13#10;

    if WootingInstalled then
      Message := Message + '  - Wooting Analog SDK (Analog Input)' + #13#10;

    Message := Message + #13#10 +
               'Warning: Other applications might be using these components.' + #13#10 +
               'Only remove them if you are sure they are not needed.';

    RemoveDependencies := MsgBox(Message, mbConfirmation, MB_YESNO or MB_DEFBUTTON2);
    Log('User dependency removal choice: ' + IntToStr(RemoveDependencies));

    if RemoveDependencies = IDYES then
    begin
      Log('User chose to remove dependencies');

      // Uninstall ViGEm if installed
      if ViGEmInstalled then
      begin
        Log('=== Attempting to uninstall ViGEm ===');
        Log('Searching for ViGEm uninstall string in registry...');

        if RegQueryStringValue(HKEY_LOCAL_MACHINE,
          'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{966606F3-2745-49E9-BF15-5C3EAA4E9077}',
          'UninstallString', ViGEmUninstallString) then
        begin
          Log('Found ViGEm uninstall string in 64-bit registry: ' + ViGEmUninstallString);
        end
        else if RegQueryStringValue(HKEY_LOCAL_MACHINE,
          'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{966606F3-2745-49E9-BF15-5C3EAA4E9077}',
          'UninstallString', ViGEmUninstallString) then
        begin
          Log('Found ViGEm uninstall string in 32-bit registry: ' + ViGEmUninstallString);
        end
        else
        begin
          Log('ERROR: Could not find ViGEm uninstall string in registry!');
          ViGEmUninstallString := '';
        end;

        if ViGEmUninstallString <> '' then
        begin
          // ViGEm uses MSI, extract the GUID and use msiexec
          if Pos('MsiExec.exe', ViGEmUninstallString) > 0 then
          begin
            Log('Executing ViGEm MSI uninstaller: msiexec.exe /X{966606F3-2745-49E9-BF15-5C3EAA4E9077} /qn');
            if Exec(ExpandConstant('{sys}\msiexec.exe'), '/X{966606F3-2745-49E9-BF15-5C3EAA4E9077} /qn', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode) then
            begin
              Log('ViGEm uninstaller executed, exit code: ' + IntToStr(ErrorCode));
              if ErrorCode = 0 then
                Log('ViGEm uninstalled successfully')
              else
                Log('WARNING: ViGEm uninstall returned non-zero error code: ' + IntToStr(ErrorCode));
            end
            else
              Log('ERROR: Failed to execute ViGEm uninstaller!');
          end
          else
          begin
            Log('Executing ViGEm uninstaller: ' + RemoveQuotes(ViGEmUninstallString) + ' /SILENT');
            if Exec(RemoveQuotes(ViGEmUninstallString), '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, ErrorCode) then
            begin
              Log('ViGEm uninstaller executed, exit code: ' + IntToStr(ErrorCode));
              if ErrorCode = 0 then
                Log('ViGEm uninstalled successfully')
              else
                Log('WARNING: ViGEm uninstall returned non-zero error code: ' + IntToStr(ErrorCode));
            end
            else
              Log('ERROR: Failed to execute ViGEm uninstaller!');
          end;
        end;
      end
      else
        Log('ViGEm not installed, skipping');


      // Uninstall Wooting SDK if installed
      if WootingInstalled then
      begin
        Log('=== Attempting to uninstall Wooting SDK ===');
        Log('Searching for Wooting SDK uninstall string in registry...');

        if RegQueryStringValue(HKEY_LOCAL_MACHINE,
          'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{4775AE15-6216-498C-8A0C-FFFFDEB36C35}',
          'UninstallString', WootingUninstallString) then
        begin
          Log('Found Wooting SDK uninstall string in 64-bit registry: ' + WootingUninstallString);
        end
        else if RegQueryStringValue(HKEY_LOCAL_MACHINE,
          'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{4775AE15-6216-498C-8A0C-FFFFDEB36C35}',
          'UninstallString', WootingUninstallString) then
        begin
          Log('Found Wooting SDK uninstall string in 32-bit registry: ' + WootingUninstallString);
        end
        else
        begin
          Log('ERROR: Could not find Wooting SDK uninstall string in registry!');
          WootingUninstallString := '';
        end;

        if WootingUninstallString <> '' then
        begin
          // Wooting SDK uses MSI, use /X for uninstall (not /I which is install!)
          if Pos('MsiExec.exe', WootingUninstallString) > 0 then
          begin
            Log('Executing Wooting SDK MSI uninstaller: msiexec.exe /X{4775AE15-6216-498C-8A0C-FFFFDEB36C35} /qn');
            if Exec(ExpandConstant('{sys}\msiexec.exe'), '/X{4775AE15-6216-498C-8A0C-FFFFDEB36C35} /qn', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode) then
            begin
              Log('Wooting SDK uninstaller executed, exit code: ' + IntToStr(ErrorCode));
              if ErrorCode = 0 then
                Log('Wooting SDK uninstalled successfully')
              else
                Log('WARNING: Wooting SDK uninstall returned non-zero error code: ' + IntToStr(ErrorCode));
            end
            else
              Log('ERROR: Failed to execute Wooting SDK uninstaller!');
          end
          else
          begin
            Log('Executing Wooting SDK uninstaller: ' + RemoveQuotes(WootingUninstallString) + ' /qn');
            if Exec(RemoveQuotes(WootingUninstallString), '/qn', '', SW_SHOW, ewWaitUntilTerminated, ErrorCode) then
            begin
              Log('Wooting SDK uninstaller executed, exit code: ' + IntToStr(ErrorCode));
              if ErrorCode = 0 then
                Log('Wooting SDK uninstalled successfully')
              else
                Log('WARNING: Wooting SDK uninstall returned non-zero error code: ' + IntToStr(ErrorCode));
            end
            else
              Log('ERROR: Failed to execute Wooting SDK uninstaller!');
          end;
        end;
      end
      else
        Log('Wooting SDK not installed, skipping');
    end
    else
      Log('User chose to keep dependencies');
  end
  else
    Log('No dependencies installed to remove');

  Log('=== Uninstall Process Complete ===');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Log('Uninstallation completed');
  end;
end;

// ============================================================================
// Custom Messages
// ============================================================================

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo,
  MemoTypeInfo, MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  S: String;
begin
  S := '';

  // Show upgrade/repair information
  if IsUpgrade then
  begin
    S := S + 'Installation Type:' + NewLine;
    S := S + Space + 'Upgrade/Repair from version ' + PreviousVersion + ' to {#AppVersion}' + NewLine + NewLine;
  end;

  // Standard information
  S := S + MemoDirInfo + NewLine + NewLine;
  S := S + MemoGroupInfo + NewLine + NewLine;

  // Components
  S := S + 'Components:' + NewLine;
  S := S + MemoComponentsInfo + NewLine + NewLine;

  // Dependency status
  S := S + 'Dependency Status:' + NewLine;

  if WizardIsComponentSelected('deps\vigem') then
  begin
    if IsViGEmInstalled then
      S := S + Space + 'ViGEm Bus Driver: Already installed (will skip)' + NewLine
    else
      S := S + Space + 'ViGEm Bus Driver: Will be installed' + NewLine;
  end
  else
    S := S + Space + 'ViGEm Bus Driver: Manual installation required' + NewLine;

  if WizardIsComponentSelected('deps\wooting') then
  begin
    if IsWootingSDKInstalled then
      S := S + Space + 'Wooting Analog SDK: Already installed (will skip)' + NewLine
    else
      S := S + Space + 'Wooting Analog SDK: Will be installed' + NewLine;
  end
  else
    S := S + Space + 'Wooting Analog SDK: Manual installation required' + NewLine;

  S := S + NewLine;

  // Tasks
  if MemoTasksInfo <> '' then
    S := S + MemoTasksInfo + NewLine + NewLine;

  Result := S;
end;
