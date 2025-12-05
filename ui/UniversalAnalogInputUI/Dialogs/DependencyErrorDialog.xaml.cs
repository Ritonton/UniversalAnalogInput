using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using Windows.UI;

namespace UniversalAnalogInputUI.Dialogs;

/// <summary>Displays dependency health and offers exit or degraded-mode continuation.</summary>
public sealed partial class DependencyErrorDialog : ContentDialog
{
    public enum DependencyResult
    {
        ContinueAnyway,
        ExitApplication
    }

    public DependencyResult Result { get; private set; } = DependencyResult.ExitApplication;

    private readonly bool _wootingOk;
    private readonly string? _wootingError;
    private readonly bool _vigemOk;
    private readonly string? _vigemError;

    private static readonly SolidColorBrush ErrorRedBrush = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68));

    public DependencyErrorDialog(
        bool wootingOk,
        string? wootingError,
        bool vigemOk,
        string? vigemError)
    {
        this.InitializeComponent();

        _wootingOk = wootingOk;
        _wootingError = wootingError;
        _vigemOk = vigemOk;
        _vigemError = vigemError;

        // Inherit theme from app
        try
        {
            var themeService = MainWindow.Instance?.ThemeServiceInstance;
            if (themeService != null)
            {
                this.RequestedTheme = themeService.GetResolvedTheme();
            }
            else if (App.MainWindow?.Content is FrameworkElement rootElement)
            {
                this.RequestedTheme = rootElement.ActualTheme;
            }
        }
        catch
        {
            // Fallback to default if theme service not available
            if (App.MainWindow?.Content is FrameworkElement rootElement)
            {
                this.RequestedTheme = rootElement.ActualTheme;
            }
        }

        UpdateDependencyStatus();
        ApplyButtonStyles();
        ApplyWarningStyle();

        this.PrimaryButtonClick += (s, e) =>
        {
            Result = DependencyResult.ExitApplication;
        };

        this.SecondaryButtonClick += (s, e) =>
        {
            Result = DependencyResult.ContinueAnyway;
        };
    }

    private void ApplyButtonStyles()
    {
        this.Loaded += (s, e) =>
        {
            if (this.Content is FrameworkElement root)
            {
                var buttons = FindVisualChildren<Button>(root);
                foreach (var button in buttons)
                {
                    button.CornerRadius = new CornerRadius(4);
                }
            }
        };
    }


    private void ApplyWarningStyle()
    {
        bool isLightTheme = this.RequestedTheme == ElementTheme.Light ||
                           (this.RequestedTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Light);

        if (isLightTheme)
        {
            WarningBorder.Background = new SolidColorBrush(Color.FromArgb(255, 229, 231, 235)); // Gray-200
        }
        else
        {
            WarningBorder.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorAttentionBackgroundBrush"];
        }
    }

    private SolidColorBrush GetStatusTextBrush()
    {
        bool isLightTheme = this.RequestedTheme == ElementTheme.Light ||
                           (this.RequestedTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Light);

        if (isLightTheme)
        {
            return new SolidColorBrush(Color.FromArgb(255, 115, 115, 115)); // Gray-500
        }
        else
        {
            return (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        }
    }

    private SolidColorBrush GetErrorTextBrush()
    {
        bool isLightTheme = this.RequestedTheme == ElementTheme.Light ||
                           (this.RequestedTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Light);

        if (isLightTheme)
        {
            return new SolidColorBrush(Color.FromArgb(255, 107, 114, 128)); // Gray-500
        }
        else
        {
            return (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        }
    }

    private SolidColorBrush GetCardBorderBrush()
    {
        bool isLightTheme = this.RequestedTheme == ElementTheme.Light ||
                           (this.RequestedTheme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Light);

        if (isLightTheme)
        {
            return new SolidColorBrush(Color.FromArgb(255, 229, 229, 229)); // Gray-200
        }
        else
        {
            return (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void UpdateDependencyStatus()
    {
        // Wooting SDK Status
        if (_wootingOk)
        {
            WootingStatusIcon.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            WootingStatusIconGlyph.Glyph = "\uE73E";
            WootingStatusText.Text = "Status: Installed and working";
            WootingStatusText.Foreground = GetStatusTextBrush();
            WootingErrorBorder.BorderBrush = GetCardBorderBrush();
        }
        else
        {
            WootingStatusIcon.Background = ErrorRedBrush;
            WootingStatusIconGlyph.Glyph = "\uE711";
            WootingStatusText.Text = "Status: Missing or failed to initialize";
            WootingStatusText.Foreground = GetStatusTextBrush();
            WootingErrorBorder.BorderBrush = GetCardBorderBrush();

            if (!string.IsNullOrEmpty(_wootingError))
            {
                WootingErrorText.Text = $"Error: {_wootingError}";
                WootingErrorText.Foreground = GetErrorTextBrush();
                WootingErrorText.Visibility = Visibility.Visible;
            }
        }

        // ViGEm Bus Driver Status
        if (_vigemOk)
        {
            VigemStatusIcon.Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            VigemStatusIconGlyph.Glyph = "\uE73E";
            VigemStatusText.Text = "Status: Installed and working";
            VigemStatusText.Foreground = GetStatusTextBrush();
            VigemErrorBorder.BorderBrush = GetCardBorderBrush();
        }
        else
        {
            VigemStatusIcon.Background = ErrorRedBrush;
            VigemStatusIconGlyph.Glyph = "\uE711";
            VigemStatusText.Text = "Status: Missing or failed to initialize";
            VigemStatusText.Foreground = GetStatusTextBrush();
            VigemErrorBorder.BorderBrush = GetCardBorderBrush();

            if (!string.IsNullOrEmpty(_vigemError))
            {
                VigemErrorText.Text = $"Error: {_vigemError}";
                VigemErrorText.Foreground = GetErrorTextBrush();
                VigemErrorText.Visibility = Visibility.Visible;
            }
        }

        // If all dependencies are OK, hide secondary button
        if (_wootingOk && _vigemOk)
        {
            this.SecondaryButtonText = "";
            this.Title = "All Dependencies OK";
        }
    }
}
