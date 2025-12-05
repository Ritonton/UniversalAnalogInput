using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using UniversalAnalogInputUI.Models;

namespace UniversalAnalogInputUI.Services.Interfaces;

/// <summary>Manages profiles and sub-profiles backed by the Rust library.</summary>
public interface IProfileManagementService : INotifyPropertyChanged
{
    /// <summary>Raised after profiles are refreshed.</summary>
    event EventHandler<ProfileRefreshedEventArgs>? ProfilesRefreshed;
    /// <summary>Raised when the active profile or sub-profile changes.</summary>
    event EventHandler<ProfileSelectionChangedEventArgs>? SelectionChanged;

    ObservableCollection<GameProfile> Profiles { get; }
    GameProfile? SelectedProfile { get; set; }
    SubProfile? SelectedSubProfile { get; set; }
    bool IsRefreshing { get; }

    Task RefreshProfilesAsync(bool syncActiveSelection = true, Guid? overrideProfileId = null, Guid? overrideSubProfileId = null);
    Task SelectProfileAsync(GameProfile profile, SubProfile? subProfile, bool activateInRust = true);
    GameProfile? FindProfileById(Guid id);
    SubProfile? FindSubProfileById(GameProfile? profile, Guid id);
    bool TryGetActiveProfileAndSubFromRust(out Guid profileId, out Guid subId, out string profileName, out string subName);

    void SetMappingService(IMappingManagementService mappingService);

    Task<(bool Success, string Message)> CreateProfileAsync(string name, string description);
    Task<(bool Success, string Message)> RenameProfileAsync(Guid profileId, string newName);
    Task<(bool Success, string Message)> DeleteProfileAsync(Guid profileId);
    Task<(bool Success, string Message)> UpdateProfileHotkeyAsync(Guid profileId, string hotkey);

    Task<(bool Success, string Message)> AddSubProfileAsync(Guid profileId, string name, string description, string hotkey);
    Task<(bool Success, string Message)> RenameSubProfileAsync(Guid profileId, Guid subProfileId, string newName);
    Task<(bool Success, string Message, bool ProfileAlsoDeleted)> DeleteSubProfileAsync(Guid profileId, Guid subProfileId);
    Task<(bool Success, string Message)> UpdateSubProfileHotkeyAsync(Guid profileId, Guid subProfileId, string hotkey);

    Task<List<GameProfile>> LoadAllProfilesAsync();
    Task<(bool Success, GameProfile? Profile, SubProfile? SubProfile)> GetActiveProfileAsync();

    Task<(bool Success, string Message)> SwitchToProfileAsync(Guid profileId, Guid subProfileId);

    Task<(bool Success, string Message)> SaveProfileToFileAsync(Guid profileId, string filePath);
    Task<(bool Success, string Message)> LoadProfileFromFileAsync(string filePath);

    Task<bool> HandleAddProfileWorkflowAsync();
    Task<bool> HandleAddSubProfileWorkflowAsync();
    Task<bool> HandleImportProfileWorkflowAsync();
    Task<bool> HandleExportProfileWorkflowAsync(GameProfile profile);
    Task<bool> HandleRenameProfileWorkflowAsync(GameProfile profile);
    Task<bool> HandleDeleteProfileWorkflowAsync(GameProfile profile);
    Task<bool> HandleRenameSubProfileWorkflowAsync(GameProfile profile, SubProfile subProfile);
    Task<bool> HandleDeleteSubProfileWorkflowAsync(GameProfile profile, SubProfile subProfile);
}
