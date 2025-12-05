using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using UniversalAnalogInputUI.Services;
using UniversalAnalogInputUI.Services.Interfaces;
using UniversalAnalogInputUI.Models;
using UniversalAnalogInputUI.Dialogs;
using UniversalAnalogInputUI.Enums;
using UniversalAnalogInputUI.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text;
using System.IO;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Provider;
using Muc = Microsoft.UI.Composition.SystemBackdrops;

namespace UniversalAnalogInputUI;

/// <summary>Main application window wiring UI controls to services.</summary>
public sealed partial class MainWindow : Window
{
    private readonly IRustInteropService _ipcService;
    private readonly IProfileManagementService _profileService;
    private readonly ProfileNavigationHelper _navigationHelper;
    private readonly IToastService _toastService;
    private readonly IHotkeyManagementService _hotkeyService;
    private readonly IMappingManagementService _mappingService;
    private readonly IDialogService _dialogService;
    private readonly IStatusMonitorService _statusMonitorService;
    private readonly IGamepadService _gamepadService;
    private readonly TaskbarBadgeService _taskbarBadgeService;
    private readonly ThemeService _themeService;
    private readonly Services.Factories.IWindowHandleProvider _windowHandleProvider;

    public static MainWindow? Instance { get; private set; }
    public Controls.MappingsListControl? MappingsListControlInstance => MappingsListControl;
    public ThemeService? ThemeServiceInstance => _themeService;

    private bool _isInitialized = false;
    private bool _isMappingActive = false;
    private bool _mappingStateKnown = false;
    private bool _startMappingWhenReady = true;
    private bool? _lastKeyboardStatus = null;

    private bool _wootingOk = true;
    private string? _wootingError = null;
    private bool _vigemOk = true;
    private string? _vigemError = null;

    private IntPtr _windowHandle;

    public MainWindow(
        IRustInteropService rustInteropService,
        IProfileManagementService profileService,
        IToastService toastService,
        IHotkeyManagementService hotkeyService,
        IMappingManagementService mappingService,
        IDialogService dialogService,
        IStatusMonitorService statusMonitorService,
        IGamepadService gamepadService,
        TaskbarBadgeService taskbarBadgeService,
        Services.Factories.IWindowHandleProvider windowHandleProvider)
    {
        Instance = this;
        this.InitializeComponent();

        _ipcService = rustInteropService;
        _profileService = profileService;
        _toastService = toastService;
        _hotkeyService = hotkeyService;
        _mappingService = mappingService;
        _dialogService = dialogService;
        _statusMonitorService = statusMonitorService;
        _gamepadService = gamepadService;
        _taskbarBadgeService = taskbarBadgeService;
        _windowHandleProvider = windowHandleProvider;

        // Create ThemeService manually as it needs the Window instance
        _themeService = new ThemeService(this);

        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _windowHandleProvider.SetHandle(_windowHandle);

        MainNavigationView.PaneOpened += MainNavigationView_PaneOpened;
        MainNavigationView.PaneClosed += MainNavigationView_PaneClosed;

        _gamepadService.Initialize();

        _themeService.Initialize();

        _themeService.ThemeChanged += OnThemeChanged;

        if (this.Content is FrameworkElement rootElement)
        {
            rootElement.ActualThemeChanged += (s, e) => UpdateTitleBarColors();
        }

        _profileService.SetMappingService(_mappingService);
        _navigationHelper = new ProfileNavigationHelper(MainNavigationView, _profileService);

        MappingsListControl.Initialize(_mappingService);
        ProfileManagementBar.Initialize(_profileService, _hotkeyService);
        ProfileManagementBar.SetMappingStateUnknown();

        _statusMonitorService.IsEnabled = Views.SettingsPage.GetShowStatusTabSettingStatic();

        ProfileManagementBar.MappingToggleRequested += OnMappingToggleRequested;
        ProfileManagementBar.DegradedModeClicked += OnDegradedModeClicked;

        TabNavigationControl.PageChanged += OnTabPageChanged;

        _profileService.ProfilesRefreshed += OnProfilesRefreshed;
        _profileService.SelectionChanged += OnProfileSelectionChanged;

        _hotkeyService.HotkeyUpdated += OnHotkeyUpdated;

        _navigationHelper.RenameProfileRequested += (s, p) => RenameProfileMenuItem_Click(p, EventArgs.Empty);
        _navigationHelper.ExportProfileRequested += (s, p) => ExportProfileMenuItem_Click(p, EventArgs.Empty);
        _navigationHelper.DeleteProfileRequested += (s, p) => DeleteProfileMenuItem_Click(p, EventArgs.Empty);
        _navigationHelper.RenameSubProfileRequested += (s, ctx) => RenameSubProfileMenuItem_Click(ctx, EventArgs.Empty);
        _navigationHelper.DeleteSubProfileRequested += (s, ctx) => DeleteSubProfileMenuItem_Click(ctx, EventArgs.Empty);

        _navigationHelper.RefreshNavigationRequested += (expansionStates) =>
        {
            RefreshNavigationMenu(expansionStates);
        };
        _navigationHelper.ClearMappingsRequested += () =>
        {
            _mappingService.ClearMappings();
        };
        _navigationHelper.UpdateSelectionHeaderRequested += () =>
        {
            UpdateSelectionHeader();
        };

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(titleBar);

        TrySetMicaBackdrop(useMicaAlt: false);

        this.AppWindow.TitleBar.PreferredHeightOption = Microsoft.UI.Windowing.TitleBarHeightOption.Tall;

        try
        {
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(titleBar);
        }
        catch { /* Safe no-op if not supported */ }

        InitializeUI();

        System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
        {
            this.DispatcherQueue.TryEnqueue(() => UpdateTitleBarColors());
        });

