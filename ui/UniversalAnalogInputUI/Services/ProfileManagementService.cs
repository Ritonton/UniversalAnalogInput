using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using UniversalAnalogInputUI.Enums;
using UniversalAnalogInputUI.Helpers;
using UniversalAnalogInputUI.Models;
using UniversalAnalogInputUI.Services.Interfaces;

namespace UniversalAnalogInputUI.Services;

/// <summary>
/// Service for managing profiles and sub-profiles with Rust backend
/// Stores profiles in-memory and exposes them as observable properties
/// </summary>
public class ProfileManagementService : IProfileManagementService, INotifyPropertyChanged
{
    private readonly IRustInteropService _rustService;
    private readonly IToastService? _toastService;
    private readonly IFilePickerService? _filePickerService;
    private readonly IDialogService? _dialogService;
    private readonly IStatusMonitorService _statusMonitorService;
    private IMappingManagementService? _mappingService;
    private ObservableCollection<GameProfile> _profiles;
    private GameProfile? _selectedProfile;
    private SubProfile? _selectedSubProfile;
    private bool _isRefreshing;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<ProfileRefreshedEventArgs>? ProfilesRefreshed;
    public event EventHandler<ProfileSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Collection of all loaded profiles
    /// </summary>
    public ObservableCollection<GameProfile> Profiles
    {
        get => _profiles;
        private set => SetProperty(ref _profiles, value);
    }

    /// <summary>
    /// Currently selected profile
    /// </summary>
    public GameProfile? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    /// <summary>
    /// Currently selected sub-profile
    /// </summary>
    public SubProfile? SelectedSubProfile
    {
        get => _selectedSubProfile;
        set => SetProperty(ref _selectedSubProfile, value);
    }

    /// <summary>
    /// Indicates if profiles are currently being refreshed
    /// </summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public ProfileManagementService(IRustInteropService rustService, IToastService? toastService = null, IFilePickerService? filePickerService = null, IDialogService? dialogService = null, IStatusMonitorService? statusMonitorService = null)
    {
        _rustService = rustService ?? throw new ArgumentNullException(nameof(rustService));
        _toastService = toastService;
        _filePickerService = filePickerService;
        _dialogService = dialogService;
        _statusMonitorService = statusMonitorService ?? throw new ArgumentNullException(nameof(statusMonitorService));
        _profiles = new ObservableCollection<GameProfile>();
    }

