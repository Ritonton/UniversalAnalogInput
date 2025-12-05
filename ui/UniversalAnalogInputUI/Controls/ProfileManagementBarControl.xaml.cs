using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using UniversalAnalogInputUI.Models;
using UniversalAnalogInputUI.Services.Interfaces;
using UniversalAnalogInputUI.Helpers;
using Windows.UI;

namespace UniversalAnalogInputUI.Controls;

/// <summary>Top bar for profile/sub-profile display, hotkeys, and mapping state controls.</summary>
public sealed partial class ProfileManagementBarControl : UserControl
{
    private enum MappingDisplayState
    {
        Unknown,
        Active,
        Inactive
    }

    private GameProfile? _currentProfile;
    private SubProfile? _currentSubProfile;
    private IProfileManagementService? _profileService;
    private IHotkeyManagementService? _hotkeyService;
    private bool _isInitialized;
    private bool _isMappingActive = false;
    private bool _isTestPageActive = false;
    private MappingDisplayState _mappingState = MappingDisplayState.Unknown;

    public event EventHandler<bool>? MappingToggleRequested;
    public event EventHandler? DegradedModeClicked;

    public ProfileManagementBarControl()
    {
        InitializeComponent();
        Loaded += ProfileManagementBarControl_Loaded;
        SetMappingStateUnknown();
        ActualThemeChanged += OnActualThemeChanged;
    }

