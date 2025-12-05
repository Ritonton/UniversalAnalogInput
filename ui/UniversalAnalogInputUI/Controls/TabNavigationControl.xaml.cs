using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;

namespace UniversalAnalogInputUI.Controls;

/// <summary>Custom tab bar that drives navigation between main pages.</summary>
public sealed partial class TabNavigationControl : UserControl
{
    private string _currentTab = "Tester";
    private string? _hoveredTab = null;

    public event EventHandler<string>? PageChanged;

    /// <summary>Navigates to a specific tab programmatically.</summary>
    public void NavigateToTab(string tabTag)
    {
        UpdateTabSelection(tabTag);
        NavigateToPage(tabTag);
    }

    public TabNavigationControl()
    {
        this.InitializeComponent();

        UpdateStatusTabVisibility(Views.SettingsPage.GetShowStatusTabSettingStatic());

        // Subscribe to setting changes
        Views.SettingsPage.TabNavigationControlUpdated += (s, showStatus) =>
        {
            UpdateStatusTabVisibility(showStatus);
        };

        TesterTab.Loaded += (s, e) =>
        {
            NavigateToPage("Tester");
            UpdateTabSelection("Tester");
        };

        TabBarGrid.SizeChanged += (s, e) =>
        {
            if (e.NewSize.Width != e.PreviousSize.Width)
            {
                UpdateTabSelection(_currentTab, animate: false);
            }
        };
    }


    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            if (tag == _currentTab)
            {
                if (_hoveredTab == tag)
                {
                    AnimateIndicatorMargin(6);
                }
                return;
            }

