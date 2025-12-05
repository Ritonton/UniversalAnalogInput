using System;
using System.Collections.Generic;

namespace UniversalAnalogInputUI.Services.Interfaces
{
    /// <summary>Tracks status text, icons, and live input history for the UI.</summary>
    public interface IStatusMonitorService
    {
        string CurrentStatusText { get; }
        string CurrentStatusBackgroundColor { get; }
        string CurrentStatusIconGlyph { get; }
        string CurrentStatusIconColor { get; }
        int CurrentMappingCount { get; }
        IReadOnlyList<string> LiveInputHistory { get; }

        bool IsEnabled { get; set; }

        event EventHandler<string>? LiveInputAppended;
        event EventHandler<int>? MappingCountUpdated;
        event EventHandler<StatusUpdateEventArgs>? StatusUpdated;

        void AppendLiveInput(string message);
        void UpdateMappingCount(int count);
        void UpdateStatus(string text, string backgroundColor, string iconGlyph, string iconColor);
    }

    /// <summary>Payload for status updates applied to the status bar.</summary>
    public class StatusUpdateEventArgs : EventArgs
    {
        public string Text { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = string.Empty;
        public string IconColor { get; set; } = string.Empty;
    }
}