    public void Initialize(IProfileManagementService profileService, IHotkeyManagementService hotkeyService)
    {
        if (_isInitialized)
        {
            return;
        }

        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));

        _profileService.ProfilesRefreshed += OnProfilesRefreshed;
        _profileService.SelectionChanged += OnProfileSelectionChanged;
        _hotkeyService.HotkeyUpdated += OnHotkeyUpdated;

        _isInitialized = true;
        RefreshFromServices();
    }

    private void ProfileManagementBarControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            RefreshFromServices();
            UpdateRemoveIconAccentBrushes("Loaded");
        }
    }

    public void RefreshFromServices()
    {
        if (!_isInitialized || _profileService == null)
        {
            return;
        }

        _currentProfile = _profileService.SelectedProfile;
        _currentSubProfile = _profileService.SelectedSubProfile;

        RefreshProfileSection();
        RefreshSubProfileSection();
    }

    private void OnProfilesRefreshed(object? sender, ProfileRefreshedEventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(RefreshFromServices);
    }

    private void OnProfileSelectionChanged(object? sender, ProfileSelectionChangedEventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(RefreshFromServices);
    }

    private void OnHotkeyUpdated(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(RefreshFromServices);
    }

    private void RefreshProfileSection()
    {
        bool hasProfile = _currentProfile != null;
        SelectedProfileText.Text = hasProfile ? _currentProfile!.Name : "Select a profile";

        var profileHotkey = _currentProfile?.HotKey;
        bool hasHotkey = !string.IsNullOrWhiteSpace(profileHotkey);
        ProfileHotkeyText.Text = profileHotkey ?? string.Empty;

        ProfileHotkeyAddButton.Visibility = hasHotkey ? Visibility.Collapsed : Visibility.Visible;
        ProfileHotkeyButton.Visibility = hasHotkey ? Visibility.Visible : Visibility.Collapsed;
        ProfileHotkeySeparator.Visibility = hasHotkey ? Visibility.Visible : Visibility.Collapsed;
        ProfileHotkeyRemoveButton.Visibility = hasHotkey ? Visibility.Visible : Visibility.Collapsed;

        bool serviceAvailable = _isInitialized && _hotkeyService != null;
        ProfileHotkeyAddButton.IsEnabled = hasProfile && serviceAvailable;
        ProfileHotkeyButton.IsEnabled = hasProfile && serviceAvailable;
        ProfileHotkeyRemoveButton.IsEnabled = hasProfile && hasHotkey && serviceAvailable;
    }

    private void RefreshSubProfileSection()
    {
        bool hasSubProfile = _currentSubProfile != null;

        BreadcrumbSeparator.Visibility = hasSubProfile ? Visibility.Visible : Visibility.Collapsed;
        SubProfileSection.Visibility = hasSubProfile ? Visibility.Visible : Visibility.Collapsed;

        if (!hasSubProfile)
        {
            SelectedSubProfileText.Text = "Select a sub-profile";
            SubProfileHotkeyText.Text = string.Empty;

            SubProfileHotkeyAddButton.Visibility = Visibility.Visible;
            SubProfileHotkeyButton.Visibility = Visibility.Collapsed;
            SubProfileHotkeyRemoveButton.Visibility = Visibility.Collapsed;

            SubProfileHotkeyAddButton.IsEnabled = false;
            SubProfileHotkeyButton.IsEnabled = false;
            SubProfileHotkeyRemoveButton.IsEnabled = false;
            return;
        }

        SelectedSubProfileText.Text = _currentSubProfile!.Name;

        var subProfileHotkey = _currentSubProfile!.HotKey;
        bool hasHotkey = !string.IsNullOrWhiteSpace(subProfileHotkey);
        SubProfileHotkeyText.Text = subProfileHotkey ?? string.Empty;

        SubProfileHotkeyAddButton.Visibility = hasHotkey ? Visibility.Collapsed : Visibility.Visible;
        SubProfileHotkeyButton.Visibility = hasHotkey ? Visibility.Visible : Visibility.Collapsed;
        SubProfileHotkeySeparator.Visibility = hasHotkey ? Visibility.Visible : Visibility.Collapsed;
        SubProfileHotkeyRemoveButton.Visibility = hasHotkey ? Visibility.Visible : Visibility.Collapsed;

        bool serviceAvailable = _isInitialized && _hotkeyService != null;
        SubProfileHotkeyAddButton.IsEnabled = serviceAvailable;
        SubProfileHotkeyButton.IsEnabled = serviceAvailable;
        SubProfileHotkeyRemoveButton.IsEnabled = hasHotkey && serviceAvailable;
    }

    private async void OnAssignProfileHotkeyClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _hotkeyService == null)
        {
            return;
        }

        await _hotkeyService.HandleAssignProfileHotkeyWorkflowAsync();
        RefreshFromServices();
    }

    private async void OnClearProfileHotkeyClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _hotkeyService == null)
        {
            return;
        }

        await _hotkeyService.HandleClearProfileHotkeyWorkflowAsync();
        RefreshFromServices();
    }

    private async void OnAssignSubProfileHotkeyClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _hotkeyService == null)
        {
            return;
        }

        await _hotkeyService.HandleAssignSubProfileHotkeyWorkflowAsync();
        RefreshFromServices();
    }

    private async void OnClearSubProfileHotkeyClicked(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || _hotkeyService == null)
        {
            return;
        }

        await _hotkeyService.HandleClearSubProfileHotkeyWorkflowAsync();
        RefreshFromServices();
    }

    public void SetTestPageActive(bool isActive)
    {
        _isTestPageActive = isActive;
        UpdatePauseButtonState();
    }

    public void SetMappingActive(bool isActive)
    {
        _isMappingActive = isActive;
        _mappingState = isActive ? MappingDisplayState.Active : MappingDisplayState.Inactive;
        UpdatePauseButtonState();
    }

    public void SetMappingStateUnknown()
    {
        _mappingState = MappingDisplayState.Unknown;
        _isMappingActive = false;
        UpdatePauseButtonState();
    }

    private void UpdatePauseButtonState()
    {
        bool isUnknown = _mappingState == MappingDisplayState.Unknown;
        PauseSpinner.Visibility = isUnknown ? Visibility.Visible : Visibility.Collapsed;
        PauseSpinner.IsActive = isUnknown;
        PlayIcon.Visibility = isUnknown ? Visibility.Collapsed : Visibility.Visible;
        PauseIcon.Visibility = isUnknown ? Visibility.Collapsed : Visibility.Visible;

        if (_isTestPageActive || isUnknown)
        {
            PauseButton.Opacity = 0.5;
            ToolTipService.SetToolTip(PauseButton, _isTestPageActive
                ? "Mapping cannot be paused while testing - Test page requires active mapping"
                : "Checking mapping state...");
        }
        else
        {
            PauseButton.Opacity = 1.0;
            ToolTipService.SetToolTip(PauseButton, null);
        }

        if (isUnknown)
        {
            PauseButtonText.Text = "Hold";
        }
        else
        {
            PauseButtonText.Text = _isMappingActive ? "Pause" : "Start";
            AnimateIconTransition(_isMappingActive);
        }
    }

    private void AnimateIconTransition(bool showPause)
    {
        var storyboard = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(200);

        if (showPause)
        {
            var playOpacityAnim = new DoubleAnimation
            {
                To = 0,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(playOpacityAnim, PlayIcon);
            Storyboard.SetTargetProperty(playOpacityAnim, "Opacity");
            storyboard.Children.Add(playOpacityAnim);

            var playScaleXAnim = new DoubleAnimation
            {
                To = 0.8,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(playScaleXAnim, PlayIconScale);
            Storyboard.SetTargetProperty(playScaleXAnim, "ScaleX");
            storyboard.Children.Add(playScaleXAnim);

            var playScaleYAnim = new DoubleAnimation
            {
                To = 0.8,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(playScaleYAnim, PlayIconScale);
            Storyboard.SetTargetProperty(playScaleYAnim, "ScaleY");
            storyboard.Children.Add(playScaleYAnim);

            var pauseOpacityAnim = new DoubleAnimation
            {
                To = 1,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(pauseOpacityAnim, PauseIcon);
            Storyboard.SetTargetProperty(pauseOpacityAnim, "Opacity");
            storyboard.Children.Add(pauseOpacityAnim);

            var pauseScaleXAnim = new DoubleAnimation
            {
                To = 1,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(pauseScaleXAnim, PauseIconScale);
            Storyboard.SetTargetProperty(pauseScaleXAnim, "ScaleX");
            storyboard.Children.Add(pauseScaleXAnim);

            var pauseScaleYAnim = new DoubleAnimation
            {
                To = 1,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(pauseScaleYAnim, PauseIconScale);
            Storyboard.SetTargetProperty(pauseScaleYAnim, "ScaleY");
            storyboard.Children.Add(pauseScaleYAnim);
        }
        else
        {
            var pauseOpacityAnim = new DoubleAnimation
            {
                To = 0,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(pauseOpacityAnim, PauseIcon);
            Storyboard.SetTargetProperty(pauseOpacityAnim, "Opacity");
            storyboard.Children.Add(pauseOpacityAnim);

            var pauseScaleXAnim = new DoubleAnimation
            {
                To = 0.8,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(pauseScaleXAnim, PauseIconScale);
            Storyboard.SetTargetProperty(pauseScaleXAnim, "ScaleX");
            storyboard.Children.Add(pauseScaleXAnim);

            var pauseScaleYAnim = new DoubleAnimation
            {
                To = 0.8,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(pauseScaleYAnim, PauseIconScale);
            Storyboard.SetTargetProperty(pauseScaleYAnim, "ScaleY");
            storyboard.Children.Add(pauseScaleYAnim);

            var playOpacityAnim = new DoubleAnimation
            {
                To = 1,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(playOpacityAnim, PlayIcon);
            Storyboard.SetTargetProperty(playOpacityAnim, "Opacity");
            storyboard.Children.Add(playOpacityAnim);

            var playScaleXAnim = new DoubleAnimation
            {
                To = 1,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(playScaleXAnim, PlayIconScale);
            Storyboard.SetTargetProperty(playScaleXAnim, "ScaleX");
            storyboard.Children.Add(playScaleXAnim);

            var playScaleYAnim = new DoubleAnimation
            {
                To = 1,
                Duration = duration,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(playScaleYAnim, PlayIconScale);
            Storyboard.SetTargetProperty(playScaleYAnim, "ScaleY");
            storyboard.Children.Add(playScaleYAnim);
        }

        storyboard.Begin();
    }

    private void OnPauseButtonClicked(object sender, RoutedEventArgs e)
    {
        if (_isTestPageActive || _mappingState == MappingDisplayState.Unknown)
        {
            return;
        }

        bool newState = !_isMappingActive;
        MappingToggleRequested?.Invoke(this, newState);
    }

    private void OnRemoveButtonPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button && button.Content is FontIcon icon)
        {
            icon.Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 38, 38));
        }
    }

    private void OnRemoveButtonPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button && button.Content is FontIcon icon)
        {
            icon.Foreground = ThemeAccentHelper.GetAccentBrush(this);
        }
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateRemoveIconAccentBrushes("ThemeChanged");
    }

    private void UpdateRemoveIconAccentBrushes(string contextTag)
    {
        var accentBrush = ThemeAccentHelper.GetAccentBrush(this);

        if (ProfileHotkeyRemoveIcon != null)
        {
            ProfileHotkeyRemoveIcon.Foreground = accentBrush;
        }

        if (SubProfileHotkeyRemoveIcon != null)
        {
            SubProfileHotkeyRemoveIcon.Foreground = accentBrush;
        }
    }

    /// <summary>
    /// Update keyboard status indicator
    /// </summary>
    public void SetKeyboardStatus(bool connected)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (connected)
            {
                KeyboardStatusIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 34, 197, 94)); // Green
                KeyboardStatusText.Text = "Connected";
                ToolTipService.SetToolTip(KeyboardStatusIndicatorPanel, "Keyboard connected");
            }
            else
            {
                KeyboardStatusIndicator.Fill = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)); // Red
                KeyboardStatusText.Text = "No Device";
                ToolTipService.SetToolTip(KeyboardStatusIndicatorPanel, "No keyboard detected");
            }
        });
    }

    /// <summary>
    /// Shows/hides the degraded mode indicator
    /// </summary>
    public void SetDegradedMode(bool isDegraded)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            DegradedModeIndicatorButton.Visibility = isDegraded ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private void DegradedModeIndicatorButton_Click(object sender, RoutedEventArgs e)
    {
        DegradedModeClicked?.Invoke(this, EventArgs.Empty);
    }
}