            NavigateToPage(tag);
            UpdateTabSelection(tag, animateMarginAfter: _hoveredTab == tag);
        }
    }

    private void TabButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            _hoveredTab = tag;

            if (tag != _currentTab)
            {
                button.Opacity = 1.0;
            }
            else
            {
                AnimateIndicatorMargin(6);
            }
        }
    }

    private void TabButton_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            _hoveredTab = null;

            if (tag != _currentTab)
            {
                button.Opacity = 0.6;
            }
            else
            {
                AnimateIndicatorMargin(2);
            }
        }
    }

    private void AnimateIndicatorMargin(double targetBottomMargin)
    {
        var storyboard = new Storyboard();

        double currentBottom = SelectionIndicator.Margin.Bottom;

        var marginAnimation = new ObjectAnimationUsingKeyFrames
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(150))
        };

        int steps = 10;
        for (int i = 0; i <= steps; i++)
        {
            double progress = i / (double)steps;
            double easedProgress = 1 - Math.Pow(1 - progress, 3); // EaseOut cubic
            double interpolatedBottom = currentBottom + (targetBottomMargin - currentBottom) * easedProgress;

            var keyFrame = new DiscreteObjectKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150 * progress)),
                Value = new Thickness(0, 0, 0, interpolatedBottom)
            };
            marginAnimation.KeyFrames.Add(keyFrame);
        }

        Storyboard.SetTarget(marginAnimation, SelectionIndicator);
        Storyboard.SetTargetProperty(marginAnimation, "Margin");
        storyboard.Children.Add(marginAnimation);

        storyboard.Begin();
    }

    private void UpdateTabSelection(string selectedTag, bool animate = true, bool animateMarginAfter = false)
    {
        _currentTab = selectedTag;

        Button targetButton = selectedTag switch
        {
            "Tester" => TesterTab,
            "Curves" => CurvesTab,
            "Status" => StatusTab,
            "Settings" => SettingsTab,
            _ => TesterTab
        };

        if (targetButton.ActualWidth == 0)
        {
            targetButton.Loaded += (s, e) => UpdateTabSelection(selectedTag);
            return;
        }

        var transform = targetButton.TransformToVisual(SelectionIndicator.Parent as UIElement);
        var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

        double indicatorWidth = targetButton.ActualWidth * 0.15;
        double centerOffset = (targetButton.ActualWidth - indicatorWidth) / 2;

        double totalOffset = 4;
        position = new Windows.Foundation.Point(position.X - totalOffset, position.Y);

        if (SelectionIndicator.Width == 0 || double.IsNaN(SelectionIndicator.Width) || !animate)
        {
            SelectionIndicator.Width = indicatorWidth;
            IndicatorTransform.X = position.X + centerOffset;
        }
        else
        {
            var storyboard = new Storyboard();

            var xAnimation = new DoubleAnimation
            {
                To = position.X + centerOffset,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(xAnimation, IndicatorTransform);
            Storyboard.SetTargetProperty(xAnimation, "X");
            storyboard.Children.Add(xAnimation);

            var widthAnimation = new DoubleAnimation
            {
                To = indicatorWidth,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(widthAnimation, SelectionIndicator);
            Storyboard.SetTargetProperty(widthAnimation, "Width");
            storyboard.Children.Add(widthAnimation);

            if (animateMarginAfter)
            {
                storyboard.Completed += (s, e) =>
                {
                    if (_hoveredTab == _currentTab)
                    {
                        AnimateIndicatorMargin(6);
                    }
                };
            }

            storyboard.Begin();
        }

        var selectedOpacity = 1.0;
        var unselectedOpacity = 0.6;

        TesterTab.Opacity = selectedTag == "Tester" ? selectedOpacity : unselectedOpacity;
        CurvesTab.Opacity = selectedTag == "Curves" ? selectedOpacity : unselectedOpacity;
        StatusTab.Opacity = selectedTag == "Status" ? selectedOpacity : unselectedOpacity;
        SettingsTab.Opacity = selectedTag == "Settings" ? selectedOpacity : unselectedOpacity;
    }

    private void UpdateStatusTabVisibility(bool show)
    {
        StatusTab.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

        foreach (var child in TabBarGrid.Children)
        {
            if (child is Microsoft.UI.Xaml.Controls.AppBarSeparator separator)
            {
                var column = Microsoft.UI.Xaml.Controls.Grid.GetColumn(separator);
                if (column == 5) // Separator before Status tab
                {
                    separator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                    break;
                }
            }
        }

        if (show)
        {
            if (TabBarGrid.ColumnDefinitions.Count == 5)
            {
                TabBarGrid.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = Microsoft.UI.Xaml.GridLength.Auto });
                TabBarGrid.ColumnDefinitions.Add(new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Star) });
            }
        }
        else
        {
            while (TabBarGrid.ColumnDefinitions.Count > 5)
            {
                TabBarGrid.ColumnDefinitions.RemoveAt(TabBarGrid.ColumnDefinitions.Count - 1);
            }
        }

        if (!show && _currentTab == "Status")
        {
            NavigateToTab("Tester");
        }

        EventHandler<object>? layoutHandler = null;
        layoutHandler = (s, e) =>
        {
            TabBarGrid.LayoutUpdated -= layoutHandler;
            UpdateTabSelection(_currentTab, animate: false);
        };
        TabBarGrid.LayoutUpdated += layoutHandler;
    }

    private void NavigateToPage(string pageTag)
    {
        Type? pageType = pageTag switch
        {
            "Tester" => typeof(Views.TesterPage),
            "Curves" => typeof(Views.CurveConfigurationPage),
            "Status" => typeof(Views.StatusMonitorPage),
            "Settings" => typeof(Views.SettingsPage),
            _ => null
        };

        if (pageType != null)
        {
            string previousTab = _currentTab;

            var transitionInfo = GetTransitionInfo(previousTab, pageTag);

            _currentTab = pageTag;

            var navOptions = new FrameNavigationOptions
            {
                TransitionInfoOverride = transitionInfo,
                IsNavigationStackEnabled = false
            };
            ContentFrame.NavigateToType(pageType, null, navOptions);

            PageChanged?.Invoke(this, pageTag);
        }
    }


    private NavigationTransitionInfo GetTransitionInfo(string fromTag, string toTag)
    {
        int fromIndex = GetTabIndex(fromTag);
        int toIndex = GetTabIndex(toTag);

        if (toIndex > fromIndex)
        {
            return new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight };
        }
        else if (toIndex < fromIndex)
        {
            return new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromLeft };
        }
        else
        {
            return new SuppressNavigationTransitionInfo();
        }
    }

    private static int GetTabIndex(string tag)
    {
        return tag switch
        {
            "Tester" => 0,
            "Curves" => 1,
            "Status" => 2,
            _ => -1
        };
    }
}
