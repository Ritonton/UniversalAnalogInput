using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using UniversalAnalogInputUI.Interop;
using UniversalAnalogInputUI.Models;
using UniversalAnalogInputUI.Services.Interfaces;
using UniversalAnalogInputUI.Enums;
using UniversalAnalogInputUI.Helpers;
using UniversalAnalogInputUI.Controls;

namespace UniversalAnalogInputUI.Services;

/// <summary>
/// Manages key mappings with CRUD operations, validation, conflict resolution, and Rust synchronization.
/// </summary>
public class MappingManagementService : IMappingManagementService
{
    private readonly IRustInteropService _rustService;
    private readonly IProfileManagementService _profileService;
    private readonly IToastService _toastService;
    private readonly IStatusMonitorService _statusMonitorService;

    private readonly ObservableCollection<KeyMapping> _currentMappings;
    private readonly List<string> _availableKeys;
    private readonly List<string> _gamepadControls;
    private readonly Dictionary<DateTime, PendingMappingState> _pendingMappingOverrides;

    private bool _isLoadingFromRust = false;
    private bool _isUpdatingList = false;
    private bool _isValidatingMapping = false;
    private bool _isFlushingPending = false;
    private long _mappingCreationCounter = 0;

    public MappingManagementService(
        IRustInteropService rustService,
        IProfileManagementService profileService,
        IToastService toastService,
        IStatusMonitorService statusMonitorService)
    {
        _rustService = rustService ?? throw new ArgumentNullException(nameof(rustService));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
        _statusMonitorService = statusMonitorService ?? throw new ArgumentNullException(nameof(statusMonitorService));

        _currentMappings = new ObservableCollection<KeyMapping>();
        _availableKeys = new List<string>();
        _gamepadControls = new List<string>();
        _pendingMappingOverrides = new Dictionary<DateTime, PendingMappingState>();

        _currentMappings.CollectionChanged += OnCurrentMappingsCollectionChanged;
    }

    public ObservableCollection<KeyMapping> CurrentMappings => _currentMappings;
    public List<string> AvailableKeys => _availableKeys;
    public List<string> GamepadControls => _gamepadControls;
    public int MappingCount => _currentMappings?.Count ?? 0;

    public async Task<bool> InitializeAsync()
    {
        _statusMonitorService.AppendLiveInput("Initializing mapping service.");

        try
        {
            bool success = await Task.Run(() => LoadDynamicListsFromRust()).ConfigureAwait(false);
            if (!success)
            {
                _statusMonitorService.AppendLiveInput("Failed to load available keys and controls from Rust.");
                _toastService.ShowToast("Configuration Error", "Failed to load system configuration", ToastType.Error);
                return false;
            }

            _statusMonitorService.AppendLiveInput($"Loaded {_availableKeys.Count} keys and {_gamepadControls.Count} gamepad controls.");
            return true;
        }
        catch (Exception ex)
        {
            _statusMonitorService.AppendLiveInput($"Mapping service initialization error: {ex.Message}");
            _toastService.ShowToast("System Error", "Mapping service initialization failed", ToastType.Error);
            return false;
        }
    }

    public Task LoadMappingsAsync(GameProfile profile, SubProfile subProfile)
    {
        _statusMonitorService.AppendLiveInput($"Loading mappings for {profile.Name} :: {subProfile.Name}.");

        try
        {
            uint mappingCount = _rustService.GetCurrentMappingCount();
            var loadedMappings = LoadCurrentMappingsFromRust(mappingCount, profile.Id, subProfile.Id);

            subProfile.Mappings = loadedMappings;

            _isUpdatingList = true;
            _currentMappings.Clear();
            foreach (var mapping in loadedMappings)
            {
                _currentMappings.Add(mapping);
            }

            _statusMonitorService.AppendLiveInput($"Loaded {mappingCount} mappings for {profile.Name} :: {subProfile.Name}.");

            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _isUpdatingList = false;
            });

