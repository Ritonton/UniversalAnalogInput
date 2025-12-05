using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using UniversalAnalogInputUI.Services.Interfaces;

namespace UniversalAnalogInputUI.Dialogs;

/// <summary>Dialog for capturing keyboard hotkeys from user input.</summary>
public sealed partial class HotkeyAssignmentDialog : ContentDialog
{
    private readonly IRustInteropService _rustInteropService;
    private readonly TextBox _hotkeyTextBox;
    private bool _ctrlDown;
    private bool _altDown;
    private bool _shiftDown;

    /// <summary>Gets the captured hotkey string (ex: "Ctrl + Alt + F1").</summary>
    public string AssignedHotkey { get; private set; } = string.Empty;

    public HotkeyAssignmentDialog(string currentHotkey, IRustInteropService rustInteropService)
    {
        _rustInteropService = rustInteropService ?? throw new ArgumentNullException(nameof(rustInteropService));
        Title = "Assign Hotkey";
        PrimaryButtonText = "Assign";
        SecondaryButtonText = "Clear";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        AssignedHotkey = currentHotkey?.Trim() ?? string.Empty;

        var panel = new StackPanel { Spacing = 15 };
        var instructions = new TextBlock
        {
            Text = "Press the key combination you want to assign:",
            FontSize = 14
        };
        panel.Children.Add(instructions);

        _hotkeyTextBox = new TextBox
        {
            Text = AssignedHotkey,
            PlaceholderText = "Press keys...",
            IsReadOnly = true,
            IsHitTestVisible = false,
            IsTabStop = false,
            FontFamily = new FontFamily("Consolas")
        };
        panel.Children.Add(_hotkeyTextBox);

        var infoText = new TextBlock
        {
            Text = "Supported: F1-F12, Ctrl/Alt/Shift + key",
            FontSize = 11,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 156, 163, 175))
        };
        panel.Children.Add(infoText);

        SecondaryButtonClick += HotkeyAssignmentDialog_SecondaryButtonClick;
        Opened += HotkeyAssignmentDialog_Opened;
        Closed += HotkeyAssignmentDialog_Closed;

        AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(Dialog_KeyDown), true);
        AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(Dialog_KeyUp), true);

        Content = panel;
    }

    private void Dialog_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        e.Handled = true;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Control:
            case Windows.System.VirtualKey.LeftControl:
            case Windows.System.VirtualKey.RightControl:
                _ctrlDown = true;
                UpdatePreview();
                return;
            case Windows.System.VirtualKey.Menu:
            case Windows.System.VirtualKey.LeftMenu:
            case Windows.System.VirtualKey.RightMenu:
                _altDown = true;
                UpdatePreview();
                return;
            case Windows.System.VirtualKey.Shift:
            case Windows.System.VirtualKey.LeftShift:
            case Windows.System.VirtualKey.RightShift:
                _shiftDown = true;
                UpdatePreview();
                return;
            case Windows.System.VirtualKey.Escape:
                ClearAssignment();
                return;
        }

        string keyName = NormalizeKey(e.Key);
        if (string.IsNullOrEmpty(keyName))
        {
            UpdatePreview();
            return;
        }

        AssignedHotkey = BuildHotkeyString(keyName);
        _hotkeyTextBox.Text = AssignedHotkey;
    }

    private void Dialog_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Control:
            case Windows.System.VirtualKey.LeftControl:
            case Windows.System.VirtualKey.RightControl:
                _ctrlDown = false;
                break;
            case Windows.System.VirtualKey.Menu:
            case Windows.System.VirtualKey.LeftMenu:
            case Windows.System.VirtualKey.RightMenu:
                _altDown = false;
                break;
            case Windows.System.VirtualKey.Shift:
            case Windows.System.VirtualKey.LeftShift:
            case Windows.System.VirtualKey.RightShift:
                _shiftDown = false;
                break;
        }

        if (!_ctrlDown && !_altDown && !_shiftDown)
        {
            UpdatePreview();
        }
    }

    private string BuildHotkeyString(string keyName)
    {
        var parts = new List<string>();
        if (_ctrlDown) parts.Add("Ctrl");
        if (_altDown) parts.Add("Alt");
        if (_shiftDown) parts.Add("Shift");
        parts.Add(keyName);
        return string.Join(" + ", parts);
    }

    private void UpdatePreview()
    {
        if (_ctrlDown || _altDown || _shiftDown)
        {
            var parts = new List<string>();
            if (_ctrlDown) parts.Add("Ctrl");
            if (_altDown) parts.Add("Alt");
            if (_shiftDown) parts.Add("Shift");
            _hotkeyTextBox.Text = string.Join(" + ", parts);
        }
        else
        {
            _hotkeyTextBox.Text = AssignedHotkey;
        }
    }

    private static string NormalizeKey(Windows.System.VirtualKey key)
    {
        if (key >= Windows.System.VirtualKey.A && key <= Windows.System.VirtualKey.Z)
        {
            return key.ToString();
        }
        if (key >= Windows.System.VirtualKey.Number0 && key <= Windows.System.VirtualKey.Number9)
        {
            return ((char)('0' + (key - Windows.System.VirtualKey.Number0))).ToString();
        }
        if (key >= Windows.System.VirtualKey.F1 && key <= Windows.System.VirtualKey.F24)
        {
            return key.ToString();
        }
        if (key == Windows.System.VirtualKey.Space)
        {
            return "Space";
        }
        return string.Empty;
    }

    private void HotkeyAssignmentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        ClearAssignment();
    }

    private void ClearAssignment()
    {
        AssignedHotkey = string.Empty;
        _ctrlDown = _altDown = _shiftDown = false;
        _hotkeyTextBox.Text = string.Empty;
    }

    private void HotkeyAssignmentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        // Suspend global hotkeys to prevent interference during assignment
        _rustInteropService.SuspendHotkeys();
    }

    private void HotkeyAssignmentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        _rustInteropService.ResumeHotkeys();

        Opened -= HotkeyAssignmentDialog_Opened;
        Closed -= HotkeyAssignmentDialog_Closed;
    }
}
