using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using UniversalAnalogInputUI.Services.Interfaces;
using System;
using System.Linq;
using System.Text;

namespace UniversalAnalogInputUI.Views
{
    /// <summary>Displays live status, mapping count, and input logs.</summary>
    public sealed partial class StatusMonitorPage : Page
    {
        private readonly IStatusMonitorService _statusMonitorService;
        private readonly StringBuilder _liveInputBuilder;

        public StatusMonitorPage()
        {
            this.InitializeComponent();

            _statusMonitorService = (App.Services.GetService(typeof(IStatusMonitorService)) as IStatusMonitorService)!;

            _liveInputBuilder = new StringBuilder();

            _statusMonitorService.LiveInputAppended += OnLiveInputAppended;
            _statusMonitorService.MappingCountUpdated += OnMappingCountUpdated;
            _statusMonitorService.StatusUpdated += OnStatusUpdated;

            InitializeCurrentState();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _statusMonitorService.LiveInputAppended -= OnLiveInputAppended;
            _statusMonitorService.MappingCountUpdated -= OnMappingCountUpdated;
            _statusMonitorService.StatusUpdated -= OnStatusUpdated;

            base.OnNavigatedFrom(e);
        }

        private void InitializeCurrentState()
        {
            UpdateStatus(_statusMonitorService.CurrentStatusText,
                        _statusMonitorService.CurrentStatusBackgroundColor,
                        _statusMonitorService.CurrentStatusIconGlyph,
                        _statusMonitorService.CurrentStatusIconColor);

            MappingCountText.Text = $"{_statusMonitorService.CurrentMappingCount} mappings configured";

            if (_statusMonitorService.LiveInputHistory.Any())
            {
                var historyText = string.Join("\n", _statusMonitorService.LiveInputHistory);
                _liveInputBuilder.Append(historyText);
                LiveInputText.Text = historyText;
                LiveInputScrollViewer.ChangeView(null, LiveInputScrollViewer.ScrollableHeight, null);
            }
        }

        private void OnLiveInputAppended(object? sender, string message)
        {
            AppendLiveInput(message);
        }

        private void OnMappingCountUpdated(object? sender, int count)
        {
            UpdateMappingCount(count);
        }

        private void OnStatusUpdated(object? sender, Services.Interfaces.StatusUpdateEventArgs e)
        {
            UpdateStatus(e.Text, e.BackgroundColor, e.IconGlyph, e.IconColor);
        }

        public void AppendLiveInput(string message)
        {
            if (_liveInputBuilder.Length == 0 || LiveInputText.Text == "Loading...")
            {
                _liveInputBuilder.Clear();
                _liveInputBuilder.Append(message);
            }
            else
            {
                _liveInputBuilder.AppendLine();
                _liveInputBuilder.Append(message);
            }

            LiveInputText.Text = _liveInputBuilder.ToString();

            LiveInputScrollViewer.ChangeView(null, LiveInputScrollViewer.ScrollableHeight, null);
        }

        public void UpdateMappingCount(int count)
        {
            MappingCountText.Text = $"{count} mappings configured";
        }

        public void UpdateStatus(string text, string backgroundColor, string iconGlyph, string iconColor)
        {
            StatusText.Text = text;
            StatusIcon.Glyph = iconGlyph;

            if (!string.IsNullOrEmpty(backgroundColor) && backgroundColor.StartsWith("#") && backgroundColor.Length == 9)
            {
                try
                {
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(
                        Convert.ToByte(backgroundColor.Substring(1, 2), 16),
                        Convert.ToByte(backgroundColor.Substring(3, 2), 16),
                        Convert.ToByte(backgroundColor.Substring(5, 2), 16),
                        Convert.ToByte(backgroundColor.Substring(7, 2), 16)
                    ));
                }
                catch (FormatException)
                {
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 38, 38));
                }
            }

            if (!string.IsNullOrEmpty(iconColor) && iconColor.StartsWith("#") && iconColor.Length == 9)
            {
                try
                {
                    StatusIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(
                        Convert.ToByte(iconColor.Substring(1, 2), 16),
                        Convert.ToByte(iconColor.Substring(3, 2), 16),
                        Convert.ToByte(iconColor.Substring(5, 2), 16),
                        Convert.ToByte(iconColor.Substring(7, 2), 16)
                    ));
                }
                catch (FormatException)
                {
                    StatusIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 38, 38));
                }
            }
        }
    }
}
