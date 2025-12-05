using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UniversalAnalogInputUI.Models;

namespace UniversalAnalogInputUI.Services.Interfaces;

/// <summary>Coordinates key mapping CRUD, validation, and Rust synchronization.</summary>
public interface IMappingManagementService
{
    /// <summary>Current mappings collection for UI binding.</summary>
    ObservableCollection<KeyMapping> CurrentMappings { get; }

    /// <summary>Available keyboard keys for selection.</summary>
    List<string> AvailableKeys { get; }

    /// <summary>Available gamepad controls for selection.</summary>
    List<string> GamepadControls { get; }

    /// <summary>Current mapping count.</summary>
    int MappingCount { get; }

    /// <summary>Initializes the service and loads dynamic lists from Rust.</summary>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    Task<bool> InitializeAsync();

    /// <summary>Loads mappings for the specified profile and sub-profile from Rust.</summary>
    /// <param name="profile">The profile containing the mappings.</param>
    /// <param name="subProfile">The sub-profile containing the mappings.</param>
    Task LoadMappingsAsync(GameProfile profile, SubProfile subProfile);

    /// <summary>Clears all current mappings from the UI.</summary>
    void ClearMappings();

    /// <summary>Adds a new empty mapping after validating existing entries.</summary>
    /// <returns>True if mapping was added successfully, false if blocked by invalid mappings.</returns>
    bool AddMapping();

    /// <summary>Removes a specific mapping from both UI and Rust.</summary>
    /// <param name="mapping">The mapping to remove.</param>
    /// <returns>True if removal succeeded, false otherwise.</returns>
    Task<bool> RemoveMappingAsync(KeyMapping mapping);

    /// <summary>Removes all selected mappings in bulk.</summary>
    /// <param name="selectedMappings">The mappings to remove.</param>
    /// <returns>The number of mappings successfully removed.</returns>
    Task<int> RemoveSelectedMappingsAsync(IEnumerable<KeyMapping> selectedMappings);

    /// <summary>Validates and synchronizes a mapping after UI changes.</summary>
    /// <param name="mapping">The mapping that was modified.</param>
    void ValidateAndSyncMapping(KeyMapping mapping);

    /// <summary>Cleans up pending mappings associated with a deleted profile.</summary>
    /// <param name="profileId">The ID of the deleted profile.</param>
    void CleanupPendingMappingsForProfile(Guid profileId);

    /// <summary>Cleans up pending mappings associated with a deleted sub-profile.</summary>
    /// <param name="profileId">The profile ID containing the sub-profile.</param>
    /// <param name="subProfileId">The ID of the deleted sub-profile.</param>
    void CleanupPendingMappingsForSubProfile(Guid profileId, Guid subProfileId);

    /// <summary>Synchronizes a mapping to Rust (e.g., curve or dead zone updates).</summary>
    /// <param name="mapping">The mapping to synchronize.</param>
    void SyncMappingToRust(KeyMapping mapping);
}
