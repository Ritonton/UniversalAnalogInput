using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using UniversalAnalogInputUI.Helpers;

namespace UniversalAnalogInputUI.Controls;

/// <summary>Dual-handle slider for configuring inner/outer dead zones.</summary>
public sealed partial class DeadZoneRangeSlider : UserControl
{
    private const double HandleContainerSize = 32.0;
    private const double HandleContainerHalf = HandleContainerSize / 2.0;
    private const double TrackThickness = 4.0;
    private const double NormalScale = 0.7;
    private const double HoverScale = 0.4;
    private const double ActiveScale = 1.0;
    private static readonly TimeSpan ScaleAnimationDuration = TimeSpan.FromMilliseconds(120);

    private enum ActiveHandle
    {
        None,
        Lower,
        Upper
    }

    private ActiveHandle _activeHandle = ActiveHandle.None;
    private bool _isPointerCaptured = false;
    private bool _isUserInteraction = false;
    private bool _isLowerHovered = false;
    private bool _isUpperHovered = false;
    private bool _isUpdatingValues = false;
    private SolidColorBrush? _accentBrush;
    private SolidColorBrush? _accentSecondaryBrush;

    public event EventHandler<RangeValueChangedEventArgs>? RangeValueChanged;

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(double),
            typeof(DeadZoneRangeSlider),
            new PropertyMetadata(0.0, OnMinMaxChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(DeadZoneRangeSlider),
            new PropertyMetadata(1.0, OnMinMaxChanged));

    public static readonly DependencyProperty MinimumSeparationProperty =
        DependencyProperty.Register(
            nameof(MinimumSeparation),
            typeof(double),
            typeof(DeadZoneRangeSlider),
            new PropertyMetadata(0.02, OnMinimumSeparationChanged));

    public static readonly DependencyProperty LowerValueProperty =
        DependencyProperty.Register(
            nameof(LowerValue),
            typeof(double),
            typeof(DeadZoneRangeSlider),
            new PropertyMetadata(0.05, OnLowerValueChanged));

    public static readonly DependencyProperty UpperValueProperty =
        DependencyProperty.Register(
            nameof(UpperValue),
            typeof(double),
            typeof(DeadZoneRangeSlider),
            new PropertyMetadata(0.95, OnUpperValueChanged));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double MinimumSeparation
    {
        get => (double)GetValue(MinimumSeparationProperty);
        set => SetValue(MinimumSeparationProperty, value);
    }

    public double LowerValue
    {
        get => (double)GetValue(LowerValueProperty);
        set => SetValue(LowerValueProperty, value);
    }

    public double UpperValue
    {
        get => (double)GetValue(UpperValueProperty);
        set => SetValue(UpperValueProperty, value);
    }

    public DeadZoneRangeSlider()
    {
        InitializeComponent();
        SizeChanged += DeadZoneRangeSlider_SizeChanged;
        ActualThemeChanged += DeadZoneRangeSlider_ActualThemeChanged;
        ResolveAccentBrush();
    }

    private void DeadZoneRangeSlider_ActualThemeChanged(FrameworkElement sender, object args)
    {
        ResolveAccentBrush();
    }

