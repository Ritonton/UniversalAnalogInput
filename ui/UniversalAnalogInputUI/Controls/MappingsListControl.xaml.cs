using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using UniversalAnalogInputUI.Services.Interfaces;
using UniversalAnalogInputUI.Models;
using UniversalAnalogInputUI.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace UniversalAnalogInputUI.Controls;

public sealed partial class MappingsListControl : UserControl
{
    private IMappingManagementService? _mappingService;
    private bool _isInSelectionMode = false;
    private bool _selectionButtonsVisible = false;

    /// <summary>
    /// Raised when user selects/deselects mappings; consumed by CurveConfigurationPage
    /// </summary>
    public event EventHandler<IEnumerable<KeyMapping>>? MappingSelectionChanged;

    /// <summary>
    /// Returns currently selected mappings for external consumers
    /// </summary>
    public IEnumerable<KeyMapping> SelectedMappings =>
        MappingsListView?.SelectedItems?.Cast<KeyMapping>() ?? Enumerable.Empty<KeyMapping>();

    public MappingsListControl()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Connects control to mapping service and subscribes to collection changes for zebra striping
    /// </summary>
    public void Initialize(IMappingManagementService mappingService)
    {
        _mappingService = mappingService;

        MappingsListView.ItemsSource = _mappingService.CurrentMappings;

        _mappingService.CurrentMappings.CollectionChanged += CurrentMappings_CollectionChanged;
    }

    private void AddMappingButton_Click(object sender, RoutedEventArgs e)
    {
        _mappingService?.AddMapping();
    }

