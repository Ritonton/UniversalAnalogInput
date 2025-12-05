using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using UniversalAnalogInputUI.Services;
using UniversalAnalogInputUI.Services.Interfaces;
using Windows.UI.ViewManagement;

namespace UniversalAnalogInputUI.Helpers;

/// <summary>Provides theme-aware accent brushes for icons and text.</summary>
public static class ThemeAccentHelper
{
    /// <summary>Applies the accent foreground brush to a font icon.</summary>
    public static void ApplyAccentForeground(FontIcon? icon, string contextTag)
    {
        if (icon == null)
        {
            return;
        }

        icon.Foreground = GetAccentTextBrush(icon, contextTag);
    }

    public static SolidColorBrush GetAccentTextBrush(FrameworkElement element, string contextTag)
    {
        var resolvedTheme = ResolveTheme(element);
        var themeKey = resolvedTheme == ElementTheme.Dark ? "Dark" : "Light";

        var brush = GetThemeBrush(themeKey);
        return brush;
    }

    private static SolidColorBrush GetThemeBrush(string themeKey)
    {
        var uiSettings = new Windows.UI.ViewManagement.UISettings();
        var accentColor = themeKey == "Dark"
            ? uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentLight2)
            : uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.AccentDark1);
        return new SolidColorBrush(accentColor);
    }

    /// <summary>Returns the theme-aware accent brush for an element.</summary>
    public static SolidColorBrush GetAccentBrush(FrameworkElement element)
    {
        return GetAccentTextBrush(element, "GetAccentBrush");
    }

    private static ElementTheme ResolveTheme(FrameworkElement element)
    {
        if (element.ActualTheme != ElementTheme.Default)
        {
            return element.ActualTheme;
        }

        if (element.XamlRoot?.Content is FrameworkElement root && root.ActualTheme != ElementTheme.Default)
        {
            return root.ActualTheme;
        }

        var appTheme = Application.Current?.RequestedTheme;
        return appTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
    }
}
