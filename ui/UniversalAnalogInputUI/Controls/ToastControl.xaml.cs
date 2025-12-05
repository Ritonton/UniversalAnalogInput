using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using UniversalAnalogInputUI.Enums;
using Windows.UI;

namespace UniversalAnalogInputUI.Controls;

public sealed partial class ToastControl : UserControl
{
    private DispatcherTimer? _toastTimer;

    public ToastControl()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Displays toast notification with icon, colors, and auto-dismiss; thread-safe for background calls
    /// </summary>
    /// <param name="title">Notification title text</param>
    /// <param name="message">Notification body text</param>
    /// <param name="type">Toast type determining icon and color (Info, Success, Warning, Error)</param>
    /// <param name="durationMs">Auto-dismiss duration in milliseconds (default 4000ms)</param>
    public void ShowToast(string title, string message, ToastType type = ToastType.Info, int durationMs = 4000)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => ShowToast(title, message, type, durationMs));
            return;
        }

        _toastTimer?.Stop();

        string glyph;
        Microsoft.UI.Xaml.Media.SolidColorBrush brush;

        switch (type)
        {
            case ToastType.Success:
                glyph = "\uE73E";
                brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 22, 160, 133));
                break;
            case ToastType.Error:
                glyph = "\uE783";
                brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 220, 38, 38));
                break;
            case ToastType.Warning:
                glyph = "\uE7BA";
                brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 255, 193, 7));
                break;
            default:
                glyph = "\uE946";
                brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
                break;
        }

        ToastIcon.Glyph = glyph;
        ToastIcon.Foreground = brush;
        ToastTitle.Text = title ?? "";
        ToastMessage.Text = message ?? "";

        ToastNotification.IsOpen = true;

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            _toastTimer = null;
            ToastNotification.IsOpen = false;
        };
        _toastTimer.Start();
    }

    /// <summary>
    /// Dismisses toast immediately and cancels auto-dismiss timer
    /// </summary>
    public void HideToast()
    {
        _toastTimer?.Stop();
        _toastTimer = null;
        ToastNotification.IsOpen = false;
    }
}