            CheckForDuplicateKeys();
            _statusMonitorService.UpdateMappingCount(_currentMappings.Count);
        }
        catch (Exception ex)
        {
            _statusMonitorService.AppendLiveInput($"Failed to load mappings: {ex.Message}");
            _toastService.ShowToast("Loading Error", "Failed to load mappings from system", ToastType.Error);
        }

        return Task.CompletedTask;
    }

    public void ClearMappings()
    {
        _currentMappings.Clear();
        _statusMonitorService.UpdateMappingCount(_currentMappings.Count);
    }

    public bool AddMapping()
    {
        _statusMonitorService.AppendLiveInput("Validating mappings before adding a new entry.");

        if (_currentMappings.Any(m => !m.IsValid))
        {
            _toastService.ShowToast("Mapping In Progress", "Complete current mapping first", ToastType.Warning);
            _statusMonitorService.AppendLiveInput("Complete existing mappings before adding another.");
            return false;
        }

        _statusMonitorService.AppendLiveInput("Creating new mapping entry.");

        _isUpdatingList = true;

        var baseTime = DateTime.UtcNow;
        var counter = System.Threading.Interlocked.Increment(ref _mappingCreationCounter);
        var uniqueTime = baseTime.AddTicks(counter % 10000);

        var newMapping = new KeyMapping
        {
            CreatedAt = uniqueTime
        };

        _currentMappings.Add(newMapping);
        _profileService.SelectedSubProfile?.Mappings.Add(newMapping);

        _statusMonitorService.AppendLiveInput("New mapping added; select key and gamepad control.");
        _statusMonitorService.UpdateMappingCount(_currentMappings.Count);

        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _isUpdatingList = false;
        });

        return true;
    }

    public Task<bool> RemoveMappingAsync(KeyMapping mapping)
    {
        _statusMonitorService.AppendLiveInput($"Removing mapping {mapping.KeyName ?? "empty"} -> {mapping.GamepadControl ?? "empty"}.");

        _isUpdatingList = true;

        try
        {
            var removedFromCurrent = _currentMappings.Remove(mapping);
            var removedFromSub = _profileService.SelectedSubProfile?.Mappings.Remove(mapping) ?? false;

            if (removedFromCurrent || removedFromSub)
            {
                _statusMonitorService.AppendLiveInput("Synchronizing removal with Rust.");

                if (!TryGetMappingContext(out var profileId, out var subId, out var profileObj, out var subObj))
                {
                    _statusMonitorService.AppendLiveInput("No active profile or sub-profile for Rust sync.");
                    _isUpdatingList = false;
                    return Task.FromResult(false);
                }

                string keyToRemove = GetRemovalKeyForMapping(mapping);

                if (!string.IsNullOrEmpty(keyToRemove) && profileObj != null && subObj != null)
                {
                    var removeResult = _rustService.RemoveMapping(profileObj.Id, subObj.Id, keyToRemove);
                    if (removeResult != 0)
                    {
                        _statusMonitorService.AppendLiveInput($"Failed to remove mapping in Rust: {keyToRemove}.");
                    }
                    else
                    {
                        _statusMonitorService.AppendLiveInput($"Removed mapping in Rust: {keyToRemove} -> {mapping.GamepadControl}.");
                    }
                }
                else
                {
                    _statusMonitorService.AppendLiveInput("Removed invalid mapping from UI (not in Rust).");
                }

                _pendingMappingOverrides.Remove(mapping.CreatedAt);
                CheckForDuplicateKeys();

                _statusMonitorService.AppendLiveInput("Mapping deleted.");
                _statusMonitorService.UpdateMappingCount(_currentMappings.Count);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        finally
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _isUpdatingList = false;
            });
        }
    }

    public Task<int> RemoveSelectedMappingsAsync(IEnumerable<KeyMapping> selectedMappings)
    {
        var mappingsToRemove = selectedMappings.ToList();
        if (mappingsToRemove.Count == 0) return Task.FromResult(0);

        _statusMonitorService.AppendLiveInput($"Removing {mappingsToRemove.Count} selected mappings.");

        _isUpdatingList = true;

        try
        {
            if (!TryGetMappingContext(out var profileId, out var subId, out var profileObj, out var subObj))
            {
                _statusMonitorService.AppendLiveInput("No active profile or sub-profile.");
                _isUpdatingList = false;
                return Task.FromResult(0);
            }

            bool hasValidContext = profileObj != null && subObj != null;
            int deletedCount = 0;

            _statusMonitorService.AppendLiveInput("Processing bulk removal.");

            foreach (var mapping in mappingsToRemove)
            {
                _currentMappings.Remove(mapping);
                _profileService.SelectedSubProfile?.Mappings.Remove(mapping);

                string keyToRemove = GetRemovalKeyForMapping(mapping);

                if (!string.IsNullOrEmpty(keyToRemove) && hasValidContext && profileObj != null && subObj != null)
                {
                    var result = _rustService.RemoveMapping(profileObj.Id, subObj.Id, keyToRemove);
                    if (result == 0)
                    {
                        _statusMonitorService.AppendLiveInput($"Removed from Rust: {keyToRemove} -> {mapping.GamepadControl}.");
                    }
                    else
                    {
                        _statusMonitorService.AppendLiveInput($"Failed to remove from Rust: {keyToRemove}.");
                    }
                }
                else
                {
                    if (!hasValidContext)
                    {
                        _statusMonitorService.AppendLiveInput("No valid context for Rust removal.");
                    }
                    _statusMonitorService.AppendLiveInput("Removed invalid mapping from UI.");
                }

                _pendingMappingOverrides.Remove(mapping.CreatedAt);
                deletedCount++;
            }

            CheckForDuplicateKeys();

            _statusMonitorService.AppendLiveInput($"Deleted {deletedCount} selected mapping{(deletedCount != 1 ? "s" : "")}.");
            _statusMonitorService.UpdateMappingCount(_currentMappings.Count);

            return Task.FromResult(deletedCount);
        }
        finally
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                _isUpdatingList = false;
            });
        }
    }

    public void ValidateAndSyncMapping(KeyMapping mapping)
    {
        if (_isLoadingFromRust || _isUpdatingList) return;

        if (!mapping.HasBeenModified && !_pendingMappingOverrides.ContainsKey(mapping.CreatedAt))
        {
            return;
        }

        if (mapping.IsValid)
        {
            _isValidatingMapping = true;
            try
            {
                CheckForDuplicateKeys();

                if (mapping.HasWarning)
                {
                    if (_profileService.SelectedProfile != null && _profileService.SelectedSubProfile != null)
                    {
                        if (_pendingMappingOverrides.TryGetValue(mapping.CreatedAt, out var existingPending))
                        {
                            existingPending.KeyName = mapping.KeyName;
                            existingPending.GamepadControl = mapping.GamepadControl;
                            existingPending.ResponseCurve = mapping.Curve;
                            existingPending.DeadZoneInner = mapping.DeadZoneInner;
                            existingPending.DeadZoneOuter = mapping.DeadZoneOuter;
                        }
                        else
                        {
                            _pendingMappingOverrides[mapping.CreatedAt] = new PendingMappingState
                            {
                                ProfileId = _profileService.SelectedProfile.Id,
                                SubProfileId = _profileService.SelectedSubProfile.Id,
                                KeyName = mapping.KeyName,
                                GamepadControl = mapping.GamepadControl,
                                ResponseCurve = mapping.Curve,
                                DeadZoneInner = mapping.DeadZoneInner,
                                DeadZoneOuter = mapping.DeadZoneOuter,
                                OriginalKeyInRust = mapping._originalKeyName ?? ""
                            };
                        }
                    }

                    _toastService.ShowToast("Duplicate Key", $"Key {mapping.KeyName} is already mapped", ToastType.Warning);
                    _statusMonitorService.AppendLiveInput($"Duplicate key detected; stored as pending: {mapping.KeyName}.");
                }
                else
                {
                    var duplicateControl = _currentMappings
                        .Where(m => m != mapping && m.IsValid && !string.IsNullOrEmpty(m.GamepadControl))
                        .FirstOrDefault(m => m.GamepadControl == mapping.GamepadControl);

                    if (duplicateControl != null)
                    {
                        _toastService.ShowToast("Multiple Keys", $"Keys for {mapping.GamepadControl} will be combined", ToastType.Info);
                        _statusMonitorService.AppendLiveInput($"Multiple keys mapped to {mapping.GamepadControl}: values will be combined using max().");
                    }

                    SynchronizeSingleMappingToRust(mapping);
                }
            }
            finally
            {
                _isValidatingMapping = false;
                CheckForDuplicateKeys();
            }
        }
    }

    public void CleanupPendingMappingsForProfile(Guid profileId)
    {
        var keysToRemove = _pendingMappingOverrides
            .Where(kvp => kvp.Value.ProfileId == profileId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _pendingMappingOverrides.Remove(key);
        }

        if (keysToRemove.Count > 0)
        {
            _statusMonitorService.AppendLiveInput($"Removed {keysToRemove.Count} pending mapping(s) for deleted profile.");
        }
    }

    public void CleanupPendingMappingsForSubProfile(Guid profileId, Guid subProfileId)
    {
        var keysToRemove = _pendingMappingOverrides
            .Where(kvp => kvp.Value.ProfileId == profileId && kvp.Value.SubProfileId == subProfileId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _pendingMappingOverrides.Remove(key);
        }

        if (keysToRemove.Count > 0)
        {
            _statusMonitorService.AppendLiveInput($"Removed {keysToRemove.Count} pending mapping(s) for deleted sub-profile.");
        }
    }

    private bool LoadDynamicListsFromRust()
    {
        try
        {
            _availableKeys.Clear();
            _gamepadControls.Clear();

            uint keyCount = _rustService.GetSupportedKeyCount();
            for (uint i = 0; i < keyCount; i++)
            {
                string name = _rustService.GetSupportedKeyName(i);
                if (!string.IsNullOrEmpty(name))
                {
                    _availableKeys.Add(name);
                }
            }

            uint controlCount = _rustService.GetGamepadControlCount();
            for (uint i = 0; i < controlCount; i++)
            {
                string name = _rustService.GetGamepadControlName(i);
                if (!string.IsNullOrEmpty(name))
                {
                    _gamepadControls.Add(name);
                }
            }

            return _availableKeys.Count > 0 && _gamepadControls.Count > 0;
        }
        catch (Exception ex)
        {
            _statusMonitorService.AppendLiveInput($"Failed to load available keys and controls from Rust: {ex.Message}");
            return false;
        }
    }

    private ObservableCollection<KeyMapping> LoadCurrentMappingsFromRust(uint mappingCount, Guid profileId, Guid subProfileId)
    {
        _isLoadingFromRust = true;

        var tempList = new List<(int Index, KeyMapping Mapping)>();

        for (uint mIndex = 0; mIndex < mappingCount; mIndex++)
        {
            if (_rustService.GetCurrentMappingInfo(mIndex, out var info) != 0)
            {
                continue;
            }

            var createdAt = info.CreatedAt != 0
                ? DateTimeOffset.FromUnixTimeSeconds((long)info.CreatedAt).UtcDateTime
                : DateTime.MinValue;

            var customCurvePoints = new List<(double X, double Y)>();
            if (info.CustomPointCount > 0 && info.CustomPoints != null)
            {
                int pointCount = Math.Min((int)info.CustomPointCount, 16);
                for (int i = 0; i < pointCount; i++)
                {
                    var point = info.CustomPoints[i];
                    customCurvePoints.Add((point.X, point.Y));
                }
            }

            var mapping = new KeyMapping
            {
                KeyName = RustDataConverter.DecodeUtf8(info.KeyName),
                GamepadControl = RustDataConverter.DecodeUtf8(info.GamepadControl),
                Curve = RustDataConverter.DecodeUtf8(info.ResponseCurve),
                DeadZoneInner = info.DeadZoneInner,
                DeadZoneOuter = info.DeadZoneOuter,
                UseSmoothCurve = info.UseSmoothCurve != 0,
                CustomCurvePoints = customCurvePoints.Count > 0 ? customCurvePoints : null,
                CreatedAt = createdAt
            };
            tempList.Add(((int)mIndex, mapping));
        }

        var ordered = tempList
            .OrderBy(t => t.Mapping.CreatedAt == DateTime.MinValue)
            .ThenBy(t => t.Mapping.CreatedAt)
            .ThenBy(t => t.Index)
            .Select(t => t.Mapping)
            .ToList();

        var mappings = new ObservableCollection<KeyMapping>();
        var processedPendingKeys = new HashSet<DateTime>();
        var usedTimestamps = new HashSet<DateTime>();
        var hadTimestampFixes = false;

        foreach (var mapping in ordered)
        {
            var originalTimestamp = mapping.CreatedAt;

            // Fix timestamp collisions by adding milliseconds
            while (usedTimestamps.Contains(mapping.CreatedAt))
            {
                mapping.CreatedAt = mapping.CreatedAt.AddMilliseconds(1);
                hadTimestampFixes = true;
                _statusMonitorService.AppendLiveInput($"Resolved timestamp collision for mapping {mapping.KeyName}: {originalTimestamp:HH:mm:ss.fff} -> {mapping.CreatedAt:HH:mm:ss.fff}.");
            }
            usedTimestamps.Add(mapping.CreatedAt);

            mapping.MarkAsOriginal();

            PendingMappingState? pendingState = null;
            bool foundPending = false;

            if (_pendingMappingOverrides.TryGetValue(mapping.CreatedAt, out pendingState))
            {
                foundPending = true;
            }
            else if (originalTimestamp != mapping.CreatedAt &&
                     _pendingMappingOverrides.TryGetValue(originalTimestamp, out pendingState))
            {
                foundPending = true;
                _pendingMappingOverrides.Remove(originalTimestamp);
                _pendingMappingOverrides[mapping.CreatedAt] = pendingState;
                _statusMonitorService.AppendLiveInput($"Remapped pending state: {originalTimestamp} -> {mapping.CreatedAt}.");
            }

            if (foundPending && pendingState != null &&
                pendingState.ProfileId == profileId &&
                pendingState.SubProfileId == subProfileId)
            {
                mapping.KeyName = pendingState.KeyName;
                mapping.GamepadControl = pendingState.GamepadControl;
                mapping.Curve = pendingState.ResponseCurve;
                mapping.DeadZoneInner = pendingState.DeadZoneInner;
                mapping.DeadZoneOuter = pendingState.DeadZoneOuter;
                processedPendingKeys.Add(mapping.CreatedAt);
            }

            mappings.Add(mapping);
        }

        foreach (var kvp in _pendingMappingOverrides)
        {
            if (!processedPendingKeys.Contains(kvp.Key))
            {
                var pendingState = kvp.Value;

                if (pendingState.ProfileId == profileId && pendingState.SubProfileId == subProfileId)
                {
                    var orphanTimestamp = kvp.Key;
                    while (usedTimestamps.Contains(orphanTimestamp))
                    {
                        orphanTimestamp = orphanTimestamp.AddMilliseconds(1);
                        _statusMonitorService.AppendLiveInput($"Resolved orphan timestamp collision: {orphanTimestamp:HH:mm:ss.fff}.");
                    }
                    usedTimestamps.Add(orphanTimestamp);

                    var orphanMapping = new KeyMapping
                    {
                        KeyName = pendingState.KeyName,
                        GamepadControl = pendingState.GamepadControl,
                        Curve = pendingState.ResponseCurve,
                        DeadZoneInner = pendingState.DeadZoneInner,
                        DeadZoneOuter = pendingState.DeadZoneOuter,
                        CreatedAt = orphanTimestamp
                    };
                    mappings.Add(orphanMapping);
                }
            }
        }

        if (hadTimestampFixes)
        {
            _statusMonitorService.AppendLiveInput("Timestamp collision fixes applied in memory.");
        }

        Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _isLoadingFromRust = false;
        });

        return mappings;
    }

    private void CheckForDuplicateKeys()
    {
        foreach (var m in _currentMappings)
        {
            m.HasWarning = false;
        }

        var duplicateGroups = _currentMappings
            .Where(m => m.IsValid && !string.IsNullOrEmpty(m.KeyName))
            .GroupBy(m => m.KeyName)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var group in duplicateGroups)
        {
            foreach (var mapping in group)
            {
                mapping.HasWarning = true;
            }
        }

        if (!_isValidatingMapping && !_isFlushingPending && _profileService.SelectedProfile != null && _profileService.SelectedSubProfile != null)
        {
            _isFlushingPending = true;
            var pendingKeys = _pendingMappingOverrides
                .Where(kvp => kvp.Value.ProfileId == _profileService.SelectedProfile.Id &&
                             kvp.Value.SubProfileId == _profileService.SelectedSubProfile.Id)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var createdAt in pendingKeys)
            {
                var pendingMappings = _currentMappings.Where(m => m.CreatedAt == createdAt).ToList();
                foreach (var pendingMapping in pendingMappings)
                {
                    if (!pendingMapping.HasWarning)
                    {
                        TryFlushPendingMapping(pendingMapping);
                        break;
                    }
                }
            }
            _isFlushingPending = false;
        }
    }

    private bool TryGetMappingContext(out Guid profileId, out Guid subId, out GameProfile? profileObj, out SubProfile? subObj)
    {
        if (_profileService.SelectedProfile != null && _profileService.SelectedSubProfile != null)
        {
            profileId = _profileService.SelectedProfile.Id;
            subId = _profileService.SelectedSubProfile.Id;
            profileObj = _profileService.SelectedProfile;
            subObj = _profileService.SelectedSubProfile;
            return true;
        }
        else if (_profileService.TryGetActiveProfileAndSubFromRust(out profileId, out subId, out string profileName, out string subName))
        {
            profileObj = _profileService.FindProfileById(profileId);
            subObj = _profileService.FindSubProfileById(profileObj, subId);
            return profileObj != null && subObj != null;
        }
        else
        {
            profileId = Guid.Empty;
            subId = Guid.Empty;
            profileObj = null;
            subObj = null;
            return false;
        }
    }

    private string GetRemovalKeyForMapping(KeyMapping mapping)
    {
        if (_pendingMappingOverrides.TryGetValue(mapping.CreatedAt, out var pendingState))
        {
            if (_profileService.SelectedProfile != null && _profileService.SelectedSubProfile != null &&
                pendingState.ProfileId == _profileService.SelectedProfile.Id &&
                pendingState.SubProfileId == _profileService.SelectedSubProfile.Id)
            {
                return pendingState.OriginalKeyInRust;
            }
        }

        return mapping.KeyName;
    }

    private void TryFlushPendingMapping(KeyMapping mapping)
    {
        if (!_pendingMappingOverrides.TryGetValue(mapping.CreatedAt, out var pendingState))
        {
            return;
        }

        _statusMonitorService.AppendLiveInput($"Pending mapping resolved: {pendingState.KeyName} -> {mapping.KeyName}.");

        try
        {
            _statusMonitorService.AppendLiveInput($"Flushing resolved pending mapping for {mapping.KeyName}.");

            SynchronizeSingleMappingToRust(mapping);

            _statusMonitorService.AppendLiveInput($"Pending mapping flushed: {mapping.KeyName}.");
        }
        catch (Exception ex)
        {
            _statusMonitorService.AppendLiveInput($"Failed to flush pending mapping: {ex.Message}");
            return;
        }

        _pendingMappingOverrides.Remove(mapping.CreatedAt);
    }

    /// <summary>
    /// Synchronizes a mapping to Rust.
    /// </summary>
    public void SyncMappingToRust(KeyMapping mapping)
    {
        SynchronizeSingleMappingToRust(mapping);
    }

    private void SynchronizeSingleMappingToRust(KeyMapping mapping)
    {
        try
        {
            if (!TryGetMappingContext(out var profileId, out var subId, out var profileObj, out var subObj))
            {
                _statusMonitorService.AppendLiveInput("No active profile or sub-profile.");
                return;
            }

            if (mapping.HasBeenModified && !string.IsNullOrEmpty(mapping._originalKeyName))
            {
                bool keyTakenByOther = _currentMappings
                    .Any(m => m != mapping && m.IsValid && m.KeyName == mapping._originalKeyName);

                if (!keyTakenByOther && profileObj != null && subObj != null)
                {
                    var removeResult = _rustService.RemoveMapping(profileObj.Id, subObj.Id, mapping._originalKeyName);
                    if (removeResult == 0)
                    {
                        _statusMonitorService.AppendLiveInput($"Removed original mapping: {mapping._originalKeyName}.");
                    }
                }
                else
                {
                    _statusMonitorService.AppendLiveInput($"Original key now used by another mapping: {mapping._originalKeyName}.");
                }
            }

            var customPoints = new CCurvePoint[16];
            byte customPointCount = 0;

            if (mapping.CustomCurvePoints != null && mapping.CustomCurvePoints.Count > 0)
            {
                customPointCount = (byte)Math.Min(mapping.CustomCurvePoints.Count, 16);
                for (int i = 0; i < customPointCount; i++)
                {
                    var point = mapping.CustomCurvePoints[i];
                    customPoints[i] = new CCurvePoint
                    {
                        X = (float)point.X,
                        Y = (float)point.Y
                    };
                }
            }

            for (int i = customPointCount; i < 16; i++)
            {
                customPoints[i] = new CCurvePoint { X = 0.0f, Y = 0.0f };
            }

            var info = new CMappingInfo
            {
                KeyName = new byte[32],
                GamepadControl = new byte[32],
                ResponseCurve = new byte[32],
                DeadZoneInner = (float)mapping.DeadZoneInner,
                DeadZoneOuter = (float)mapping.DeadZoneOuter,
                UseSmoothCurve = mapping.UseSmoothCurve ? (byte)1 : (byte)0,
                CustomPointCount = customPointCount,
                CustomPoints = customPoints,
                CreatedAt = mapping.CreatedAt == DateTime.MinValue
                    ? 0UL
                    : (ulong)new DateTimeOffset(mapping.CreatedAt.ToUniversalTime()).ToUnixTimeSeconds()
            };

            var keyBytes = System.Text.Encoding.UTF8.GetBytes(mapping.KeyName ?? "");
            Array.Copy(keyBytes, info.KeyName, Math.Min(keyBytes.Length, 31));
            var controlBytes = System.Text.Encoding.UTF8.GetBytes(mapping.GamepadControl ?? "");
            Array.Copy(controlBytes, info.GamepadControl, Math.Min(controlBytes.Length, 31));
            var curveBytes = System.Text.Encoding.UTF8.GetBytes(mapping.Curve ?? "");
            Array.Copy(curveBytes, info.ResponseCurve, Math.Min(curveBytes.Length, 31));

            if (profileObj != null && subObj != null)
            {
                var temp = info;
                var result = _rustService.SetMapping(profileObj.Id, subObj.Id, ref temp);
                if (result == 0)
                {
                    mapping.MarkAsOriginal();
                    _statusMonitorService.AppendLiveInput($"Synced mapping to Rust: {mapping.KeyName} -> {mapping.GamepadControl}.");
                }
                else
                {
                    _statusMonitorService.AppendLiveInput($"Failed to sync mapping to Rust: {mapping.KeyName}.");
                }
            }
        }
        catch (Exception ex)
        {
            _statusMonitorService.AppendLiveInput($"Sync error: {ex.Message}");
        }
    }

    private void OnCurrentMappingsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _statusMonitorService.UpdateMappingCount(_currentMappings.Count);

        if (_isLoadingFromRust || _isUpdatingList)
        {
            return;
        }

        CheckForDuplicateKeys();
    }
}
