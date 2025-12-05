using UniversalAnalogInputUI.Controls;
using UniversalAnalogInputUI.Enums;
using UniversalAnalogInputUI.Services.Interfaces;

namespace UniversalAnalogInputUI.Services;

/// <summary>Coordinates toast notifications shown inside the UI shell.</summary>
public class ToastService : IToastService
{
    private ToastControl? _toastControl;

    /// <summary>Registers the toast control instance created by the UI.</summary>
    public void SetToastControl(ToastControl toastControl)
    {
        _toastControl = toastControl;
    }

    /// <summary>Displays a toast with the specified content and duration.</summary>
    public void ShowToast(string title, string message, ToastType type = ToastType.Info, int durationMs = 4000)
    {
        _toastControl?.ShowToast(title, message, type, durationMs);
    }

    /// <summary>Hides any currently visible toast.</summary>
    public void HideToast()
    {
        _toastControl?.HideToast();
    }
}
