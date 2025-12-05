using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace UniversalAnalogInputUI.Services.Interfaces;

/// <summary>Provides dialog prompts and hotkey capture.</summary>
public interface IDialogService
{
    /// <summary>Shows a text input dialog.</summary>
    /// <param name="title">Dialog title</param>
    /// <param name="placeholder">Placeholder text for input</param>
    /// <param name="initialText">Initial text value</param>
    /// <returns>The entered text, or null if canceled</returns>
    Task<string?> PromptForTextAsync(string title, string placeholder, string initialText = "");

    /// <summary>Shows a confirmation dialog.</summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    /// <returns>True if confirmed, false if canceled</returns>
    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>Shows a hotkey assignment dialog.</summary>
    /// <param name="currentHotkey">Current hotkey value</param>
    /// <returns>Tuple with success flag and the assigned hotkey (empty if cleared)</returns>
    Task<(bool Success, string Hotkey)> ShowHotkeyAssignmentAsync(string currentHotkey);
}