    /// <summary>
    /// Sets the mapping service for dependency injection
    /// </summary>
    public void SetMappingService(IMappingManagementService mappingService)
    {
        _mappingService = mappingService;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public Task<(bool Success, string Message)> CreateProfileAsync(string name, string description)
    {
        return Task.Run(() =>
        {
            int result = _rustService.CreateProfile(name, description);
            if (result == 0)
                return (true, $"Profile '{name}' created successfully");
            else
                return (false, $"Failed to create profile '{name}'");
        });
    }

    public Task<(bool Success, string Message)> RenameProfileAsync(Guid profileId, string newName)
    {
        return Task.Run(() =>
        {
            int result = _rustService.RenameProfile(profileId, newName);
            if (result == 0)
                return (true, $"Profile renamed to '{newName}'");
            else
                return (false, $"Failed to rename profile");
        });
    }

    public Task<(bool Success, string Message)> DeleteProfileAsync(Guid profileId)
    {
        return Task.Run(() =>
        {
            int result = _rustService.DeleteProfile(profileId);
            if (result == 0)
            {
                _mappingService?.CleanupPendingMappingsForProfile(profileId);
                return (true, "Profile deleted successfully");
            }
            else
                return (false, "Failed to delete profile");
        });
    }

    public Task<(bool Success, string Message)> UpdateProfileHotkeyAsync(Guid profileId, string hotkey)
    {
        return Task.Run(() =>
        {
            int result = _rustService.UpdateProfileHotkey(profileId, hotkey);
            if (result == 0)
                return (true, $"Profile hotkey updated to '{hotkey}'");
            else
                return (false, "Failed to update profile hotkey");
        });
    }

    public Task<(bool Success, string Message)> AddSubProfileAsync(Guid profileId, string name, string description, string hotkey)
    {
        return Task.Run(() =>
        {
            int result = _rustService.AddSubProfile(profileId, name, description, hotkey);
            if (result == 0)
                return (true, $"Sub-profile '{name}' added successfully");
            else
                return (false, $"Failed to add sub-profile '{name}'");
        });
    }

    public Task<(bool Success, string Message)> RenameSubProfileAsync(Guid profileId, Guid subProfileId, string newName)
    {
        return Task.Run(() =>
        {
            int result = _rustService.RenameSubProfile(profileId, subProfileId, newName);
            if (result == 0)
                return (true, $"Sub-profile renamed to '{newName}'");
            else
                return (false, "Failed to rename sub-profile");
        });
    }

    public Task<(bool Success, string Message, bool ProfileAlsoDeleted)> DeleteSubProfileAsync(Guid profileId, Guid subProfileId)
    {
        return Task.Run(() =>
        {
            int result = _rustService.DeleteSubProfile(profileId, subProfileId);
            switch (result)
            {
                case 0:
                    _mappingService?.CleanupPendingMappingsForSubProfile(profileId, subProfileId);
                    return (true, "Sub-profile deleted successfully", false);
                case 1:
                    _mappingService?.CleanupPendingMappingsForProfile(profileId);
                    return (true, "Sub-profile deleted (profile removed – no remaining sub-profiles)", true);
                default:
                    return (false, "Failed to delete sub-profile", false);
            }
        });
    }

    public Task<(bool Success, string Message)> UpdateSubProfileHotkeyAsync(Guid profileId, Guid subProfileId, string hotkey)
    {
        return Task.Run(() =>
        {
            int result = _rustService.UpdateSubProfileHotkey(profileId, subProfileId, hotkey);
            if (result == 0)
                return (true, $"Sub-profile hotkey updated to '{hotkey}'");
            else
                return (false, "Failed to update sub-profile hotkey");
        });
    }

    public Task<List<GameProfile>> LoadAllProfilesAsync()
    {
        return Task.Run(() =>
        {
            var profiles = new List<GameProfile>();
            uint profileCount = _rustService.GetProfileMetadataCount();

            for (uint pIndex = 0; pIndex < profileCount; pIndex++)
            {
                if (_rustService.GetProfileMetadata(pIndex, out var pMeta) != 0) continue;

                var profile = new GameProfile
                {
                    Id = RustDataConverter.DecodeGuid(pMeta.Id),
                    Name = RustDataConverter.DecodeUtf8(pMeta.Name),
                    Description = RustDataConverter.DecodeUtf8(pMeta.Description),
                    GamePath = "",
                    SourceIndex = pIndex,
                    CreatedAt = RustDataConverter.UnixTimestampToDateTime(pMeta.CreatedAt),
                    ModifiedAt = RustDataConverter.UnixTimestampToDateTime(pMeta.ModifiedAt)
                };

                var profileHotkey = RustDataConverter.DecodeUtf8(pMeta.Hotkey);
                if (string.Equals(profileHotkey, "None", StringComparison.OrdinalIgnoreCase))
                {
                    profileHotkey = string.Empty;
                }
                profile.HotKey = profileHotkey;

                profile.SubProfiles.Clear();
                uint subCount = pMeta.SubProfileCount;
                for (uint sIndex = 0; sIndex < subCount; sIndex++)
                {
                    if (_rustService.GetSubProfileMetadata(pIndex, sIndex, out var sMeta) != 0) continue;

                    var hotkey = RustDataConverter.DecodeUtf8(sMeta.Hotkey);
                    if (string.Equals(hotkey, "None", StringComparison.OrdinalIgnoreCase))
                    {
                        hotkey = string.Empty;
                    }

                    var subProfile = new SubProfile
                    {
                        Id = RustDataConverter.DecodeGuid(sMeta.Id),
                        Name = RustDataConverter.DecodeUtf8(sMeta.Name),
                        Description = RustDataConverter.DecodeUtf8(sMeta.Description),
                        HotKey = hotkey,
                        SourceIndex = sIndex,
                        CreatedAt = RustDataConverter.UnixTimestampToDateTime(sMeta.CreatedAt),
                        ModifiedAt = RustDataConverter.UnixTimestampToDateTime(sMeta.ModifiedAt),
                        Mappings = new ObservableCollection<KeyMapping>()
                    };

                    profile.SubProfiles.Add(subProfile);
                }

                var sortedSubProfiles = profile.SubProfiles
                    .OrderBy(sp => sp.CreatedAt)
                    .ToList();

                profile.SubProfiles.Clear();
                foreach (var sortedSub in sortedSubProfiles)
                {
                    profile.SubProfiles.Add(sortedSub);
                }

                profiles.Add(profile);
            }

            return profiles.OrderBy(p => p.CreatedAt).ToList();
        });
    }

    public Task<(bool Success, GameProfile? Profile, SubProfile? SubProfile)> GetActiveProfileAsync()
    {
        return Task.Run<(bool Success, GameProfile? Profile, SubProfile? SubProfile)>(() =>
        {
            uint profileCount = _rustService.GetProfileMetadataCount();
            if (profileCount == 0)
                return (Success: false, Profile: null, SubProfile: null);

            if (_rustService.GetProfileMetadata(0, out var pinfo) == 0)
            {
                var profileId = RustDataConverter.DecodeGuid(pinfo.Id);
                var profileName = RustDataConverter.DecodeUtf8(pinfo.Name);

                for (uint si = 0; si < pinfo.SubProfileCount; si++)
                {
                    if (_rustService.GetSubProfileMetadata(0, si, out var sinfo) == 0)
                    {
                        var subId = RustDataConverter.DecodeGuid(sinfo.Id);
                        var subName = RustDataConverter.DecodeUtf8(sinfo.Name);

                        var profile = new GameProfile { Id = profileId, Name = profileName };
                        var subProfile = new SubProfile { Id = subId, Name = subName };

                        return (Success: true, Profile: profile, SubProfile: subProfile);
                    }
                }
            }

            return (Success: false, Profile: null, SubProfile: null);
        });
    }

    public Task<(bool Success, string Message)> SwitchToProfileAsync(Guid profileId, Guid subProfileId)
    {
        return Task.Run(() =>
        {
            int result = _rustService.SwitchProfile(profileId, subProfileId);
            if (result == 0)
                return (true, "Switched profile successfully");
            else
                return (false, "Failed to switch profile");
        });
    }

    public Task<(bool Success, string Message)> SaveProfileToFileAsync(Guid profileId, string filePath)
    {
        return Task.Run(() =>
        {
            int result = _rustService.SaveProfileToFile(profileId, filePath);
            if (result == 0)
                return (true, $"Profile saved to {filePath}");
            else
                return (false, $"Failed to save profile to {filePath}");
        });
    }

    public Task<(bool Success, string Message)> LoadProfileFromFileAsync(string filePath)
    {
        return Task.Run(() =>
        {
            int result = _rustService.LoadProfileFromFile(filePath);
            if (result == 0)
                return (true, $"Profile loaded from {filePath}");
            else
                return (false, $"Failed to load profile from {filePath}");
        });
    }

    /// <summary>
    /// Refreshes the Profiles collection from Rust with full selection management
    /// </summary>
    public async Task RefreshProfilesAsync(bool syncActiveSelection = true, Guid? overrideProfileId = null, Guid? overrideSubProfileId = null)
    {
        if (IsRefreshing) return;

        uint profileCount = _rustService.GetProfileMetadataCount();
        if (profileCount == 0)
        {
            _statusMonitorService.AppendLiveInput("No profiles found in Rust.");
            ProfilesRefreshed?.Invoke(this, new ProfileRefreshedEventArgs
            {
                ProfileCount = 0,
                SubProfileCount = 0,
                Success = false,
                Message = "No profiles found"
            });
            return;
        }

        IsRefreshing = true;
        try
        {
            Guid activeProfileId = Guid.Empty;
            Guid activeSubProfileId = Guid.Empty;

            if (syncActiveSelection && !overrideProfileId.HasValue)
            {
                var (success, activeProfile, activeSubProfile) = await GetActiveProfileAsync();
                if (success && activeProfile != null && activeSubProfile != null)
                {
                    activeProfileId = activeProfile.Id;
                    activeSubProfileId = activeSubProfile.Id;
                }
            }

            var sortedProfiles = await LoadAllProfilesAsync();

            Profiles.Clear();
            foreach (var profile in sortedProfiles)
            {
                Profiles.Add(profile);
            }

            GameProfile? profileToSelect = null;
            SubProfile? subToSelect = null;

            if (overrideProfileId.HasValue)
            {
                profileToSelect = FindProfileById(overrideProfileId.Value);
                if (profileToSelect != null)
                {
                    if (overrideSubProfileId.HasValue)
                    {
                        subToSelect = FindSubProfileById(profileToSelect, overrideSubProfileId.Value);
                    }
                    subToSelect ??= profileToSelect.SubProfiles.FirstOrDefault();
                }
            }
            else if (syncActiveSelection)
            {
                if (activeProfileId != Guid.Empty)
                {
                    profileToSelect = FindProfileById(activeProfileId);
                    if (profileToSelect != null)
                    {
                        if (activeSubProfileId != Guid.Empty)
                        {
                            subToSelect = FindSubProfileById(profileToSelect, activeSubProfileId);
                        }
                        subToSelect ??= profileToSelect.SubProfiles.FirstOrDefault();
                    }
                }

                if (profileToSelect == null)
                {
                    profileToSelect = Profiles.FirstOrDefault();
                    subToSelect = profileToSelect?.SubProfiles.FirstOrDefault();
                }
            }

            if (profileToSelect != null)
            {
                await SelectProfileAsync(profileToSelect, subToSelect, activateInRust: syncActiveSelection);
            }
            else if (!syncActiveSelection)
            {
                SelectedProfile = null;
                SelectedSubProfile = null;
            }

            int totalSubProfiles = sortedProfiles.Sum(p => p.SubProfiles.Count);
            _statusMonitorService.AppendLiveInput($"Loaded {profileCount} profiles and {totalSubProfiles} sub-profiles.");

            ProfilesRefreshed?.Invoke(this, new ProfileRefreshedEventArgs
            {
                ProfileCount = (int)profileCount,
                SubProfileCount = totalSubProfiles,
                Success = true,
                Message = $"Loaded {profileCount} profiles"
            });
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Selects a profile and sub-profile, optionally activating in Rust
    /// </summary>
    public async Task SelectProfileAsync(GameProfile profile, SubProfile? subProfile, bool activateInRust = true)
    {
        var targetSubProfile = subProfile ?? profile.SubProfiles.FirstOrDefault();
        if (targetSubProfile == null)
        {
            _statusMonitorService.AppendLiveInput($"Profile '{profile.Name}' has no sub-profiles.");
            return;
        }

        if (activateInRust)
        {
            var (success, message) = await SwitchToProfileAsync(profile.Id, targetSubProfile.Id);
            if (success)
            {
                _statusMonitorService.AppendLiveInput($"Activated {profile.Name} :: {targetSubProfile.Name}.");
            }
            else
            {
                _statusMonitorService.AppendLiveInput($"Failed to activate: {message}");
                return;
            }
        }

        SelectedProfile = profile;
        SelectedSubProfile = targetSubProfile;

        SelectionChanged?.Invoke(this, new ProfileSelectionChangedEventArgs
        {
            Profile = profile,
            SubProfile = targetSubProfile,
            WasActivatedInRust = activateInRust
        });
    }

    /// <summary>
    /// Finds a profile by ID in the current Profiles collection
    /// </summary>
    public GameProfile? FindProfileById(Guid id)
    {
        if (id == Guid.Empty) return null;
        return Profiles.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Finds a sub-profile by ID within a profile
    /// </summary>
    public SubProfile? FindSubProfileById(GameProfile? profile, Guid id)
    {
        if (profile == null || id == Guid.Empty) return null;
        return profile.SubProfiles.FirstOrDefault(sp => sp.Id == id);
    }

    /// <summary>
    /// Tries to get the active profile and sub-profile from Rust backend
    /// </summary>
    public bool TryGetActiveProfileAndSubFromRust(out Guid profileId, out Guid subId, out string profileName, out string subName)
    {
        profileId = Guid.Empty;
        subId = Guid.Empty;
        profileName = string.Empty;
        subName = string.Empty;
        try
        {
            uint pcount = _rustService.GetProfileMetadataCount();
            for (uint pi = 0; pi < pcount; pi++)
            {
                if (_rustService.GetProfileMetadata(pi, out var pinfo) == 0)
                {
                    profileId = RustDataConverter.DecodeGuid(pinfo.Id);
                    profileName = RustDataConverter.DecodeUtf8(pinfo.Name);
                    for (uint si = 0; si < pinfo.SubProfileCount; si++)
                    {
                        if (_rustService.GetSubProfileMetadata(pi, si, out var sinfo) == 0)
                        {
                            subId = RustDataConverter.DecodeGuid(sinfo.Id);
                            subName = RustDataConverter.DecodeUtf8(sinfo.Name);
                            return true;
                        }
                    }
                    return pi == 0;
                }
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Complete add sub-profile workflow with validation, prompt, creation, and refresh
    /// </summary>
    public async Task<bool> HandleAddSubProfileWorkflowAsync()
    {
        if (SelectedProfile == null)
        {
            _toastService?.ShowToast("No Profile Selected", "Select a profile first", ToastType.Warning);
            _statusMonitorService.AppendLiveInput("Select a profile before adding a sub-profile.");
            return false;
        }

        if (_dialogService == null)
        {
            _toastService?.ShowToast("Service Error", "Dialog service not available", ToastType.Error);
            _statusMonitorService.AppendLiveInput("Dialog service not available.");
            return false;
        }

        string defaultName = $"Mode {SelectedProfile.SubProfiles.Count + 1}";
        var name = await _dialogService.PromptForTextAsync("Create Sub-Profile", "Sub-profile name", defaultName);
        if (string.IsNullOrWhiteSpace(name)) return false;

        Guid profileId = SelectedProfile.Id;

        var (success, message) = await AddSubProfileAsync(SelectedProfile.Id, name, string.Empty, string.Empty);

        if (success)
        {
            _toastService?.ShowToast("Sub-profile Created", $"'{name}' added successfully", ToastType.Success);
            _statusMonitorService.AppendLiveInput($"Sub-profile created: {name}.");

            await RefreshProfilesAsync(syncActiveSelection: false);
            var refreshedProfile = FindProfileById(profileId);
            var refreshedSub = refreshedProfile?.SubProfiles.FirstOrDefault(sp => sp.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (refreshedProfile != null)
            {
                await SelectProfileAsync(refreshedProfile, refreshedSub, activateInRust: true);
            }
        }
        else
        {
            _toastService?.ShowToast("Sub-profile Error", $"Failed to create '{name}'", ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Failed to create sub-profile '{name}'.");
        }

        return success;
    }

    /// <summary>
    /// Complete add profile workflow with prompt, creation, and refresh
    /// </summary>
    public async Task<bool> HandleAddProfileWorkflowAsync()
    {
        if (_dialogService == null)
        {
            _toastService?.ShowToast("Service Error", "Dialog service not available", ToastType.Error);
            _statusMonitorService.AppendLiveInput("Dialog service not available.");
            return false;
        }

        string defaultName = $"Profile {Profiles.Count + 1}";
        var name = await _dialogService.PromptForTextAsync("Create Profile", "Profile name", defaultName);
        if (string.IsNullOrWhiteSpace(name)) return false;

        var (success, message) = await CreateProfileAsync(name, "Custom profile");

        if (success)
        {
            _toastService?.ShowToast("Success", message, ToastType.Success);
            _statusMonitorService.AppendLiveInput($"Profile created: {name}.");

            await RefreshProfilesAsync(syncActiveSelection: false);
            var createdProfile = Profiles.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (createdProfile != null)
            {
                await SelectProfileAsync(createdProfile, createdProfile.SubProfiles.FirstOrDefault(), activateInRust: true);
            }
        }
        else
        {
            _toastService?.ShowToast("Error", message, ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Operation failed: {message}");
        }

        return success;
    }

    /// <summary>
    /// Complete import profile workflow with file picker, import, and refresh
    /// </summary>
    public async Task<bool> HandleImportProfileWorkflowAsync()
    {
        try
        {
            if (_filePickerService == null)
            {
                _toastService?.ShowToast("Service Error", "File picker service not available", ToastType.Error);
                _statusMonitorService.AppendLiveInput("File picker service not available.");
                return false;
            }

            var filePath = await _filePickerService.PickImportFileAsync();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _toastService?.ShowToast("Import Cancelled", "Profile import canceled", ToastType.Warning);
                _statusMonitorService.AppendLiveInput("Profile import canceled.");
                return false;
            }

            string fileName = System.IO.Path.GetFileName(filePath);
            _statusMonitorService.AppendLiveInput($"Importing profile: {fileName}.");

            var (success, message) = await LoadProfileFromFileAsync(filePath);

            if (success)
            {
                _toastService?.ShowToast("Profile Imported", $"Profile loaded from {fileName}", ToastType.Success);
                _statusMonitorService.AppendLiveInput($"Profile imported: {fileName}.");

                var beforeIds = Profiles.Select(p => p.Id).ToHashSet();

                await RefreshProfilesAsync(syncActiveSelection: false);

                var addedProfile = Profiles.FirstOrDefault(p => !beforeIds.Contains(p.Id));
                if (addedProfile != null)
                {
                    var firstSub = addedProfile.SubProfiles.FirstOrDefault();
                    await SelectProfileAsync(addedProfile, firstSub, activateInRust: true);
                }
            }
            else
            {
                _toastService?.ShowToast("Import Failed", $"Failed to import {fileName}", ToastType.Error);
                _statusMonitorService.AppendLiveInput($"Failed to import profile '{fileName}'. Ensure the file contains at least one sub-profile.");
            }

            return success;
        }
        catch (Exception ex)
        {
            _toastService?.ShowToast("Import Error", "Failed to import profile", ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Failed to import profile: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Complete export profile workflow with file picker and export
    /// </summary>
    public async Task<bool> HandleExportProfileWorkflowAsync(GameProfile profile)
    {
        try
        {
            if (_filePickerService == null)
            {
                _toastService?.ShowToast("Service Error", "File picker service not available", ToastType.Error);
                _statusMonitorService.AppendLiveInput("File picker service not available.");
                return false;
            }

            var filePath = await _filePickerService.PickExportFileAsync(profile.Name);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                _toastService?.ShowToast("Export Cancelled", "Profile export canceled", ToastType.Warning);
                _statusMonitorService.AppendLiveInput("Profile export canceled.");
                return false;
            }

            var (success, message) = await SaveProfileToFileAsync(profile.Id, filePath);

            if (success)
            {
                await _filePickerService.CompleteExportUpdatesAsync(filePath);

                string fileName = System.IO.Path.GetFileName(filePath);
                _toastService?.ShowToast("Profile Exported", $"Profile saved to {fileName}", ToastType.Success);
                _statusMonitorService.AppendLiveInput($"Profile exported: {fileName}.");
            }
            else
            {
                await _filePickerService.CompleteExportUpdatesAsync(filePath);
                _toastService?.ShowToast("Export Failed", $"Failed to export '{profile.Name}'", ToastType.Error);
                _statusMonitorService.AppendLiveInput($"Failed to export profile '{profile.Name}'.");
            }

            return success;
        }
        catch (Exception ex)
        {
            _toastService?.ShowToast("Export Error", "Failed to export profile", ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Failed to export profile: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Complete rename profile workflow with prompt, rename, and refresh
    /// </summary>
    public async Task<bool> HandleRenameProfileWorkflowAsync(GameProfile profile)
    {
        if (_dialogService == null)
        {
            _toastService?.ShowToast("Service Error", "Dialog service not available", ToastType.Error);
            _statusMonitorService.AppendLiveInput("Dialog service not available.");
            return false;
        }

        var newName = await _dialogService.PromptForTextAsync("Rename Profile", "Profile name", profile.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName.Equals(profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string oldName = profile.Name;
        Guid profileId = profile.Id;
        var (success, message) = await RenameProfileAsync(profile.Id, newName);

        if (success)
        {
            _toastService?.ShowToast("Profile Renamed", $"'{oldName}' → '{newName}'", ToastType.Success);
            _statusMonitorService.AppendLiveInput($"Profile renamed: {oldName} -> {newName}.");

            await RefreshProfilesAsync(syncActiveSelection: false);
            var refreshedProfile = FindProfileById(profileId);
            if (refreshedProfile != null)
            {
                await SelectProfileAsync(refreshedProfile, SelectedSubProfile, activateInRust: true);
            }
        }
        else
        {
            _toastService?.ShowToast("Rename Failed", $"Failed to rename '{oldName}'", ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Failed to rename profile '{oldName}'.");
        }

        return success;
    }

    /// <summary>
    /// Complete delete profile workflow with confirmation and refresh
    /// </summary>
    public async Task<bool> HandleDeleteProfileWorkflowAsync(GameProfile profile)
    {
        if (_dialogService == null)
        {
            _toastService?.ShowToast("Service Error", "Dialog service not available", ToastType.Error);
            _statusMonitorService.AppendLiveInput("Dialog service not available.");
            return false;
        }

        bool confirmed = await _dialogService.ConfirmAsync("Delete Profile", $"Delete profile '{profile.Name}'?");
        if (!confirmed) return false;

        var (success, message) = await DeleteProfileAsync(profile.Id);

        if (success)
        {
            _statusMonitorService.AppendLiveInput($"Profile deleted: {profile.Name}.");
            await RefreshProfilesAsync();
        }
        else
        {
            _toastService?.ShowToast("Delete Failed", $"Failed to delete '{profile.Name}'", ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Failed to delete profile '{profile.Name}'.");
        }

        return success;
    }

    /// <summary>
    /// Complete rename sub-profile workflow with prompt, rename, and refresh
    /// </summary>
    public async Task<bool> HandleRenameSubProfileWorkflowAsync(GameProfile profile, SubProfile subProfile)
    {
        if (_dialogService == null)
        {
            _toastService?.ShowToast("Service Error", "Dialog service not available", ToastType.Error);
            _statusMonitorService.AppendLiveInput("Dialog service not available.");
            return false;
        }

        var newName = await _dialogService.PromptForTextAsync("Rename Sub-Profile", "Sub-profile name", subProfile.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName.Equals(subProfile.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string oldName = subProfile.Name;
        Guid profileId = profile.Id;
        Guid subProfileId = subProfile.Id;

        var (success, message) = await RenameSubProfileAsync(profile.Id, subProfile.Id, newName);

        if (success)
        {
            _toastService?.ShowToast("Sub-profile Renamed", $"'{oldName}' → '{newName}'", ToastType.Success);
            _statusMonitorService.AppendLiveInput($"Sub-profile renamed: {oldName} -> {newName}.");

            await RefreshProfilesAsync(syncActiveSelection: false);
            var refreshedProfile = FindProfileById(profileId);
            var refreshedSub = FindSubProfileById(refreshedProfile, subProfileId);
            if (refreshedProfile != null)
            {
                await SelectProfileAsync(refreshedProfile, refreshedSub, activateInRust: true);
            }
        }
        else
        {
            _toastService?.ShowToast("Rename Failed", $"Failed to rename '{oldName}'", ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Failed to rename sub-profile '{oldName}'.");
        }

        return success;
    }

    /// <summary>
    /// Complete delete sub-profile workflow with confirmation and refresh
    /// </summary>
    public async Task<bool> HandleDeleteSubProfileWorkflowAsync(GameProfile profile, SubProfile subProfile)
    {
        if (_dialogService == null)
        {
            _toastService?.ShowToast("Service Error", "Dialog service not available", ToastType.Error);
            _statusMonitorService.AppendLiveInput("Dialog service not available.");
            return false;
        }

        bool confirmed = await _dialogService.ConfirmAsync("Delete Sub-Profile", $"Delete sub-profile '{subProfile.Name}'?");
        if (!confirmed) return false;

        var (success, message, profileAlsoDeleted) = await DeleteSubProfileAsync(profile.Id, subProfile.Id);

        if (success)
        {
            _statusMonitorService.AppendLiveInput($"Profile deletion failed: {message}");
            await RefreshProfilesAsync();
        }
        else
        {
            _toastService?.ShowToast("Delete Failed", message, ToastType.Error);
            _statusMonitorService.AppendLiveInput($"Operation failed: {message}");
        }

        return success;
    }
}