    /// <summary>
    /// Validates and syncs mapping to Rust when user changes key selection
    /// </summary>
    private void KeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.DataContext is KeyMapping mapping)
        {
            _mappingService?.ValidateAndSyncMapping(mapping);
        }
    }

    /// <summary>
    /// Validates and syncs mapping to Rust when user changes gamepad control selection
    /// </summary>
    private void GamepadComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.DataContext is KeyMapping mapping)
        {
            _mappingService?.ValidateAndSyncMapping(mapping);
        }
    }

    /// <summary>
    /// Applies zebra striping style when ListView creates or recycles item containers
    /// </summary>
    private void MappingsListView_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
    {
        if (args.ItemContainer == null)
        {
            args.ItemContainer = new ListViewItem();
        }

        if (args.ItemContainer is ListViewItem listViewItem)
        {
            ApplyZebraBrushes(listViewItem, args.ItemIndex);
        }
    }

    /// <summary>
    /// Reapplies zebra striping when collection changes to maintain correct pattern
    /// </summary>
    private void CurrentMappings_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ReapplyZebraPattern();
    }

    /// <summary>
    /// Updates zebra striping for all realized containers; virtualized items update on realization
    /// </summary>
    private void ReapplyZebraPattern()
    {
        if (MappingsListView?.Items == null) return;

        int count = MappingsListView.Items.Count;
        for (int i = 0; i < count; i++)
        {
            if (MappingsListView.ContainerFromIndex(i) is ListViewItem item)
            {
                ApplyZebraBrushes(item, i);
            }
        }
    }

    /// <summary>
    /// Assigns even/odd row style based on item index for alternating background colors
    /// </summary>
    private void ApplyZebraBrushes(ListViewItem item, int index)
    {
        var styleKey = (index % 2 == 0) ? "EvenRowStyle" : "OddRowStyle";
        var style = (Style)MappingsListView.Resources[styleKey];

        item.Style = style;
    }

    /// <summary>
    /// Updates UI mode when user selects/deselects mappings; notifies listeners and refreshes buttons
    /// </summary>
    private void MappingsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _isInSelectionMode = MappingsListView.SelectedItems.Count > 0;
        UpdateSelectionButtons();
        RefreshAllActionButtons();

        var selectedMappings = MappingsListView.SelectedItems.Cast<KeyMapping>();
        MappingSelectionChanged?.Invoke(this, selectedMappings);
    }

    /// <summary>
    /// Shows/hides selection toolbar with animation; updates SelectAll button state
    /// </summary>
    private async void UpdateSelectionButtons()
    {
        bool hasSelection = MappingsListView.SelectedItems.Count > 0;

        if (hasSelection && !_selectionButtonsVisible)
        {
            ResetSelectAllButtonToDefaultState();
            _selectionButtonsVisible = true;
            await AnimateContainerAppearance(true);
        }
        else if (!hasSelection && _selectionButtonsVisible)
        {
            _selectionButtonsVisible = false;
            await AnimateContainerAppearance(false);
        }

        if (_selectionButtonsVisible && _mappingService != null)
        {
            bool allSelected = MappingsListView.SelectedItems.Count == _mappingService.CurrentMappings.Count && _mappingService.CurrentMappings.Count > 0;

            var stackPanel = SelectAllButton.Content as StackPanel;
            if (stackPanel != null)
            {
                var fontIcon = stackPanel.Children.OfType<FontIcon>().FirstOrDefault();
                var textBlock = stackPanel.Children.OfType<TextBlock>().FirstOrDefault();

                if (fontIcon != null && textBlock != null)
                {
                    if (allSelected)
                    {
                        fontIcon.Glyph = "\uE8B2";
                        textBlock.Text = "Deselect All";
                    }
                    else
                    {
                        fontIcon.Glyph = "\uE8B3";
                        textBlock.Text = "Select All";
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resets SelectAll button to default state before showing selection toolbar
    /// </summary>
    private void ResetSelectAllButtonToDefaultState()
    {
        var stackPanel = SelectAllButton.Content as StackPanel;
        if (stackPanel != null)
        {
            var fontIcon = stackPanel.Children.OfType<FontIcon>().FirstOrDefault();
            var textBlock = stackPanel.Children.OfType<TextBlock>().FirstOrDefault();

            if (fontIcon != null && textBlock != null)
            {
                fontIcon.Glyph = "\uE8B3";
                textBlock.Text = "Select All";
            }
        }
    }

    /// <summary>
    /// Animates selection toolbar appearance with fade and scale effects
    /// </summary>
    private async Task AnimateContainerAppearance(bool show)
    {
        var container = SelectionButtonsContainer;

        if (show)
        {
            container.Visibility = Visibility.Visible;

            var opacityAnimation = new DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var scaleXAnimation = new DoubleAnimation
            {
                From = 0.9,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 }
            };

            var scaleYAnimation = new DoubleAnimation
            {
                From = 0.9,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.2 }
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(opacityAnimation);
            storyboard.Children.Add(scaleXAnimation);
            storyboard.Children.Add(scaleYAnimation);

            Storyboard.SetTarget(opacityAnimation, container);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

            var transform = container.RenderTransform as CompositeTransform;
            if (transform != null)
            {
                Storyboard.SetTarget(scaleXAnimation, transform);
                Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");
                Storyboard.SetTarget(scaleYAnimation, transform);
                Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");
            }

            storyboard.Begin();
            await Task.Delay(250);
        }
        else
        {
            var opacityAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var scaleXAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var scaleYAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var storyboard = new Storyboard();
            storyboard.Children.Add(opacityAnimation);
            storyboard.Children.Add(scaleXAnimation);
            storyboard.Children.Add(scaleYAnimation);

            Storyboard.SetTarget(opacityAnimation, container);
            Storyboard.SetTargetProperty(opacityAnimation, "Opacity");

            var transform = container.RenderTransform as CompositeTransform;
            if (transform != null)
            {
                Storyboard.SetTarget(scaleXAnimation, transform);
                Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");
                Storyboard.SetTarget(scaleYAnimation, transform);
                Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");
            }

            storyboard.Begin();
            await Task.Delay(180);
            container.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Binds ComboBox data sources and initializes action button when row finishes loading
    /// </summary>
    private void ItemGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border border && border.Child is Grid grid && _mappingService != null)
        {
            var keyComboBox = VisualTreeHelpers.FindChildOfType<ComboBox>(grid, "KeyComboBox");
            var gamepadComboBox = VisualTreeHelpers.FindChildOfType<ComboBox>(grid, "GamepadComboBox");

            if (keyComboBox != null)
            {
                keyComboBox.ItemsSource = _mappingService.AvailableKeys;
            }

            if (gamepadComboBox != null)
            {
                gamepadComboBox.ItemsSource = _mappingService.GamepadControls;
            }

            var listViewItem = VisualTreeHelpers.FindParentOfType<ListViewItem>(border);
            if (listViewItem != null)
            {
                var index = MappingsListView.IndexFromContainer(listViewItem);
                UpdateActionButtonInGrid(grid, index);
            }
        }
    }

    /// <summary>
    /// Refreshes all visible action buttons when selection mode changes
    /// </summary>
    private void RefreshAllActionButtons()
    {
        for (int i = 0; i < MappingsListView.Items.Count; i++)
        {
            if (MappingsListView.ContainerFromIndex(i) is ListViewItem listViewItem)
            {
                var grid = VisualTreeHelpers.FindChildOfType<Grid>(listViewItem, "ItemGrid");
                if (grid != null)
                {
                    UpdateActionButtonInGrid(grid, i);
                }
            }
        }
    }

    /// <summary>
    /// Switches action button between delete icon (normal) and checkmark/empty (selection mode)
    /// </summary>
    private async void UpdateActionButtonInGrid(Grid grid, int itemIndex)
    {
        if (_mappingService == null) return;

        var button = VisualTreeHelpers.FindChildOfType<Button>(grid, "ActionButton");
        if (button == null) return;

        var icon = VisualTreeHelpers.FindChildOfType<FontIcon>(button, "ActionIcon");
        if (icon == null) return;

        bool isSelected = false;
        if (itemIndex >= 0 && itemIndex < _mappingService.CurrentMappings.Count)
        {
            var mapping = _mappingService.CurrentMappings[itemIndex];
            isSelected = MappingsListView.SelectedItems.Contains(mapping);
        }

        if (_isInSelectionMode)
        {
            await AnimateIconChange(icon, isSelected ? "\uE8FB" : "");

            if (isSelected)
            {
                button.IsEnabled = true;
                button.IsHitTestVisible = false;
            }
            else
            {
                button.IsEnabled = false;
            }
        }
        else
        {
            await AnimateIconChange(icon, "\uE74D");
            button.IsEnabled = true;
            button.IsHitTestVisible = true;
        }
    }

    /// <summary>
    /// Fades icon out, changes glyph, then fades back in for smooth transitions
    /// </summary>
    private async Task AnimateIconChange(FontIcon icon, string newGlyph)
    {
        if (icon.Glyph == newGlyph) return;

        var fadeOut = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        var storyboardOut = new Storyboard();
        storyboardOut.Children.Add(fadeOut);
        Storyboard.SetTarget(fadeOut, icon);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");

        storyboardOut.Begin();
        await Task.Delay(150);

        icon.Glyph = newGlyph;

        var fadeIn = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(150),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var storyboardIn = new Storyboard();
        storyboardIn.Children.Add(fadeIn);
        Storyboard.SetTarget(fadeIn, icon);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        storyboardIn.Begin();
    }

    /// <summary>
    /// Toggles between selecting all mappings and clearing selection
    /// </summary>
    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mappingService == null) return;

        bool allSelected = MappingsListView.SelectedItems.Count == _mappingService.CurrentMappings.Count && _mappingService.CurrentMappings.Count > 0;

        if (allSelected)
        {
            MappingsListView.SelectedItems.Clear();
        }
        else
        {
            MappingsListView.SelectAll();
        }
    }

    /// <summary>
    /// Deletes all selected mappings and syncs to Rust via mapping service
    /// </summary>
    private async void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_mappingService == null) return;

        var selectedMappings = MappingsListView.SelectedItems.Cast<KeyMapping>().ToList();

        if (selectedMappings.Count == 0) return;

        int deletedCount = await _mappingService.RemoveSelectedMappingsAsync(selectedMappings);

        MappingsListView.SelectedItems.Clear();
    }

    /// <summary>
    /// Deletes single mapping row when user clicks delete button
    /// </summary>
    private async void DeleteMappingButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is KeyMapping mapping && _mappingService != null)
        {
            await _mappingService.RemoveMappingAsync(mapping);
        }
    }

    public void ClearSelection()
    {
        MappingsListView.SelectedItems.Clear();
    }
}