    private void DeadZoneRangeSlider_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private void LayoutCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisualState();
    }

    private static void OnMinMaxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DeadZoneRangeSlider)d;
        control.EnforceValueConstraints(true);
    }

    private static void OnMinimumSeparationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DeadZoneRangeSlider)d;
        control.EnforceValueConstraints(true);
    }

    private static void OnLowerValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DeadZoneRangeSlider)d;
        control.HandleLowerValueChanged((double)e.NewValue);
    }

    private static void OnUpperValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (DeadZoneRangeSlider)d;
        control.HandleUpperValueChanged((double)e.NewValue);
    }

    private void RaiseRangeChanged(bool isUserInteraction)
    {
        RangeValueChanged?.Invoke(this, new RangeValueChangedEventArgs(LowerValue, UpperValue, isUserInteraction));
    }

    private void HandleLowerValueChanged(double newValue)
    {
        if (_isUpdatingValues)
        {
            return;
        }

        double clamped = ClampLowerValue(newValue);
        if (!AreClose(clamped, newValue))
        {
            _isUpdatingValues = true;
            SetValue(LowerValueProperty, clamped);
            _isUpdatingValues = false;
            UpdateVisualState();
            RaiseRangeChanged(_isUserInteraction);
            return;
        }

        UpdateVisualState();
        RaiseRangeChanged(_isUserInteraction);
    }

    private void HandleUpperValueChanged(double newValue)
    {
        if (_isUpdatingValues)
        {
            return;
        }

        double clamped = ClampUpperValue(newValue);
        if (!AreClose(clamped, newValue))
        {
            _isUpdatingValues = true;
            SetValue(UpperValueProperty, clamped);
            _isUpdatingValues = false;
            UpdateVisualState();
            RaiseRangeChanged(_isUserInteraction);
            return;
        }

        UpdateVisualState();
        RaiseRangeChanged(_isUserInteraction);
    }

    private void EnforceValueConstraints(bool raiseEvent)
    {
        double originalLower = LowerValue;
        double originalUpper = UpperValue;

        double clampedLower = ClampLowerValue(originalLower);
        double clampedUpper = ClampUpperValue(originalUpper);

        bool changed = !AreClose(originalLower, clampedLower) || !AreClose(originalUpper, clampedUpper);

        _isUpdatingValues = true;
        if (!AreClose(originalLower, clampedLower))
        {
            SetValue(LowerValueProperty, clampedLower);
        }
        if (!AreClose(originalUpper, clampedUpper))
        {
            SetValue(UpperValueProperty, clampedUpper);
        }
        _isUpdatingValues = false;

        UpdateVisualState();

        if (raiseEvent && changed)
        {
            RaiseRangeChanged(false);
        }
    }

    private double ClampLowerValue(double value)
    {
        double min = Minimum;
        double maxAllowed = UpperValue - MinimumSeparation;
        double max = Math.Max(min, Math.Min(maxAllowed, Maximum));
        return Math.Clamp(value, min, max);
    }

    private double ClampUpperValue(double value)
    {
        double max = Maximum;
        double minAllowed = LowerValue + MinimumSeparation;
        double min = Math.Min(max, Math.Max(Minimum, minAllowed));
        if (min > max)
        {
            min = max;
        }
        return Math.Clamp(value, min, max);
    }

    private static bool AreClose(double a, double b) => Math.Abs(a - b) <= 1e-6;

    private void LayoutCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(LayoutCanvas).Position;
        ActivateNearestHandle(position.X);
        BeginInteraction(e.Pointer, position);
        e.Handled = true;
    }

    private void LowerHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ActivateHandle(ActiveHandle.Lower);
        BeginInteraction(e.Pointer, e.GetCurrentPoint(LayoutCanvas).Position);
        e.Handled = true;
    }

    private void UpperHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ActivateHandle(ActiveHandle.Upper);
        BeginInteraction(e.Pointer, e.GetCurrentPoint(LayoutCanvas).Position);
        e.Handled = true;
    }

    private void LayoutCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(LayoutCanvas).Position;

        if (_activeHandle == ActiveHandle.None)
        {
            RefreshHoverState(position);
        }

        if (_activeHandle == ActiveHandle.None || !_isPointerCaptured)
        {
            return;
        }

        UpdateValueFromPosition(position.X);
        e.Handled = true;
    }

    private void LayoutCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(LayoutCanvas).Position;
        EndInteraction(e.Pointer, point);
        e.Handled = true;
    }

    private void LayoutCanvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(LayoutCanvas).Position;
        EndInteraction(e.Pointer, point);
        e.Handled = true;
    }

    private void LayoutCanvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(LayoutCanvas).Position;
        EndInteraction(e.Pointer, point);
    }

    private void LayoutCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_activeHandle == ActiveHandle.None)
        {
            SetLowerHovered(false);
            SetUpperHovered(false);
        }
    }

    private void LowerHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        SetLowerHovered(true);
    }

    private void LowerHandle_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        SetLowerHovered(false);
    }

    private void UpperHandle_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        SetUpperHovered(true);
    }

    private void UpperHandle_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        SetUpperHovered(false);
    }

    private void SetLowerHovered(bool hovered)
    {
        if (_isLowerHovered == hovered)
        {
            return;
        }

        _isLowerHovered = hovered;

        if (_activeHandle != ActiveHandle.Lower)
        {
            AnimateHandle(LowerHandleScale, hovered ? HoverScale : NormalScale);
        }
    }

    private void SetUpperHovered(bool hovered)
    {
        if (_isUpperHovered == hovered)
        {
            return;
        }

        _isUpperHovered = hovered;

        if (_activeHandle != ActiveHandle.Upper)
        {
            AnimateHandle(UpperHandleScale, hovered ? HoverScale : NormalScale);
        }
    }

    private void RefreshHoverState(Point? pointerPosition)
    {
        if (LayoutCanvas == null)
        {
            return;
        }

        if (!pointerPosition.HasValue || !IsPointWithinCanvas(pointerPosition.Value))
        {
            SetLowerHovered(false);
            SetUpperHovered(false);
            return;
        }

        bool overLower = IsPointerOverHandle(pointerPosition.Value, ActiveHandle.Lower);
        bool overUpper = IsPointerOverHandle(pointerPosition.Value, ActiveHandle.Upper);

        SetLowerHovered(overLower);
        SetUpperHovered(overUpper);
    }

    private bool IsPointWithinCanvas(Point position)
    {
        return position.X >= 0 &&
               position.X <= LayoutCanvas.ActualWidth &&
               position.Y >= 0 &&
               position.Y <= LayoutCanvas.ActualHeight;
    }

    private bool IsPointerOverHandle(Point position, ActiveHandle handle)
    {
        double centerX = handle == ActiveHandle.Lower
            ? GetHandleCenterX(LowerValue)
            : GetHandleCenterX(UpperValue);

        double centerY = GetHandleCenterY();
        double dx = position.X - centerX;
        double dy = position.Y - centerY;
        double distanceSquared = (dx * dx) + (dy * dy);
        double hoverRadius = HandleContainerHalf;

        return distanceSquared <= hoverRadius * hoverRadius;
    }

    private double GetHandleCenterY()
    {
        double trackTop = (LayoutCanvas.ActualHeight - TrackThickness) / 2.0;
        return trackTop + (TrackThickness / 2.0);
    }

    private void ActivateNearestHandle(double positionX)
    {
        double lowerCenter = GetHandleCenterX(LowerValue);
        double upperCenter = GetHandleCenterX(UpperValue);
        double distanceToLower = Math.Abs(positionX - lowerCenter);
        double distanceToUpper = Math.Abs(positionX - upperCenter);
        ActivateHandle(distanceToLower <= distanceToUpper ? ActiveHandle.Lower : ActiveHandle.Upper);
    }

    private void ActivateHandle(ActiveHandle handle)
    {
        _activeHandle = handle;
        _isUserInteraction = true;

        if (handle == ActiveHandle.Lower)
        {
            AnimateHandle(LowerHandleScale, ActiveScale);
            if (_isUpperHovered)
            {
                AnimateHandle(UpperHandleScale, HoverScale);
            }
            else
            {
                AnimateHandle(UpperHandleScale, NormalScale);
            }
        }
        else if (handle == ActiveHandle.Upper)
        {
            AnimateHandle(UpperHandleScale, ActiveScale);
            if (_isLowerHovered)
            {
                AnimateHandle(LowerHandleScale, HoverScale);
            }
            else
            {
                AnimateHandle(LowerHandleScale, NormalScale);
            }
        }
    }

    private void BeginInteraction(Pointer pointer, Point position)
    {
        if (!_isPointerCaptured)
        {
            _isPointerCaptured = LayoutCanvas.CapturePointer(pointer);
        }
        UpdateValueFromPosition(position.X);
    }

    private void EndInteraction(Pointer pointer, Point? position = null)
    {
        if (_isPointerCaptured)
        {
            LayoutCanvas.ReleasePointerCapture(pointer);
            _isPointerCaptured = false;
        }

        if (_activeHandle == ActiveHandle.Lower)
        {
            AnimateHandle(LowerHandleScale, _isLowerHovered ? HoverScale : NormalScale);
        }
        else if (_activeHandle == ActiveHandle.Upper)
        {
            AnimateHandle(UpperHandleScale, _isUpperHovered ? HoverScale : NormalScale);
        }

        _activeHandle = ActiveHandle.None;

        // Send final event with IsUserInteraction=false to signal drag end
        // This triggers cleanup and final update in the consumer (CurveConfigurationPage)
        bool wasUserInteraction = _isUserInteraction;
        _isUserInteraction = false;

        if (wasUserInteraction)
        {
            RaiseRangeChanged(false); // Signal drag end
        }

        RefreshHoverState(position);
    }

    private void UpdateValueFromPosition(double positionX)
    {
        double normalized = NormalizePosition(positionX);
        double value = Minimum + normalized * (Maximum - Minimum);

        if (_activeHandle == ActiveHandle.Lower)
        {
            value = Math.Min(value, UpperValue - MinimumSeparation);
            value = Math.Clamp(value, Minimum, Maximum);
            LowerValue = value;
        }
        else if (_activeHandle == ActiveHandle.Upper)
        {
            value = Math.Max(value, LowerValue + MinimumSeparation);
            value = Math.Clamp(value, Minimum, Maximum);
            UpperValue = value;
        }
    }

    private double NormalizePosition(double positionX)
    {
        double usableWidth = Math.Max(0.0, LayoutCanvas.ActualWidth - HandleContainerSize);
        if (usableWidth <= 0.0)
        {
            return 0.0;
        }

        double offset = HandleContainerHalf;
        double clamped = Math.Clamp(positionX, offset, offset + usableWidth);
        return (clamped - offset) / usableWidth;
    }

    private double GetHandleCenterX(double value)
    {
        double usableWidth = Math.Max(0.0, LayoutCanvas.ActualWidth - HandleContainerSize);
        if (usableWidth <= 0.0)
        {
            return HandleContainerHalf;
        }

        double normalized = (value - Minimum) / (Maximum - Minimum);
        normalized = Math.Clamp(normalized, 0.0, 1.0);
        return HandleContainerHalf + normalized * usableWidth;
    }

    private void UpdateVisualState()
    {
        if (LayoutCanvas == null)
        {
            return;
        }

        double usableWidth = Math.Max(0.0, LayoutCanvas.ActualWidth - HandleContainerSize);
        double offset = HandleContainerHalf;
        double trackTop = (LayoutCanvas.ActualHeight - TrackThickness) / 2.0;
        double trackCenterY = trackTop + (TrackThickness / 2.0);

        double lowerCenterX = GetHandleCenterX(LowerValue);
        double upperCenterX = GetHandleCenterX(UpperValue);

        Canvas.SetLeft(TrackBackground, offset);
        Canvas.SetTop(TrackBackground, trackTop);
        TrackBackground.Width = usableWidth;

        Canvas.SetLeft(SelectionTrack, lowerCenterX);
        Canvas.SetTop(SelectionTrack, trackTop);
        SelectionTrack.Width = Math.Max(0.0, upperCenterX - lowerCenterX);

        Canvas.SetLeft(LowerHandle, lowerCenterX - HandleContainerHalf);
        Canvas.SetTop(LowerHandle, trackCenterY - HandleContainerHalf);

        Canvas.SetLeft(UpperHandle, upperCenterX - HandleContainerHalf);
        Canvas.SetTop(UpperHandle, trackCenterY - HandleContainerHalf);
    }

    private static void AnimateHandle(ScaleTransform transform, double target)
    {
        var animation = new DoubleAnimation
        {
            To = target,
            Duration = new Duration(ScaleAnimationDuration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };

        var storyboard = new Storyboard();
        Storyboard.SetTarget(animation, transform);
        Storyboard.SetTargetProperty(animation, "ScaleX");
        storyboard.Children.Add(animation);

        var animationY = new DoubleAnimation
        {
            To = target,
            Duration = new Duration(ScaleAnimationDuration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animationY, transform);
        Storyboard.SetTargetProperty(animationY, "ScaleY");
        storyboard.Children.Add(animationY);

        storyboard.Begin();
    }

    private void ResolveAccentBrush()
    {
        var accentBrush = ThemeAccentHelper.GetAccentBrush(this);

        _accentBrush = accentBrush;
        _accentSecondaryBrush = accentBrush;

        LowerHandleInner.Fill = accentBrush;
        UpperHandleInner.Fill = accentBrush;
        SelectionTrack.Fill = accentBrush;
    }

}

public sealed class RangeValueChangedEventArgs : EventArgs
{
    public RangeValueChangedEventArgs(double lowerValue, double upperValue, bool isUserInteraction)
    {
        LowerValue = lowerValue;
        UpperValue = upperValue;
        IsUserInteraction = isUserInteraction;
    }

    public double LowerValue { get; }
    public double UpperValue { get; }
    public bool IsUserInteraction { get; }
}

internal static class ColorHelpers
{
    public static Color WithOpacity(Color color, double opacity)
    {
        byte alpha = (byte)Math.Clamp(opacity * 255.0, 0, 255);
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }
}
