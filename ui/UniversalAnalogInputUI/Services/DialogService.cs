using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UniversalAnalogInputUI.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace UniversalAnalogInputUI.Services;

/// <summary>Provides WinUI dialogs for text prompts, confirmations, and hotkey capture.</summary>
public class DialogService : IDialogService
{
    private readonly Func<XamlRoot?> _xamlRootProvider;
    private readonly IRustInteropService _rustInteropService;

    /// <summary>Creates a new dialog service.</summary>
    /// <param name="xamlRootProvider">Lazy provider for XamlRoot (required for WinUI 3 dialogs)</param>
    /// <param name="rustInteropService">Service for suspending/resuming global hotkeys during hotkey assignment</param>
    public DialogService(Func<XamlRoot?> xamlRootProvider, IRustInteropService rustInteropService)
    {
        _xamlRootProvider = xamlRootProvider ?? throw new ArgumentNullException(nameof(xamlRootProvider));
        _rustInteropService = rustInteropService ?? throw new ArgumentNullException(nameof(rustInteropService));
    }

    private void ApplyThemeToDialog(ContentDialog dialog)
    {
        try
        {
            var themeService = MainWindow.Instance?.ThemeServiceInstance;
            if (themeService == null) return;

            themeService.ApplyThemeToElement(dialog);

            if (dialog.Content is FrameworkElement content)
            {
                themeService.ApplyThemeToElement(content);
            }
        }
        catch
        {
            // Theme adjustments are best-effort to avoid blocking dialogs.
        }
    }

    /// <summary>Shows a text input dialog for profile/sub-profile names.</summary>
    /// <param name="title">Dialog title</param>
    /// <param name="placeholder">Placeholder text for input</param>
    /// <param name="initialText">Initial text value (for rename operations)</param>
    /// <returns>Entered text if confirmed, null if canceled</returns>
    public async Task<string?> PromptForTextAsync(string title, string placeholder, string initialText = "")
    {
        var xamlRoot = _xamlRootProvider();
        if (xamlRoot == null)
        {
            throw new InvalidOperationException("XamlRoot is not available - ensure the window is fully initialized");
        }

        var input = new TextBox
        {
            PlaceholderText = placeholder,
            Text = initialText,
            Width = 300
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = input,
            PrimaryButtonText = "OK",
            SecondaryButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        ApplyThemeToDialog(dialog);

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? input.Text?.Trim() : null;
    }

    /// <summary>Shows a confirmation dialog for destructive operations.</summary>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Confirmation message</param>
    /// <returns>True if user confirmed, false if canceled</returns>
    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var xamlRoot = _xamlRootProvider();
        if (xamlRoot == null)
        {
            throw new InvalidOperationException("XamlRoot is not available - ensure the window is fully initialized");
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
            PrimaryButtonText = "Confirm",
            SecondaryButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Secondary,
            XamlRoot = xamlRoot
        };

        ApplyThemeToDialog(dialog);

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    /// <summary>Shows hotkey assignment dialog that suspends global hotkey detection while open.</summary>
    /// <param name="currentHotkey">Current hotkey value to display (empty for new assignment)</param>
    /// <returns>Success flag and captured hotkey string (empty if cleared, unchanged if canceled)</returns>
    public async Task<(bool Success, string Hotkey)> ShowHotkeyAssignmentAsync(string currentHotkey)
    {
        var xamlRoot = _xamlRootProvider();
        if (xamlRoot == null)
        {
            throw new InvalidOperationException("XamlRoot is not available - ensure the window is fully initialized");
        }

        var dialog = new UniversalAnalogInputUI.Dialogs.HotkeyAssignmentDialog(currentHotkey, _rustInteropService)
        {
            XamlRoot = xamlRoot
        };

        ApplyThemeToDialog(dialog);

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            return (true, dialog.AssignedHotkey?.Trim() ?? string.Empty);
        }
        else if (result == ContentDialogResult.Secondary)
        {
            return (true, string.Empty);
        }
        else
        {
            return (false, string.Empty);
        }
    }
}
