using Microsoft.UI.Dispatching;
using UniversalAnalogInputUI.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UniversalAnalogInputUI.Services
{
    public class StatusMonitorService : IStatusMonitorService
    {
        private readonly DispatcherQueue? _dispatcherQueue;
        private readonly List<string> _liveInputHistory;
        private readonly object _lock = new();
        private const int MaxHistoryEntries = 100;

        private string _currentStatusText = "System not initialized";
        private string _currentStatusBackgroundColor = "#FFDC2626";
        private string _currentStatusIconGlyph = "&#xE783;";
        private string _currentStatusIconColor = "#FFDC2626";
        private int _currentMappingCount = 0;
        private bool _isEnabled = false;

        public string CurrentStatusText
        {
            get { lock (_lock) return _currentStatusText; }
            private set { lock (_lock) _currentStatusText = value; }
        }

        public string CurrentStatusBackgroundColor
        {
            get { lock (_lock) return _currentStatusBackgroundColor; }
            private set { lock (_lock) _currentStatusBackgroundColor = value; }
        }

        public string CurrentStatusIconGlyph
        {
            get { lock (_lock) return _currentStatusIconGlyph; }
            private set { lock (_lock) _currentStatusIconGlyph = value; }
        }

        public string CurrentStatusIconColor
        {
            get { lock (_lock) return _currentStatusIconColor; }
            private set { lock (_lock) _currentStatusIconColor = value; }
        }

        public int CurrentMappingCount
        {
            get { lock (_lock) return _currentMappingCount; }
            private set { lock (_lock) _currentMappingCount = value; }
        }

        public bool IsEnabled
        {
            get { lock (_lock) return _isEnabled; }
            set { lock (_lock) _isEnabled = value; }
        }

        public IReadOnlyList<string> LiveInputHistory
        {
            get
            {
                lock (_lock)
                    return _liveInputHistory.ToList().AsReadOnly();
            }
        }

        public event EventHandler<string>? LiveInputAppended;
        public event EventHandler<int>? MappingCountUpdated;
        public event EventHandler<StatusUpdateEventArgs>? StatusUpdated;

        public StatusMonitorService()
        {
            try
            {
                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }
            catch
            {
                _dispatcherQueue = null;
            }

            _liveInputHistory = new List<string>();
        }

        public void AppendLiveInput(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            if (!IsEnabled)
                return;

            lock (_lock)
            {
                _liveInputHistory.Add(message);

                if (_liveInputHistory.Count > MaxHistoryEntries)
                {
                    _liveInputHistory.RemoveRange(0, _liveInputHistory.Count - MaxHistoryEntries);
                }
            }

            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() => LiveInputAppended?.Invoke(this, message));
            }
            else
            {
                LiveInputAppended?.Invoke(this, message);
            }
        }

        public void UpdateMappingCount(int count)
        {
            if (!IsEnabled)
                return;

            CurrentMappingCount = count;

            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() => MappingCountUpdated?.Invoke(this, count));
            }
            else
            {
                MappingCountUpdated?.Invoke(this, count);
            }
        }

        public void UpdateStatus(string text, string backgroundColor, string iconGlyph, string iconColor)
        {
            if (!IsEnabled)
                return;

            CurrentStatusText = text;
            CurrentStatusBackgroundColor = backgroundColor;
            CurrentStatusIconGlyph = iconGlyph;
            CurrentStatusIconColor = iconColor;

            var eventArgs = new StatusUpdateEventArgs
            {
                Text = text,
                BackgroundColor = backgroundColor,
                IconGlyph = iconGlyph,
                IconColor = iconColor
            };

            if (_dispatcherQueue != null)
            {
                _dispatcherQueue.TryEnqueue(() => StatusUpdated?.Invoke(this, eventArgs));
            }
            else
            {
                StatusUpdated?.Invoke(this, eventArgs);
            }
        }
    }
}