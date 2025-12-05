using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UniversalAnalogInputUI.Enums;
using UniversalAnalogInputUI.Models;
using UniversalAnalogInputUI.Services.Interfaces;

namespace UniversalAnalogInputUI.Services;

/// <summary>
/// Service dedicated to hotkey management for profiles and sub-profiles
/// </summary>
public class HotkeyManagementService : IHotkeyManagementService
{
    private readonly IProfileManagementService _profileService;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;
    private readonly IStatusMonitorService _statusMonitorService;

    public event EventHandler? HotkeyUpdated;

    public HotkeyManagementService(IProfileManagementService profileService, IToastService toastService, IDialogService dialogService, IStatusMonitorService statusMonitorService)
    {
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _statusMonitorService = statusMonitorService ?? throw new ArgumentNullException(nameof(statusMonitorService));
    }

    public async Task<bool> HandleAssignProfileHotkeyWorkflowAsync()
    {
        var profile = _profileService.SelectedProfile;
        if (profile == null)
        {
            _statusMonitorService.AppendLiveInput("Select a profile before assigning a hotkey.");
            return false;
        }

        try
        {
            string currentHotkey = profile.HotKey ?? string.Empty;
            _statusMonitorService.AppendLiveInput($"Assigning hotkey for profile {profile.Name}.");

            var (dialogSuccess, newHotkey) = await _dialogService.ShowHotkeyAssignmentAsync(currentHotkey);

            if (!dialogSuccess)
            {
                _statusMonitorService.AppendLiveInput("Profile hotkey assignment canceled.");
                return false;
            }

            var (success, message) = await _profileService.UpdateProfileHotkeyAsync(profile.Id, newHotkey);

            if (success)
            {
                profile.HotKey = string.IsNullOrWhiteSpace(newHotkey) ? string.Empty : newHotkey;

                string logMessage = string.IsNullOrWhiteSpace(newHotkey)
                    ? "Profile cycle hotkey cleared."
                    : $"Profile cycle hotkey set to {newHotkey}.";

                _toastService?.ShowToast("Hotkey Updated",
                    string.IsNullOrWhiteSpace(newHotkey) ? "Profile hotkey cleared" : $"Hotkey set to {newHotkey}",
                    ToastType.Success);
                _statusMonitorService.AppendLiveInput(logMessage);

                HotkeyUpdated?.Invoke(this, EventArgs.Empty);

                return true;
            }
            else
            {
                _toastService?.ShowToast("Hotkey Error", message, ToastType.Error);
                _statusMonitorService.AppendLiveInput($"Hotkey update failed: {message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to assign profile hotkey: {ex.Message}";
            _toastService?.ShowToast("Hotkey Error", errorMsg, ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Hotkey update failed: {errorMsg}");
            return false;
        }
    }

    public async Task<bool> HandleClearProfileHotkeyWorkflowAsync()
    {
        var profile = _profileService.SelectedProfile;
        if (profile == null)
        {
            _statusMonitorService.AppendLiveInput("Select a profile before clearing a hotkey.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.HotKey))
        {
            _statusMonitorService.AppendLiveInput("Profile has no hotkey to clear.");
            return false;
        }

        try
        {
            _statusMonitorService.AppendLiveInput($"Clearing hotkey for profile {profile.Name}.");

            var (success, message) = await _profileService.UpdateProfileHotkeyAsync(profile.Id, string.Empty);

            if (success)
            {
                profile.HotKey = string.Empty;

                _toastService?.ShowToast("Hotkey Cleared", "Profile hotkey removed", ToastType.Success);
                _statusMonitorService.AppendLiveInput("Profile cycle hotkey cleared.");

                HotkeyUpdated?.Invoke(this, EventArgs.Empty);

                return true;
            }
            else
            {
                _toastService?.ShowToast("Hotkey Error", message, ToastType.Error);
                _statusMonitorService.AppendLiveInput($"Hotkey update failed: {message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to clear profile hotkey: {ex.Message}";
            _toastService?.ShowToast("Hotkey Error", errorMsg, ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Hotkey update failed: {errorMsg}");
            return false;
        }
    }

    public async Task<bool> HandleAssignSubProfileHotkeyWorkflowAsync()
    {
        var profile = _profileService.SelectedProfile;
        var subProfile = _profileService.SelectedSubProfile;

        if (profile == null)
        {
            _statusMonitorService.AppendLiveInput("Select a profile before assigning a sub-profile hotkey.");
            return false;
        }

        if (subProfile == null)
        {
            _statusMonitorService.AppendLiveInput("Select a sub-profile before assigning a hotkey.");
            return false;
        }

        try
        {
            string currentHotkey = subProfile.HotKey ?? string.Empty;
            _statusMonitorService.AppendLiveInput($"Assigning hotkey for sub-profile {profile.Name} :: {subProfile.Name}.");

            var (dialogSuccess, newHotkey) = await _dialogService.ShowHotkeyAssignmentAsync(currentHotkey);

            if (!dialogSuccess)
            {
                _statusMonitorService.AppendLiveInput("Sub-profile hotkey assignment canceled.");
                return false;
            }

            var (success, message) = await _profileService.UpdateSubProfileHotkeyAsync(profile.Id, subProfile.Id, newHotkey);

            if (success)
            {
                subProfile.HotKey = string.IsNullOrWhiteSpace(newHotkey) ? string.Empty : newHotkey;

                string logMessage = string.IsNullOrWhiteSpace(newHotkey)
                    ? "Sub-profile hotkey cleared."
                    : $"Sub-profile hotkey set to {newHotkey}.";

                _toastService?.ShowToast("Hotkey Updated",
                    string.IsNullOrWhiteSpace(newHotkey) ? "Sub-profile hotkey cleared" : $"Hotkey set to {newHotkey}",
                    ToastType.Success);
                _statusMonitorService.AppendLiveInput(logMessage);

                HotkeyUpdated?.Invoke(this, EventArgs.Empty);

                return true;
            }
            else
            {
                _toastService?.ShowToast("Hotkey Error", "Failed to set sub-profile hotkey", ToastType.Error);
                _statusMonitorService.AppendLiveInput("Failed to update sub-profile hotkey.");
                return false;
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to assign sub-profile hotkey: {ex.Message}";
            _toastService?.ShowToast("Hotkey Error", errorMsg, ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Hotkey update failed: {errorMsg}");
            return false;
        }
    }

    public async Task<bool> HandleClearSubProfileHotkeyWorkflowAsync()
    {
        var profile = _profileService.SelectedProfile;
        var subProfile = _profileService.SelectedSubProfile;

        if (profile == null)
        {
            _statusMonitorService.AppendLiveInput("Select a profile before clearing a sub-profile hotkey.");
            return false;
        }

        if (subProfile == null)
        {
            _statusMonitorService.AppendLiveInput("Select a sub-profile before clearing a hotkey.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(subProfile.HotKey))
        {
            _statusMonitorService.AppendLiveInput("Sub-profile has no hotkey to clear.");
            return false;
        }

        try
        {
            _statusMonitorService.AppendLiveInput($"Clearing hotkey for sub-profile {profile.Name} :: {subProfile.Name}.");

            var (success, message) = await _profileService.UpdateSubProfileHotkeyAsync(profile.Id, subProfile.Id, string.Empty);

            if (success)
            {
                subProfile.HotKey = string.Empty;

                _toastService?.ShowToast("Hotkey Cleared", "Sub-profile hotkey removed", ToastType.Success);
                _statusMonitorService.AppendLiveInput("Sub-profile hotkey cleared.");

                HotkeyUpdated?.Invoke(this, EventArgs.Empty);

                return true;
            }
            else
            {
                _toastService?.ShowToast("Hotkey Error", message, ToastType.Error);
                _statusMonitorService.AppendLiveInput($"Hotkey update failed: {message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to clear sub-profile hotkey: {ex.Message}";
            _toastService?.ShowToast("Hotkey Error", errorMsg, ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Hotkey update failed: {errorMsg}");
            return false;
        }
    }
}
