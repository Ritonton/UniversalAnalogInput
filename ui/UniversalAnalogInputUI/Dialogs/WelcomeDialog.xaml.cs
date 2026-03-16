using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;

namespace UniversalAnalogInputUI.Dialogs;

/// <summary>Welcome screen shown on the very first application launch.</summary>
public sealed partial class WelcomeDialog : ContentDialog
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "UniversalAnalogInput",
        "settings.txt");

    public WelcomeDialog()
    {
        this.InitializeComponent();

        try
        {
            var themeService = MainWindow.Instance?.ThemeServiceInstance;
            if (themeService != null)
                this.RequestedTheme = themeService.GetResolvedTheme();
            else if (App.MainWindow?.Content is FrameworkElement root)
                this.RequestedTheme = root.ActualTheme;
        }
        catch { }

        LoadHeroImage();

        this.PrimaryButtonClick += (_, _) => MarkAsShown();
    }

    private void LoadHeroImage()
    {
        bool isDark = this.RequestedTheme == ElementTheme.Dark
            || (this.RequestedTheme == ElementTheme.Default
                && Application.Current.RequestedTheme == ApplicationTheme.Dark);

        var uri = isDark
            ? new Uri("ms-appx:///Assets/welcome_hero_dark.png")
            : new Uri("ms-appx:///Assets/welcome_hero_light.png");

        HeroImage.Source = new BitmapImage(uri);
    }

    /// <summary>Returns true if the welcome dialog has not yet been shown.</summary>
    public static bool ShouldShow()
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return true;

            foreach (var line in File.ReadAllLines(SettingsFilePath))
            {
                if (line.Trim().Equals("HasShownWelcome=true", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void MarkAsShown()
    {
        try
        {
            var folder = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(folder);

            var lines = File.Exists(SettingsFilePath)
                ? new List<string>(File.ReadAllLines(SettingsFilePath))
                : new List<string>();

            lines.RemoveAll(l => l.StartsWith("HasShownWelcome=", StringComparison.OrdinalIgnoreCase));
            lines.Add("HasShownWelcome=true");

            File.WriteAllLines(SettingsFilePath, lines);
        }
        catch { }
    }
}
