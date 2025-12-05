using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using UniversalAnalogInputUI.Models;

namespace UniversalAnalogInputUI.Services.Interfaces;

/// <summary>Manages profile and sub-profile hotkey workflows.</summary>
public interface IHotkeyManagementService
{
    /// <summary>Raised when a profile or sub-profile hotkey changes.</summary>
    event EventHandler? HotkeyUpdated;

    Task<bool> HandleAssignProfileHotkeyWorkflowAsync();
    Task<bool> HandleClearProfileHotkeyWorkflowAsync();

    Task<bool> HandleAssignSubProfileHotkeyWorkflowAsync();
    Task<bool> HandleClearSubProfileHotkeyWorkflowAsync();
}
