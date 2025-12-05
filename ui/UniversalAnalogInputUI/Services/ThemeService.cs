using System;
using System.IO;
using Microsoft.UI.Xaml;

namespace UniversalAnalogInputUI.Services
{
    /// <summary>Persists and applies the application's theme across sessions.</summary>
    public class ThemeService
    {
        private const string SettingsFileName = "theme.txt";
        private readonly Window _window;
        private readonly string _settingsFilePath;
        private ElementTheme _currentTheme = ElementTheme.Default;

        /// <summary>Raised when the theme changes.</summary>
        public event EventHandler<ElementTheme>? ThemeChanged;

        public ThemeService(Window window)
        {
            _window = window;

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appDataPath, "UniversalAnalogInput");
            Directory.CreateDirectory(appFolder); // Ensure folder exists
            _settingsFilePath = Path.Combine(appFolder, SettingsFileName);
        }

        /// <summary>Returns the resolved theme currently applied to the window.</summary>
        public ElementTheme GetResolvedTheme()
        {
            return _currentTheme;
        }

        /// <summary>Loads the persisted theme preference or defaults to the system theme.</summary>
        public ElementTheme GetCurrentTheme()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var themeString = File.ReadAllText(_settingsFilePath).Trim();
                    if (Enum.TryParse<ElementTheme>(themeString, out var theme))
                    {
                        return theme;
                    }
                }
            }
            catch
            {
                // If any error reading settings, fall back to default
            }

            // Default to system theme
            return ElementTheme.Default;
        }

        /// <summary>Saves and applies a new theme selection.</summary>
        public void SetTheme(ElementTheme theme)
        {
            _currentTheme = theme;

            try
            {
                File.WriteAllText(_settingsFilePath, theme.ToString());
            }
            catch
            {
                // Silently fail if can't save settings
            }

            // Apply to window content
            if (_window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = theme;
            }

            // Notify listeners that theme has changed
            ThemeChanged?.Invoke(this, theme);
        }

        /// <summary>Loads the saved theme and applies it to the window.</summary>
        public void Initialize()
        {
            var savedTheme = GetCurrentTheme();
            _currentTheme = savedTheme;

            if (_window.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = savedTheme;
            }
        }

        /// <summary>Applies the current theme to a specific element.</summary>
        public void ApplyThemeToElement(FrameworkElement element)
        {
            if (element != null)
            {
                element.RequestedTheme = _currentTheme;
            }
        }
    }
}