        if (_ipcService is RustInteropServiceIpc ipcSvc)
        {
            ipcSvc.UiEventReceived += OnIpcEventReceived;
            ipcSvc.ShutdownRequested += OnShutdownRequested;
            ipcSvc.KeyboardStatusChanged += OnKeyboardStatusChanged;
            ipcSvc.BringToFrontRequested += OnBringToFrontRequested;
        }

        this.Closed += MainWindow_Closed;
        this.Closed += Window_Closed; // Ensure Rust cleanup on window close

        _ = Task.Run(() => DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => _ = InitializeRustLibraryAsync()));
    }

    /// <summary>Applies Mica or acrylic backdrop when supported and logs the outcome.</summary>
    private bool TrySetMicaBackdrop(bool useMicaAlt)
    {
        try
        {
            if (Muc.MicaController.IsSupported())
            {
                var mica = new Microsoft.UI.Xaml.Media.MicaBackdrop
                {
                    Kind = useMicaAlt ? Muc.MicaKind.BaseAlt : Muc.MicaKind.Base
                };
                this.SystemBackdrop = mica;
                _statusMonitorService.AppendLiveInput("Applied Mica backdrop.");
                return true;
            }
            else if (Muc.DesktopAcrylicController.IsSupported())
            {
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
                _statusMonitorService.AppendLiveInput("Applied desktop acrylic backdrop.");
                return true;
            }
        }
        catch { /* Fallback silently if not supported at runtime */ }

        _statusMonitorService.AppendLiveInput("System backdrop not supported; using solid backgrounds.");
        return false;
    }

    /// <summary>Initializes UI controls and status indicators once services are ready.</summary>
    private void InitializeUI()
    {
        if (_toastService is ToastService toastService)
        {
            toastService.SetToastControl(ToastControl);
        }

        UpdateSelectionHeader();

        UpdateStatus("System ready", "#FFFEB2B2", "\uE783", "#FFDC2626");

        RefreshNavigationMenu();
        UpdateNavigationFooterLayout();
    }

    private async Task InitializeRustLibraryAsync()
    {
        try
        {
            _isInitialized = true;
            UpdateStatus("System initialized", "#FFB2F5EA", "\uE73E", "#FF16A085");

            _statusMonitorService.AppendLiveInput("Initializing IPC connection...");

            var (version, mappingInitialized) = await Task.Run(async () =>
            {
                string ver = _ipcService.GetVersion();
                bool mapInit = await _mappingService.InitializeAsync();

                return (ver, mapInit);
            }).ConfigureAwait(false);

            await DispatcherQueue.EnqueueAsync(() =>
            {
                VersionText.Text = $"v{version}";
                _statusMonitorService.AppendLiveInput($"IPC connected - version {version}.");

                if (!mappingInitialized)
                {
                    _statusMonitorService.AppendLiveInput("Mapping service initialization incomplete.");
                }

                _statusMonitorService.AppendLiveInput("Digital button system initialized.");
            });

            await CheckDependenciesAsync();

            await _profileService.RefreshProfilesAsync();

            bool isActive = false;
            await DispatcherQueue.EnqueueAsync(() =>
            {
                isActive = RefreshMappingState();  // UI methods must run on the UI thread

                if (!isActive && _startMappingWhenReady)
                {
                    AutoStartTesterMapping("Auto-starting mapping.");
                }
            });
        }
        catch (ObjectDisposedException)
        {
            CrashLogger.LogMessage("[InitializeRustLibraryAsync] Initialization cancelled - window closed", "MainWindow");
            return;
        }
        catch (Exception ex)
        {
            var errorMsg = $"{ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMsg += $" | Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            }

            CrashLogger.LogMessage($"[InitializeRustLibraryAsync] Exception: {errorMsg}", "MainWindow");
            CrashLogger.LogMessage($"[InitializeRustLibraryAsync] StackTrace: {ex.StackTrace}", "MainWindow");

            await DispatcherQueue.EnqueueAsync(() =>
            {
                UpdateStatus($"Error: {errorMsg}", "#FFFEB2B2", "\uE783", "#FFDC2626");
                _toastService.ShowToast("System Error", errorMsg, ToastType.Error);
                _statusMonitorService.AppendLiveInput($"Initialization error: {errorMsg}");
                _statusMonitorService.AppendLiveInput($"Stack trace (first line): {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
            });
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _gamepadService.Shutdown();

        this.Closed -= MainWindow_Closed;
    }

    private void OnShutdownRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            CrashLogger.LogMessage("Shutdown requested - closing window", "MainWindow");
            this.Close(); // Triggers Closed event handlers which will cleanup
        });
    }

    private void OnIpcEventReceived(object? sender, UiEventArgs e)
    {
        // Convert byte arrays to GUIDs
        var profileId = new Guid(e.EventData.ProfileId);
        var subProfileId = new Guid(e.EventData.SubProfileId);

        // Handle sub-profile switch notification from Rust
        if (e.EventData.EventType == 0) // SUB_PROFILE_SWITCH
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                var profile = _profileService.FindProfileById(profileId);
                if (profile == null) return;
                var subProfile = _profileService.FindSubProfileById(profile, subProfileId);
                if (subProfile == null) return;
                await _profileService.SelectProfileAsync(profile, subProfile, activateInRust: false);
            });
        }
    }

    private void OnKeyboardStatusChanged(object? sender, KeyboardStatusEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ProfileManagementBar.SetKeyboardStatus(e.IsConnected);
            UpdateStatus(
                e.IsConnected ? "Keyboard connected" : "No keyboard detected",
                e.IsConnected ? "#FFB2F5EA" : "#FFFEB2B2",
                e.IsConnected ? "\uE73E" : "\uE783",
                e.IsConnected ? "#FF16A085" : "#FFDC2626"
            );

            _taskbarBadgeService?.SetBadge(e.IsConnected);

            // Only react to actual state changes for toasts and logs
            if (_lastKeyboardStatus.HasValue && _lastKeyboardStatus.Value == e.IsConnected)
            {
                return; // Same status, ignore toast
            }

            _lastKeyboardStatus = e.IsConnected;

            if (e.IsConnected)
            {
                _statusMonitorService.AppendLiveInput("Wooting keyboard connected.");
            }
            else
            {
                _statusMonitorService.AppendLiveInput("Wooting keyboard disconnected.");
                _toastService.ShowToast(
                    "No Compatible Device",
                    "No keyboard detected. Please connect a compatible analog keyboard.",
                    ToastType.Error
                );
            }
        });
    }

    private void OnBringToFrontRequested(object? sender, EventArgs e)
    {
        BringToFront();
    }

    private void OnProfilesRefreshed(object? sender, ProfileRefreshedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            RefreshNavigationMenu();

            if (!e.Success)
            {
                _toastService.ShowToast("No Profiles", e.Message ?? "No profiles found", ToastType.Warning);
            }
        });
    }

    private void OnProfileSelectionChanged(object? sender, ProfileSelectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            MappingsListControl.ClearSelection();

            if (e.Profile != null && e.SubProfile != null)
            {
                UpdateSelectionHeader();

                await _mappingService.LoadMappingsAsync(e.Profile, e.SubProfile);
            }
            else
            {
                _mappingService.ClearMappings();
                UpdateSelectionHeader();
            }

            _navigationHelper.RestoreNavigationSelection();
        });
    }



    private void OnHotkeyUpdated(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateSelectionHeader();
            RefreshNavigationMenu();
        });
    }



    private void UpdateSelectionHeader()
    {
        ProfileManagementBar.RefreshFromServices();

        if (AddSubProfileButton != null)
        {
            AddSubProfileButton.IsEnabled = _profileService.SelectedProfile != null;
        }

        UpdateCompactAddMenuState();
    }

    private void UpdateNavigationFooterLayout()
    {
        var isExpanded = MainNavigationView.IsPaneOpen;

        if (ExpandedFooterActions != null)
        {
            ExpandedFooterActions.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        }

        if (ExpandedFooterContainer != null)
        {
            ExpandedFooterContainer.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        }

        if (CompactAddButton != null)
        {
            CompactAddButton.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;
        }

        if (CompactFooterContainer != null)
        {
            CompactFooterContainer.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;
        }

        UpdateCompactAddMenuState();

        _navigationHelper.UpdateProfileIconsForPane(isExpanded);
    }

    private void UpdateCompactAddMenuState()
    {
        if (CompactAddSubProfileMenuItem != null && AddSubProfileButton != null)
        {
            CompactAddSubProfileMenuItem.IsEnabled = AddSubProfileButton.IsEnabled;
        }
    }

    private void MainNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            TabNavigationControl.NavigateToTab("Settings");
        }
    }

    private async void MainNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_navigationHelper.IsRestoringSelection)
        {
            return;
        }

        if (args.SelectedItem is NavigationViewItem navItem && navItem.Tag is SubProfile subProfile)
        {
            var owningProfile = _profileService.Profiles.FirstOrDefault(p => p.SubProfiles.Contains(subProfile));
            if (owningProfile != null)
            {
                _statusMonitorService.AppendLiveInput($"Switched to profile {owningProfile.Name}.");
                await _profileService.SelectProfileAsync(owningProfile, subProfile, activateInRust: true);
            }
        }
    }

    private void RefreshNavigationMenu(Dictionary<Guid, bool>? expansionStates = null)
    {
        expansionStates ??= CaptureExpansionStates();
        _navigationHelper.RefreshNavigationItems(expansionStates);
    }

    private Dictionary<Guid, bool> CaptureExpansionStates()
    {
        var states = new Dictionary<Guid, bool>();

        foreach (var item in MainNavigationView.MenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag is GameProfile profile)
            {
                states[profile.Id] = navItem.IsExpanded;
            }
        }

        return states;
    }

    private void UpdateStatus(string text, string backgroundColor, string iconGlyph, string iconColor)
    {
        _statusMonitorService.UpdateStatus(text, backgroundColor, iconGlyph, iconColor);
        FooterText.Text = text;
    }



    private async void AddProfileButton_Click(object sender, RoutedEventArgs e)
    {
        await _profileService.HandleAddProfileWorkflowAsync();
    }

    private async void AddSubProfileButton_Click(object sender, RoutedEventArgs e)
    {
        await _profileService.HandleAddSubProfileWorkflowAsync();
    }

    private async void ImportProfileButton_Click(object sender, RoutedEventArgs e)
    {
        await _profileService.HandleImportProfileWorkflowAsync();
    }

    private async void ExportProfileMenuItem_Click(GameProfile profile, EventArgs e)
    {
        await _profileService.HandleExportProfileWorkflowAsync(profile);
    }

    private async void RenameProfileMenuItem_Click(GameProfile profile, EventArgs e)
    {
        await _profileService.HandleRenameProfileWorkflowAsync(profile);
    }

    private async void DeleteProfileMenuItem_Click(GameProfile profile, EventArgs e)
    {
        await _profileService.HandleDeleteProfileWorkflowAsync(profile);
    }

    private async void RenameSubProfileMenuItem_Click((GameProfile Profile, SubProfile SubProfile) context, EventArgs e)
    {
        var (profile, subProfile) = context;
        await _profileService.HandleRenameSubProfileWorkflowAsync(profile, subProfile);
    }

    private async void DeleteSubProfileMenuItem_Click((GameProfile Profile, SubProfile SubProfile) context, EventArgs e)
    {
        var (profile, subProfile) = context;
        await _profileService.HandleDeleteSubProfileWorkflowAsync(profile, subProfile);
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        this.AppWindow.Hide();

        try
        {
            CrashLogger.LogMessage("Cleaning up Rust IPC client", "MainWindow");
            _ipcService.Cleanup();
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, "MainWindow.Window_Closed");
        }
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        MainNavigationView.IsPaneOpen = !MainNavigationView.IsPaneOpen;
        UpdateNavigationFooterLayout();
    }

    private void OnThemeChanged(object? sender, ElementTheme theme)
    {
        // ActualThemeChanged will invoke UpdateTitleBarColors.
    }

    private void UpdateTitleBarColors()
    {
        var titleBar = this.AppWindow.TitleBar;

        bool isLightTheme = false;
        if (this.Content is FrameworkElement root)
        {
            isLightTheme = root.ActualTheme == ElementTheme.Light;
        }

        if (isLightTheme)
        {
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 0, 0, 0);
            titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(30, 0, 0, 0);
        }
        else
        {
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(20, 255, 255, 255);
            titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(30, 255, 255, 255);
        }

        titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
    }


    private void OnPaneDisplayModeChanged(NavigationView sender, NavigationViewDisplayModeChangedEventArgs args)
    {
        titleBar.IsPaneToggleButtonVisible = true;
        UpdateNavigationFooterLayout();
    }

    private void MainNavigationView_PaneOpened(NavigationView sender, object args)
    {
        UpdateNavigationFooterLayout();
        _navigationHelper.UpdateProfileIconsForPane(true);
    }

    private void MainNavigationView_PaneClosed(NavigationView sender, object args)
    {
        UpdateNavigationFooterLayout();
        _navigationHelper.UpdateProfileIconsForPane(false);
    }

    private void OnMappingToggleRequested(object? sender, bool startMapping)
    {
        if (!_isInitialized) return;
        if (!_mappingStateKnown)
        {
            _statusMonitorService.AppendLiveInput("Mapping state not ready yet; please wait.");
            return;
        }

        if (startMapping)
        {
            _statusMonitorService.AppendLiveInput("Starting mapping.");
            try
            {
                var result = _ipcService.StartMapping();
                if (result == 0)
                {
                    _mappingStateKnown = true;
                    _isMappingActive = true;
                    ProfileManagementBar.SetMappingActive(true);
                    UpdateStatus("Mapping active - 120 FPS Rust thread", "#FFB2F5EA", "\uE768", "#FF2196F3");
                    _statusMonitorService.AppendLiveInput("Mapping started at 120 FPS.");
                }
                else
                {
                    UpdateStatus("Failed to start mapping", "#FFFEB2B2", "\uE783", "#FFDC2626");
                    _toastService.ShowToast("Mapping Failed", "Failed to start mapping thread", ToastType.Error);
                    _statusMonitorService.AppendLiveInput("Failed to start mapping thread.");
                }
            }
            catch (Exception ex)
            {
                _mappingStateKnown = false;
                ProfileManagementBar.SetMappingStateUnknown();
                UpdateStatus("Failed to start mapping", "#FFFEB2B2", "\uE783", "#FFDC2626");
                _toastService.ShowToast("Mapping Failed", ex.Message, ToastType.Error);
                _statusMonitorService.AppendLiveInput($"Error starting mapping: {ex.Message}");
            }
        }
        else
        {
            _statusMonitorService.AppendLiveInput("Stopping mapping.");
            try
            {
                var result = _ipcService.StopMapping();
                if (result == 0)
                {
                    _mappingStateKnown = true;
                    _isMappingActive = false;
                    ProfileManagementBar.SetMappingActive(false);
                    UpdateStatus("Mapping paused", "#FFFEB2B2", "\uE71A", "#FFFF9800");
                    _statusMonitorService.AppendLiveInput("Mapping paused.");
                }
                else
                {
                    _statusMonitorService.AppendLiveInput("Failed to stop mapping thread.");
                }
            }
            catch (Exception ex)
            {
                _mappingStateKnown = false;
                ProfileManagementBar.SetMappingStateUnknown();
                _toastService.ShowToast("Stop Failed", ex.Message, ToastType.Error);
                _statusMonitorService.AppendLiveInput($"Error stopping mapping: {ex.Message}");
            }
        }
    }

    private void OnTabPageChanged(object? sender, string pageTag)
    {
        bool isTestPage = pageTag == "Tester";
        ProfileManagementBar.SetTestPageActive(isTestPage);

        if (!_isInitialized)
        {
            _startMappingWhenReady = isTestPage;
            return;
        }

        if (isTestPage && (!_mappingStateKnown || !_isMappingActive))
        {
            AutoStartTesterMapping("Test page activated - starting mapping.");
        }
        else if (!isTestPage)
        {
            _startMappingWhenReady = false;
        }
    }

    private void AutoStartTesterMapping(string logMessage)
    {
        if (_mappingStateKnown && _isMappingActive)
            return;

        _startMappingWhenReady = false;
        _statusMonitorService.AppendLiveInput(logMessage);
        try
        {
            var result = _ipcService.StartMapping();
            if (result == 0)
            {
                _mappingStateKnown = true;
                _isMappingActive = true;
                ProfileManagementBar.SetMappingActive(true);
                UpdateStatus("Mapping active - 120 FPS Rust thread", "#FFB2F5EA", "\uE768", "#FF2196F3");
                _statusMonitorService.AppendLiveInput("Mapping started for testing.");
            }
            else
            {
                _statusMonitorService.AppendLiveInput("Failed to auto-start mapping thread.");
            }
        }
        catch (Exception ex)
        {
            _mappingStateKnown = false;
            ProfileManagementBar.SetMappingStateUnknown();
            _statusMonitorService.AppendLiveInput($"Failed to start mapping: {ex.Message}");
        }
    }
    private bool RefreshMappingState()
    {
        try
        {
            bool isActive = _ipcService.IsMappingActive();
            _mappingStateKnown = true;
            _isMappingActive = isActive;
            ProfileManagementBar.SetMappingActive(isActive);

            if (isActive)
            {
                UpdateStatus("Mapping active - attached to running tray session", "#FFB2F5EA", "\uE768", "#FF2196F3");
                _statusMonitorService.AppendLiveInput("Detected active mapping thread from tray session.");
                _startMappingWhenReady = false;
            }

            return isActive;
        }
        catch (Exception ex)
        {
            _mappingStateKnown = false;
            ProfileManagementBar.SetMappingStateUnknown();
            _statusMonitorService.AppendLiveInput($"Unable to query mapping state: {ex.Message}");
            return _isMappingActive;
        }
    }

    // P/Invoke for bringing window to foreground
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    /// <summary>Brings the window to the foreground.</summary>
    public void BringToFront()
    {
        this.DispatcherQueue.TryEnqueue(() =>
        {
            if (IsIconic(_windowHandle))
            {
                ShowWindow(_windowHandle, SW_RESTORE);
            }

            SetForegroundWindow(_windowHandle);

            this.Activate();
        });
    }

    /// <summary>Handles clicks on the degraded mode indicator.</summary>
    private async void OnDegradedModeClicked(object? sender, EventArgs e)
    {
        await ShowDependencyDialog();
    }

    /// <summary>Shows the dependency error dialog with current dependency status.</summary>
    private async Task ShowDependencyDialog()
    {
        var dialog = new Dialogs.DependencyErrorDialog(
            _wootingOk,
            _wootingError,
            _vigemOk,
            _vigemError)
        {
            XamlRoot = this.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    /// <summary>Checks dependency status and opens an error dialog when components are missing.</summary>
    private async Task CheckDependenciesAsync()
    {
        try
        {
            _statusMonitorService.AppendLiveInput("Checking dependency status...");

            var metrics = await Task.Run(() =>
            {
                return _ipcService.GetPerformanceMetrics();
            }).ConfigureAwait(false);

            if (metrics == null)
            {
                _statusMonitorService.AppendLiveInput("Unable to get performance metrics from IPC.");
                return;
            }

            _wootingOk = metrics.Components.WootingSdk.IsHealthy;
            _wootingError = metrics.Components.WootingSdk.ErrorMessage;
            _vigemOk = metrics.Components.VigemClient.IsHealthy;
            _vigemError = metrics.Components.VigemClient.ErrorMessage;

            _statusMonitorService.AppendLiveInput($"Wooting SDK status: {(_wootingOk ? "OK" : "missing")} {(_wootingError != null ? $"- {_wootingError}" : string.Empty)}".Trim());
            _statusMonitorService.AppendLiveInput($"ViGEm Client status: {(_vigemOk ? "OK" : "missing")} {(_vigemError != null ? $"- {_vigemError}" : string.Empty)}".Trim());

            if (!_wootingOk || !_vigemOk)
            {
                await DispatcherQueue.EnqueueAsync(async () =>
                {
                    _statusMonitorService.AppendLiveInput("Missing dependencies detected; opening dialog.");

                    var dialog = new Dialogs.DependencyErrorDialog(
                        _wootingOk,
                        _wootingError,
                        _vigemOk,
                        _vigemError)
                    {
                        XamlRoot = this.Content.XamlRoot
                    };

                    var result = await dialog.ShowAsync();

                    if (dialog.Result == Dialogs.DependencyErrorDialog.DependencyResult.ExitApplication)
                    {
                        _statusMonitorService.AppendLiveInput("User chose to exit because dependencies are missing.");
                        this.Close();
                    }
                    else if (dialog.Result == Dialogs.DependencyErrorDialog.DependencyResult.ContinueAnyway)
                    {
                        _statusMonitorService.AppendLiveInput("User chose to continue in degraded mode.");
                        _toastService.ShowToast(
                            "Degraded Mode",
                            "Some features will not work without the required dependencies.",
                            ToastType.Warning
                        );
                        ProfileManagementBar.SetDegradedMode(true);
                    }
                });
            }
            else
            {
                await DispatcherQueue.EnqueueAsync(() =>
                {
                    _statusMonitorService.AppendLiveInput("All dependencies confirmed.");
                    ProfileManagementBar.SetDegradedMode(false);
                });
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogMessage($"[CheckDependencies] Exception: {ex.Message}", "MainWindow");
        }
    }

}
