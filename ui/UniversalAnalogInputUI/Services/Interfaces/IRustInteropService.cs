using System;
using UniversalAnalogInputUI.Interop;

namespace UniversalAnalogInputUI.Services.Interfaces;

/// <summary>Provides a testable abstraction over P/Invoke calls to the Rust library.</summary>
public interface IRustInteropService
{
    void Cleanup();
    string GetVersion();

    int StartMapping();
    int StopMapping();
    bool IsMappingActive();

    int CreateProfile(string profileName, string description);
    int DeleteProfile(Guid profileId);
    int RenameProfile(Guid profileId, string newName);
    int UpdateProfileDescription(Guid profileId, string description);
    int UpdateProfileHotkey(Guid profileId, string hotkey);
    int SwitchProfile(Guid profileId, Guid subProfileId);

    int AddSubProfile(Guid profileId, string subProfileName, string description, string hotkey);
    int RenameSubProfile(Guid profileId, Guid subProfileId, string newSubProfileName);
    int DeleteSubProfile(Guid profileId, Guid subProfileId);
    int UpdateSubProfileHotkey(Guid profileId, Guid subProfileId, string hotkey);

    int SuspendHotkeys();
    int ResumeHotkeys();

    int SetMapping(Guid profileId, Guid subProfileId, ref CMappingInfo mapping);
    int RemoveMapping(Guid profileId, Guid subProfileId, string keyName);
    uint GetCurrentMappingCount();
    int GetCurrentMappingInfo(uint index, out CMappingInfo info);

    uint GetProfileMetadataCount();
    int GetProfileMetadata(uint index, out CProfileMetadata metadata);
    int GetSubProfileMetadata(uint profileIndex, uint subIndex, out CSubProfileMetadata metadata);

    uint GetSupportedKeyCount();
    string GetSupportedKeyName(uint index);
    uint GetGamepadControlCount();
    string GetGamepadControlName(uint index);

    int SaveProfileToFile(Guid profileId, string filePath);
    int LoadProfileFromFile(string filePath);

    uint GetUiMessageId();
    uint GetUiEventTypeSubProfileSwitch();
    void RegisterUiWindow(IntPtr hwnd);
    bool NextUiEvent(out CUiEvent evt);

    Models.PerformanceMetrics? GetPerformanceMetrics();
}
