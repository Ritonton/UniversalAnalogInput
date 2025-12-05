using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using UniversalAnalogInputUI.Services;
using UniversalAnalogInputUI.Services.Interfaces;
using System;
using System.IO;
using Windows.ApplicationModel.DataTransfer;

namespace UniversalAnalogInputUI.Views
{
    /// <summary>Hosts theme selection and app-level settings.</summary>
    public sealed partial class SettingsPage : Page
    {
        private const string ShowStatusTabSettingKey = "ShowStatusTab";
        private readonly ThemeService _themeService;
        private readonly string _settingsFilePath;
        private bool _isInitializing = true;

        public SettingsPage()
        {
            this.InitializeComponent();
            _themeService = MainWindow.Instance?.ThemeServiceInstance ?? throw new InvalidOperationException("ThemeService not available");

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "UniversalAnalogInput");
            Directory.CreateDirectory(appFolder);
            _settingsFilePath = Path.Combine(appFolder, "settings.txt");

            LoadCurrentTheme();
            LoadShowStatusTabSetting();
            LoadVersion();

            _isInitializing = false;
        }

        private void LoadVersion()
        {
            try
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                {
                    VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
                }
            }
            catch
            {
                VersionText.Text = "Version Failed to load";
            }
        }

        private void LoadCurrentTheme()
        {
            var currentTheme = _themeService.GetCurrentTheme();

            switch (currentTheme)
            {
                case ElementTheme.Default:
                    ThemeComboBox.SelectedIndex = 0; // System
                    break;
                case ElementTheme.Light:
                    ThemeComboBox.SelectedIndex = 1; // Light
                    break;
                case ElementTheme.Dark:
                    ThemeComboBox.SelectedIndex = 2; // Dark
                    break;
            }
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var themeTag = selectedItem.Tag as string;
                ElementTheme newTheme = themeTag switch
                {
                    "System" => ElementTheme.Default,
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };

                _themeService.SetTheme(newTheme);
            }
        }

        private void LoadShowStatusTabSetting()
        {
            bool showStatusTab = GetShowStatusTabSetting();
            ShowStatusTabToggle.IsOn = showStatusTab;
        }

        private void ShowStatusTabToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing)
                return;

            bool isOn = ShowStatusTabToggle.IsOn;
            SaveShowStatusTabSetting(isOn);

            if (!isOn)
            {
                try
                {
                    var statusMonitorService = App.Services.GetService(typeof(IStatusMonitorService)) as IStatusMonitorService;
                    if (statusMonitorService != null)
                    {
                        statusMonitorService.IsEnabled = false;
                    }
                }
                catch { }

                TabNavigationControlUpdated?.Invoke(this, false);
            }

            try
            {
                var toastService = App.Services.GetService(typeof(IToastService)) as IToastService;
                toastService?.ShowToast(
                    "Restart Required",
                    "Please restart the window to apply this change.",
                    Enums.ToastType.Info,
                    5000
                );
            }
            catch { }
        }

        public static event EventHandler<bool>? TabNavigationControlUpdated;

        private bool GetShowStatusTabSetting()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var lines = File.ReadAllLines(_settingsFilePath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith(ShowStatusTabSettingKey + "="))
                        {
                            var value = line.Substring((ShowStatusTabSettingKey + "=").Length);
                            return bool.TryParse(value, out var result) && result;
                        }
                    }
                }
            }
            catch { }

            return false; // Default: hide status tab
        }

        private void SaveShowStatusTabSetting(bool value)
        {
            try
            {
                var lines = new System.Collections.Generic.List<string>();
                if (File.Exists(_settingsFilePath))
                {
                    lines.AddRange(File.ReadAllLines(_settingsFilePath));
                }

                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(ShowStatusTabSettingKey + "="))
                    {
                        lines[i] = ShowStatusTabSettingKey + "=" + value.ToString();
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    lines.Add(ShowStatusTabSettingKey + "=" + value.ToString());
                }

                File.WriteAllLines(_settingsFilePath, lines);
            }
            catch { }
        }

        public static bool GetShowStatusTabSettingStatic()
        {
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var settingsFilePath = Path.Combine(appDataPath, "UniversalAnalogInput", "settings.txt");

                if (File.Exists(settingsFilePath))
                {
                    var lines = File.ReadAllLines(settingsFilePath);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith(ShowStatusTabSettingKey + "="))
                        {
                            var value = line.Substring((ShowStatusTabSettingKey + "=").Length);
                            return bool.TryParse(value, out var result) && result;
                        }
                    }
                }
            }
            catch { }

            return false; // Default: hide
        }

        private void GitCloneCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(GitCloneTextBlock.Text);
                Clipboard.SetContent(dataPackage);

                var toastService = App.Services.GetService(typeof(IToastService)) as IToastService;
                toastService?.ShowToast(
                    "Copied",
                    "Git clone command copied to clipboard",
                    Enums.ToastType.Success,
                    3000
                );
            }
            catch { }
        }

        private async void ReportIssueCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Ritonton/UniversalAnalogInput/issues"));
            }
            catch { }
        }
    }
}
