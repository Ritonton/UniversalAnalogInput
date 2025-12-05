using UniversalAnalogInputUI.Enums;

namespace UniversalAnalogInputUI.Services.Interfaces
{
    /// <summary>Provides toast notification display helpers.</summary>
    public interface IToastService
    {
        /// <summary>Shows a toast message with optional severity and duration.</summary>
        void ShowToast(string title, string message, ToastType type = ToastType.Info, int durationMs = 4000);
        /// <summary>Hides the currently displayed toast if any.</summary>
        void HideToast();
    }
}
