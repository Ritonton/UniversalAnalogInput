using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using UniversalAnalogInputUI.Services.Interfaces;
using UniversalAnalogInputUI.Enums;
using UniversalAnalogInputUI.Services;
using UniversalAnalogInputUI.Controls;
using UniversalAnalogInputUI.Models;
using UniversalAnalogInputUI.Helpers;

namespace UniversalAnalogInputUI.Views
{
    /// <summary>Provides curve editing, dead zone tuning, and mapping selection visuals.</summary>
    public sealed partial class CurveConfigurationPage : Page
    {
        private static readonly SolidColorBrush GridBrush = new SolidColorBrush(Colors.Gray) { Opacity = 0.15 };
        private static readonly SolidColorBrush OuterCircleBrush = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush FixedFillBrush = new SolidColorBrush(Colors.Gray);
        private static readonly SolidColorBrush FixedStrokeBrush = new SolidColorBrush(Colors.DarkGray);
        private static readonly SolidColorBrush TransparentOverlayBrush = new SolidColorBrush(Colors.Transparent);

        private SolidColorBrush? _accentBrush = null;
        private SolidColorBrush? _accentSecondaryBrush = null;

        private List<CurvePoint> _curvePoints = new List<CurvePoint>();
        private List<CurvePoint> _sortedPoints = new List<CurvePoint>();

        private CurvePoint? _draggedPoint = null;
        private CurvePoint? _selectedPoint = null;
        private CurvePoint? _hoveredPoint = null;

        private long _lastRedrawTimestamp = 0;
        private const long MinRedrawIntervalMs = 16;
        private bool _isDragging = false;

        private DispatcherTimer? _deadZoneUpdateTimer = null;
        private bool _hasPendingMappingUpdate = false;
        private const int DeadZoneUpdateIntervalMs = 16;

        private DispatcherTimer? _rustSyncDebounceTimer = null;
        private const int RustSyncDebounceMs = 1000;

        private const double PointRadius = 8;
        private const double PointHitRadius = 20;
        private const double CanvasPadding = 10;
        private const double SelectedInnerScale = 1.0;
        private const double HoveredInnerScale = 0.4;
        private const double NormalInnerScale = 0.7;
        private static readonly TimeSpan InnerCircleAnimationDuration = TimeSpan.FromMilliseconds(150);
        private const int MaxInteractivePoints = 12;
        private const double DeadZoneEpsilon = 0.001;
        private const int MinDeadZoneSeparation = 5;
        private int _innerDeadZone = 5;
        private int _outerDeadZone = 95;
        private bool _isControlsRowCompact = false;
        private const double ControlsCompactWidthThreshold = 400;
        private const string SmoothToggleOnText = "On";
        private const string SmoothToggleOffText = "Off";
        private const string SmoothToggleMixText = "Mix";

        private bool _useSmoothCurve = false;
        private readonly IToastService? _toastService;
        private readonly IDialogService? _dialogService;
        private readonly IStatusMonitorService? _statusMonitorService;
        private IMappingManagementService? _mappingService;

        private List<KeyMapping> _selectedMappings = new();
        private bool _isUpdatingFromSelection = false;

        private bool _innerDeadZoneIsMixed = false;
        private bool _outerDeadZoneIsMixed = false;
        private bool _smoothCurveIsMixed = false;
        private bool _curveIsMixed = false;
        private Brush? _curveOverlayDefaultBackground = null;
        private double? _curveOverlayDefaultOpacity = null;

        public CurveConfigurationPage()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            this.ActualThemeChanged += CurveConfigurationPage_ActualThemeChanged;
            try
            {
                _toastService = App.Services.GetService(typeof(IToastService)) as IToastService;
                _dialogService = App.Services.GetService(typeof(IDialogService)) as IDialogService;
                _statusMonitorService = App.Services.GetService(typeof(IStatusMonitorService)) as IStatusMonitorService;
                _mappingService = App.Services.GetService(typeof(IMappingManagementService)) as IMappingManagementService;
                _statusMonitorService?.AppendLiveInput("CurveConfigurationPage initialized; services resolved.");
            }
            catch
            {
                _toastService = null;
                _dialogService = null;
                _statusMonitorService = null;
                _mappingService = null;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _statusMonitorService?.AppendLiveInput("CurveConfigurationPage loaded.");

            var accentBrush = ThemeAccentHelper.GetAccentBrush(this);
            _accentBrush = accentBrush;
            _accentSecondaryBrush = accentBrush;

            _statusMonitorService?.AppendLiveInput($"Theme-aware accent brush loaded (ActualTheme: {ActualTheme}).");

            UpdateControlsRowLayout(ControlsRowGrid.ActualWidth);
            if (CurveEditorOverlay != null)
            {
                _curveOverlayDefaultBackground ??= CurveEditorOverlay.Background;
                if (!_curveOverlayDefaultOpacity.HasValue)
                {
                    _curveOverlayDefaultOpacity = CurveEditorOverlay.Opacity;
                }
            }

            try
            {
                _statusMonitorService?.AppendLiveInput("Subscribing to MappingsListControl.");
                var mappingsListControl = MainWindow.Instance?.MappingsListControlInstance;
                if (mappingsListControl == null)
                {
                    _statusMonitorService?.AppendLiveInput("MappingsListControl not available yet.");
                    return;
                }
                mappingsListControl.MappingSelectionChanged += OnMappingSelectionChanged;

                var selectedMappings = mappingsListControl.SelectedMappings;
                int selectionCount = selectedMappings.Count();
                _statusMonitorService?.AppendLiveInput($"Current selection: {selectionCount} mapping(s).");

                UpdateSelection(selectedMappings);
            }
            catch (Exception ex)
            {
                _statusMonitorService?.AppendLiveInput($"Failed to get MappingsListControl: {ex.Message}");
                ClearAndDisable();
            }
        }

        private void CurveConfigurationPage_ActualThemeChanged(FrameworkElement sender, object args)
        {
            _statusMonitorService?.AppendLiveInput($"Theme changed to {ActualTheme}.");

            UpdateAccentBrushes();

            RedrawCurve();
        }

        private void UpdateAccentBrushes()
        {
            var accentBrush = ThemeAccentHelper.GetAccentBrush(this);
            _accentBrush = accentBrush;
            _accentSecondaryBrush = accentBrush;

            _statusMonitorService?.AppendLiveInput($"Theme brush updated (ActualTheme: {ActualTheme}).");
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _statusMonitorService?.AppendLiveInput("CurveConfigurationPage unloaded.");

            try
            {
                var mappingsListControl = MainWindow.Instance?.MappingsListControlInstance;
                if (mappingsListControl != null)
                {
                    mappingsListControl.MappingSelectionChanged -= OnMappingSelectionChanged;
                    _statusMonitorService?.AppendLiveInput("Unsubscribed from MappingSelectionChanged.");
                }
            }
            catch
            {
            }
        }

        private void OnMappingSelectionChanged(object? sender, IEnumerable<KeyMapping> selectedMappings)
        {
            int selectionCount = selectedMappings.Count();
            _statusMonitorService?.AppendLiveInput($"Selection changed: {selectionCount} mapping(s).");
            UpdateSelection(selectedMappings);
        }

        private void InitializeDefaultCurve()
        {
            _curveIsMixed = false;
            _curvePoints.Clear();
            _curvePoints.Add(new CurvePoint(0, 0, isFixed: true));  // Bottom-left (0,0)
            _curvePoints.Add(new CurvePoint(1, 1, isFixed: true));  // Top-right (1,1)
            UpdateSortedPoints();
            UpdateCurveMixedVisualState();
        }

        /// <summary>Updates the cached sorted points list after modifying _curvePoints.</summary>
        private void UpdateSortedPoints()
        {
            _sortedPoints = _curvePoints.OrderBy(p => p.X).ToList();
        }

        private void UpdateCurveMixedVisualState()
        {
            if (CurveEditorOverlay == null)
                return;

            var canvasOverlay = CurveCanvasOverlay;
            var titleBlock = CurveCanvasOverlayTitle;
            var subtitleBlock = CurveCanvasOverlaySubtitle;

            if (_curveIsMixed)
            {
                CurveEditorOverlay.IsHitTestVisible = false;
                CurveEditorOverlay.Visibility = Visibility.Visible;
                CurveEditorOverlay.Background = TransparentOverlayBrush;
                CurveEditorOverlay.Opacity = 1.0;

                if (canvasOverlay != null)
                {
                    canvasOverlay.Visibility = Visibility.Visible;
                }

                if (titleBlock != null)
                {
                    titleBlock.Text = "Mixed";
                    titleBlock.Visibility = Visibility.Visible;
                }

                if (subtitleBlock != null)
                {
                    subtitleBlock.Text = "Click to override to apply a shared curve.";
                    subtitleBlock.Visibility = Visibility.Visible;
                }
            }
            else
            {
                CurveEditorOverlay.IsHitTestVisible = false;
                if (_curveOverlayDefaultBackground != null)
                {
                    CurveEditorOverlay.Background = _curveOverlayDefaultBackground;
                }
                if (_curveOverlayDefaultOpacity.HasValue)
                {
                    CurveEditorOverlay.Opacity = _curveOverlayDefaultOpacity.Value;
                }

                if (canvasOverlay != null)
                {
                    canvasOverlay.Visibility = Visibility.Collapsed;
                }

                if (titleBlock != null)
                {
                    titleBlock.Text = string.Empty;
                    titleBlock.Visibility = Visibility.Collapsed;
                }

                if (subtitleBlock != null)
                {
                    subtitleBlock.Text = string.Empty;
                    subtitleBlock.Visibility = Visibility.Collapsed;
                }

                if (!CurveEditorOverlay.IsHitTestVisible)
                {
                    CurveEditorOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        #region Drawing

        /// <summary>Checks if enough time has elapsed since last redraw.</summary>
        private bool CanRedrawNow()
        {
            long currentTimestamp = Environment.TickCount64;
            long elapsed = currentTimestamp - _lastRedrawTimestamp;

            if (elapsed >= MinRedrawIntervalMs)
            {
                _lastRedrawTimestamp = currentTimestamp;
                return true;
            }

            return false;
        }

        /// <summary>Starts timer for mapping updates during dead zone slider drag.</summary>
        private void StartDeadZoneSliderDragTimer()
        {
            if (_deadZoneUpdateTimer != null)
                return;

            _deadZoneUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DeadZoneUpdateIntervalMs)
            };
            _deadZoneUpdateTimer.Tick += OnDeadZoneUpdateTimerTick;
            _deadZoneUpdateTimer.Start();

            _hasPendingMappingUpdate = false;
        }

        /// <summary>Stops timer and sends any pending update immediately.</summary>
        private void StopDeadZoneSliderDragTimer()
        {
            if (_deadZoneUpdateTimer == null)
                return;

            _deadZoneUpdateTimer.Stop();
            _deadZoneUpdateTimer.Tick -= OnDeadZoneUpdateTimerTick;
            _deadZoneUpdateTimer = null;

            if (_hasPendingMappingUpdate)
            {
                _hasPendingMappingUpdate = false;
                ApplyChangesToSelectedMappings();
            }
        }

        /// <summary>Processes pending mapping updates during slider drag.</summary>
        private void OnDeadZoneUpdateTimerTick(object? sender, object e)
        {
            if (_hasPendingMappingUpdate)
            {
                _hasPendingMappingUpdate = false;
                ApplyChangesToSelectedMappings();
            }
        }

        private void RedrawCurve()
        {
            if (CurvePreviewCanvas == null || CurvePreviewCanvas.ActualWidth == 0 || CurvePreviewCanvas.ActualHeight == 0)
                return;

            double width = CurvePreviewCanvas.ActualWidth;
            double height = CurvePreviewCanvas.ActualHeight;

            CurvePreviewCanvas.Children.Clear();

            if (_curveIsMixed)
            {
                return;
            }

            DrawCurveSpline(width, height);

            DrawControlPoints(width, height);
        }

        /// <summary>Throttled redraw used during drag to cap updates.</summary>
        private void RedrawCurveThrottled()
        {
            if (CanRedrawNow())
            {
                RedrawCurve();
                UpdatePointNumberBoxes();
            }
        }

        /// <summary>Draws the static grid on GridCanvas when its size changes.</summary>
        private void DrawGrid(double width, double height)
        {
            if (GridCanvas == null || width == 0 || height == 0)
                return;

            GridCanvas.Children.Clear();

            double usableWidth = width - 2 * CanvasPadding;
            double usableHeight = height - 2 * CanvasPadding;

            for (int i = 1; i < 4; i++)
            {
                double x = CanvasPadding + usableWidth * i / 4;
                var line = new Line
                {
                    X1 = x,
                    Y1 = CanvasPadding,
                    X2 = x,
                    Y2 = height - CanvasPadding,
                    Stroke = GridBrush,
                    StrokeThickness = 1
                };
                GridCanvas.Children.Add(line);
            }

            for (int i = 1; i < 4; i++)
            {
                double y = CanvasPadding + usableHeight * i / 4;
                var line = new Line
                {
                    X1 = CanvasPadding,
                    Y1 = y,
                    X2 = width - CanvasPadding,
                    Y2 = y,
                    Stroke = GridBrush,
                    StrokeThickness = 1
                };
                GridCanvas.Children.Add(line);
            }
        }

        private void DrawCurveSpline(double width, double height)
        {
            if (_sortedPoints.Count < 2 || _accentSecondaryBrush == null)
                return;

            double usableWidth = width - 2 * CanvasPadding;
            double usableHeight = height - 2 * CanvasPadding;

            var polyline = new Polyline
            {
                Stroke = _accentSecondaryBrush,
                StrokeThickness = 3
            };

            const int highResolution = 200;
            const int lowResolution = 50;
            int resolution = _isDragging ? lowResolution : highResolution;

            for (int i = 0; i < resolution; i++)
            {
                double t = i / (double)(resolution - 1);
                double y = EvaluateCurve(t, _sortedPoints);

                double canvasX = CanvasPadding + t * usableWidth;
                double canvasY = height - CanvasPadding - (y * usableHeight);

                polyline.Points.Add(new Point(canvasX, canvasY));
            }

            CurvePreviewCanvas.Children.Add(polyline);
        }

        private double EvaluateCurve(double x, List<CurvePoint> sortedPoints)
        {
            if (_useSmoothCurve)
            {
                return EvaluateSmoothCurve(x, sortedPoints);
            }
            else
            {
                return EvaluateLinearCurve(x, sortedPoints);
            }
        }

        private double EvaluateLinearCurve(double x, List<CurvePoint> sortedPoints)
        {
            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                var p1 = sortedPoints[i];
                var p2 = sortedPoints[i + 1];

                if (x >= p1.X && x <= p2.X)
                {
                    double dx = p2.X - p1.X;
                    if (Math.Abs(dx) < 1e-10)
                    {
                        return (p1.Y + p2.Y) * 0.5;
                    }

                    double t = (x - p1.X) / dx;
                    return p1.Y + t * (p2.Y - p1.Y);
                }
            }

            if (x <= sortedPoints[0].X)
                return sortedPoints[0].Y;

            return sortedPoints[^1].Y;
        }

        private double EvaluateSmoothCurve(double x, List<CurvePoint> sortedPoints)
        {
            for (int i = 0; i < sortedPoints.Count - 1; i++)
            {
                var p1 = sortedPoints[i];
                var p2 = sortedPoints[i + 1];

                if (x >= p1.X && x <= p2.X)
                {
                    double dx = p2.X - p1.X;
                    if (Math.Abs(dx) < 1e-10)
                    {
                        return (p1.Y + p2.Y) * 0.5;
                    }

                    double m1, m2;

                    if (i == 0)
                    {
                        m1 = (p2.Y - p1.Y) / dx;
                    }
                    else
                    {
                        var p0 = sortedPoints[i - 1];
                        double dx0 = p1.X - p0.X;
                        if (Math.Abs(dx0) < 1e-10) dx0 = 1e-10;
                        m1 = ((p2.Y - p1.Y) / dx + (p1.Y - p0.Y) / dx0) * 0.5;
                    }

                    if (i == sortedPoints.Count - 2)
                    {
                        m2 = (p2.Y - p1.Y) / dx;
                    }
                    else
                    {
                        var p3 = sortedPoints[i + 2];
                        double dx2 = p3.X - p2.X;
                        if (Math.Abs(dx2) < 1e-10) dx2 = 1e-10;
                        m2 = ((p3.Y - p2.Y) / dx2 + (p2.Y - p1.Y) / dx) * 0.5;
                    }

                    double t = (x - p1.X) / dx;

                    double t2 = t * t;
                    double t3 = t2 * t;

                    double h00 = 2 * t3 - 3 * t2 + 1;
                    double h10 = t3 - 2 * t2 + t;
                    double h01 = -2 * t3 + 3 * t2;
                    double h11 = t3 - t2;

                    double y = h00 * p1.Y + h10 * dx * m1 + h01 * p2.Y + h11 * dx * m2;

                    return Math.Clamp(y, 0, 1);
                }
            }

            if (x <= sortedPoints[0].X)
                return sortedPoints[0].Y;

            return sortedPoints[^1].Y;
        }

        private void DrawControlPoints(double width, double height)
        {
            if (_accentSecondaryBrush == null)
                return;

            double usableWidth = width - 2 * CanvasPadding;
            double usableHeight = height - 2 * CanvasPadding;

            foreach (var point in _curvePoints)
            {
                double canvasX = CanvasPadding + point.X * usableWidth;
                double canvasY = height - CanvasPadding - (point.Y * usableHeight);

                if (point.IsFixed)
                {
                    var fixedEllipse = new Ellipse
                    {
                        Width = PointRadius * 2,
                        Height = PointRadius * 2,
                        Tag = point,
                        Fill = FixedFillBrush,
                        Stroke = FixedStrokeBrush,
                        StrokeThickness = 2
                    };

                    Canvas.SetLeft(fixedEllipse, canvasX - PointRadius);
                    Canvas.SetTop(fixedEllipse, canvasY - PointRadius);
                    CurvePreviewCanvas.Children.Add(fixedEllipse);
                }
                else
                {
                    var outerEllipse = new Ellipse
                    {
                        Width = PointRadius * 2,
                        Height = PointRadius * 2,
                        Fill = OuterCircleBrush
                    };

                    Canvas.SetLeft(outerEllipse, canvasX - PointRadius);
                    Canvas.SetTop(outerEllipse, canvasY - PointRadius);
                    CurvePreviewCanvas.Children.Add(outerEllipse);

                    double targetScale = point == _selectedPoint
                        ? SelectedInnerScale
                        : point == _hoveredPoint
                            ? HoveredInnerScale
                            : NormalInnerScale;

                    var transform = point.InnerScaleTransform;
                    if (transform == null)
                    {
                        double initialScale = targetScale;
                        transform = new ScaleTransform
                        {
                            ScaleX = initialScale,
                            ScaleY = initialScale
                        };
                        point.InnerScaleTransform = transform;
                        point.LastTargetScale = initialScale;
                    }

                    var innerEllipse = new Ellipse
                    {
                        Width = PointRadius * 2,
                        Height = PointRadius * 2,
                        Fill = _accentSecondaryBrush,
                        Tag = point,
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        RenderTransform = transform
                    };

                    Canvas.SetLeft(innerEllipse, canvasX - PointRadius);
                    Canvas.SetTop(innerEllipse, canvasY - PointRadius);
                    CurvePreviewCanvas.Children.Add(innerEllipse);

                    bool scaleChanged = double.IsNaN(point.LastTargetScale) ||
                                       Math.Abs(point.LastTargetScale - targetScale) > 0.001;

                    if (scaleChanged)
                    {
                        point.LastTargetScale = targetScale;

                        if (_isDragging)
                        {
                            transform.ScaleX = targetScale;
                            transform.ScaleY = targetScale;
                        }
                        else
                        {
                            AnimateInnerCircle(transform, targetScale);
                        }
                    }
                }
            }
        }

        #endregion

        #region Layout

        /// <summary>
        /// Handler for GridCanvas size changes. Redraws the static grid.
        /// </summary>
        private void GridCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
                return;

            DrawGrid(e.NewSize.Width, e.NewSize.Height);
        }

        private void CurvePreviewCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            double width = CurvePreviewCanvas?.ActualWidth ?? 0;
            double height = CurvePreviewCanvas?.ActualHeight ?? 0;
            _statusMonitorService?.AppendLiveInput($"Canvas loaded ({width:F1}x{height:F1}); drawing curve.");
            RedrawCurve();
        }

        /// <summary> Handler for CurvePreviewCanvas size changes. Triggers a redraw of the curve. </summary>
        private void CurvePreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawCurve();
        }

        #endregion

        #region Mouse Interaction

        private void ExitCurveMixedState()
        {
            InitializeDefaultCurve();
            _selectedPoint = null;
            _draggedPoint = null;
            _hoveredPoint = null;
            RemovePointButton.IsEnabled = false;
            PointXInput.IsEnabled = false;
            PointYInput.IsEnabled = false;
            UpdatePointNumberBoxes();
            RedrawCurve();
        }

        private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var pointerPoint = e.GetCurrentPoint(CurvePreviewCanvas);

            if (!pointerPoint.Properties.IsLeftButtonPressed)
                return;

            CurvePreviewCanvas.Focus(FocusState.Pointer);

            if (_curveIsMixed)
            {
                ExitCurveMixedState();
            }

            var position = pointerPoint.Position;
            var point = FindPointAtPosition(position);

            if (point != null && !point.IsFixed)
            {
                _draggedPoint = point;
                _selectedPoint = point;
                _isDragging = true;
                RemovePointButton.IsEnabled = true;
                UpdatePointNumberBoxes();

                CurvePreviewCanvas.CapturePointer(e.Pointer);

                RedrawCurve();
            }
            else
            {
                if (GetInteractivePointCount() >= MaxInteractivePoints)
                {
                    _toastService?.ShowToast("Point limit reached",
                        $"Only {MaxInteractivePoints} adjustable points are supported.",
                        ToastType.Warning);
                    return;
                }

                double width = CurvePreviewCanvas.ActualWidth;
                double height = CurvePreviewCanvas.ActualHeight;
                double usableWidth = width - 2 * CanvasPadding;
                double usableHeight = height - 2 * CanvasPadding;

                double newX = Math.Clamp((position.X - CanvasPadding) / usableWidth, 0, 1);
                double newY = Math.Clamp(1.0 - ((position.Y - CanvasPadding) / usableHeight), 0, 1);

                if (IsValidNewPoint(newX, newY))
                {
                    var newPoint = new CurvePoint(newX, newY, isFixed: false);
                    newPoint.InnerScaleTransform = new ScaleTransform
                    {
                        ScaleX = 0.0,
                        ScaleY = 0.0
                    };
                    newPoint.LastTargetScale = 0.0;
                    _curvePoints.Add(newPoint);
                    UpdateSortedPoints();
                    _selectedPoint = newPoint;
                    _draggedPoint = newPoint;
                    _isDragging = true;
                    RemovePointButton.IsEnabled = true;
                    UpdatePointNumberBoxes();

                    CurvePreviewCanvas.CapturePointer(e.Pointer);

                    RedrawCurve();
                }
                else
                {
                    _selectedPoint = null;
                    RemovePointButton.IsEnabled = false;
                    PointXInput.IsEnabled = false;
                    PointYInput.IsEnabled = false;
                    RedrawCurve();
                }
            }
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_curveIsMixed)
                return;

            var position = e.GetCurrentPoint(CurvePreviewCanvas).Position;

            if (_draggedPoint != null && !_draggedPoint.IsFixed)
            {
                _isUpdatingPointFromCanvas = true;
                try
                {
                    if (CurvePreviewCanvas == null)
                        return;

                    double width = CurvePreviewCanvas.ActualWidth;
                    double height = CurvePreviewCanvas.ActualHeight;

                    if (width <= 2 * CanvasPadding || height <= 2 * CanvasPadding)
                        return;

                    double usableWidth = width - 2 * CanvasPadding;
                    double usableHeight = height - 2 * CanvasPadding;

                    double targetX = Math.Clamp((position.X - CanvasPadding) / usableWidth, 0, 1);
                    double targetY = Math.Clamp(1.0 - ((position.Y - CanvasPadding) / usableHeight), 0, 1);

                    if (double.IsNaN(targetX) || double.IsInfinity(targetX) ||
                        double.IsNaN(targetY) || double.IsInfinity(targetY))
                        return;

                    var validPosition = FindClosestValidPosition(_draggedPoint, targetX, targetY);

                    if (double.IsNaN(validPosition.X) || double.IsInfinity(validPosition.X) ||
                        double.IsNaN(validPosition.Y) || double.IsInfinity(validPosition.Y))
                        return;

                    double oldX = _draggedPoint.X;

                    _draggedPoint.X = validPosition.X;
                    _draggedPoint.Y = validPosition.Y;

                    UpdateSortedPoints();

                    if (!CheckAllPointsSpacing())
                    {
                        double acceptedX = BinarySearchValidPosition(_draggedPoint, oldX, validPosition.X);
                        _draggedPoint.X = acceptedX;
                        _draggedPoint.Y = validPosition.Y;
                        UpdateSortedPoints();
                    }

                    RedrawCurveThrottled();
                }
                finally
                {
                    _isUpdatingPointFromCanvas = false;
                }
            }
            else
            {
                var hoveredPoint = FindPointAtPosition(position);

                if (hoveredPoint != null && hoveredPoint.IsFixed)
                    hoveredPoint = null;

                if (hoveredPoint != _hoveredPoint)
                {
                    _hoveredPoint = hoveredPoint;
                    RedrawCurve();
                }
            }
        }

        private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_draggedPoint != null)
            {
                CurvePreviewCanvas.ReleasePointerCapture(e.Pointer);
                _draggedPoint = null;
                _isDragging = false;

                RedrawCurve();

                ApplyChangesToSelectedMappings();
            }
        }

        private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hoveredPoint = null;
            RedrawCurve();
        }

        private void Canvas_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (_curveIsMixed)
            {
                ExitCurveMixedState();
                e.Handled = true;
                return;
            }

            var position = e.GetPosition(CurvePreviewCanvas);
            var point = FindPointAtPosition(position);

            if (point != null && !point.IsFixed)
            {
                _curvePoints.Remove(point);
                UpdateSortedPoints();

                if (_selectedPoint == point)
                {
                    _selectedPoint = null;
                    RemovePointButton.IsEnabled = false;
                    PointXInput.IsEnabled = false;
                    PointYInput.IsEnabled = false;
                }

                if (_draggedPoint == point)
                {
                    _draggedPoint = null;
                }

                RedrawCurve();

                ApplyChangesToSelectedMappings();
            }

            e.Handled = true;
        }

        private CurvePoint? FindPointAtPosition(Point position)
        {
            double width = CurvePreviewCanvas.ActualWidth;
            double height = CurvePreviewCanvas.ActualHeight;
            double usableWidth = width - 2 * CanvasPadding;
            double usableHeight = height - 2 * CanvasPadding;

            foreach (var point in _curvePoints)
            {
                double canvasX = CanvasPadding + point.X * usableWidth;
                double canvasY = height - CanvasPadding - (point.Y * usableHeight);

                double distance = Math.Sqrt(
                    Math.Pow(position.X - canvasX, 2) +
                    Math.Pow(position.Y - canvasY, 2)
                );

                if (distance <= PointHitRadius)
                    return point;
            }

            return null;
        }

        private int GetInteractivePointCount()
        {
            return _curvePoints.Count(p => !p.IsFixed);
        }

        /// <summary>Finds the closest valid X between old and target while respecting spacing.</summary>
        private double BinarySearchValidPosition(CurvePoint movedPoint, double oldX, double targetX)
        {
            const double epsilon = 1e-6;
            const int maxIterations = 20;

            double left = oldX;
            double right = targetX;
            double bestValid = oldX;

            for (int i = 0; i < maxIterations; i++)
            {
                if (Math.Abs(right - left) < epsilon)
                    break;

                double mid = (left + right) * 0.5;

                movedPoint.X = mid;
                UpdateSortedPoints();

                if (CheckAllPointsSpacing())
                {
                    bestValid = mid;
                    left = mid;
                }
                else
                {
                    right = mid;
                }
            }

            return bestValid;
        }

        /// <summary>Returns the nearest valid X when the desired position violates spacing.</summary>
        private double FindClosestValidXPosition(CurvePoint movedPoint, double desiredX)
        {
            const double minSpacing = 0.01;
            const double epsilon = 1e-12;

            if (_sortedPoints == null || _sortedPoints.Count <= 1)
                return Math.Clamp(desiredX, minSpacing, 1.0 - minSpacing);

            double minX = minSpacing;
            double maxX = 1.0 - minSpacing;

            foreach (var otherPoint in _sortedPoints)
            {
                if (ReferenceEquals(otherPoint, movedPoint)) continue;
                if (otherPoint.IsFixed) continue;

                if (otherPoint.X < desiredX - epsilon)
                {
                    double leftBound = otherPoint.X + minSpacing;
                    if (leftBound > minX)
                        minX = leftBound;
                }
                else if (otherPoint.X > desiredX + epsilon)
                {
                    double rightBound = otherPoint.X - minSpacing;
                    if (rightBound < maxX)
                        maxX = rightBound;
                }
            }

            if (minX > maxX + epsilon)
            {
                double distToMin = Math.Abs(desiredX - minX);
                double distToMax = Math.Abs(desiredX - maxX);
                return distToMin <= distToMax ? minX : maxX;
            }

            return Math.Clamp(desiredX, minX, maxX);
        }

        /// <summary>
        /// Checks if ALL points respect minimum spacing constraints.
        /// Returns false if any violation is detected (does NOT modify positions).
        /// </summary>
        private bool CheckAllPointsSpacing()
        {
            const double minSpacing = 0.01;
            const double epsilon = 1e-12;

            if (_sortedPoints == null || _sortedPoints.Count <= 1)
                return true;

            for (int i = 0; i < _sortedPoints.Count - 1; i++)
            {
                var point1 = _sortedPoints[i];
                var point2 = _sortedPoints[i + 1];

                double distance = point2.X - point1.X;

                if (distance < minSpacing - epsilon)
                {
                    return false;
                }
            }

            foreach (var point in _sortedPoints)
            {
                if (point.IsFixed) continue;

                if (point.X < minSpacing - epsilon || point.X > 1.0 - minSpacing + epsilon)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Corrects a point's position after sorting to maintain spacing.</summary>
        private (double X, double Y) ValidatePositionAfterSort(CurvePoint movedPoint)
        {
            const double minSpacing = 0.01;
            const double epsilon = 1e-12;

            if (_sortedPoints == null || _sortedPoints.Count <= 1)
                return (movedPoint.X, movedPoint.Y);

            double currentX = movedPoint.X;
            double minX = minSpacing;
            double maxX = 1.0 - minSpacing;

            foreach (var otherPoint in _sortedPoints)
            {
                if (ReferenceEquals(otherPoint, movedPoint)) continue;
                if (otherPoint.IsFixed) continue;

                double distance = Math.Abs(otherPoint.X - currentX);

                if (distance < minSpacing - epsilon)
                {
                    if (otherPoint.X < currentX)
                    {
                        double leftBound = otherPoint.X + minSpacing;
                        if (leftBound > minX)
                            minX = leftBound;
                    }
                    else
                    {
                        double rightBound = otherPoint.X - minSpacing;
                        if (rightBound < maxX)
                            maxX = rightBound;
                    }
                }
            }

            double correctedX = currentX;
            if (correctedX < minX)
                correctedX = minX;
            else if (correctedX > maxX)
                correctedX = maxX;

            if (minX > maxX + epsilon)
                correctedX = (minX + maxX) * 0.5;

            return (correctedX, movedPoint.Y);
        }

        /// <summary>Finds a valid point position that respects spacing constraints during drag.</summary>
        private (double X, double Y) FindClosestValidPosition(CurvePoint movedPoint, double targetX, double targetY)
        {
            const double minSpacing = 0.01;
            const double epsilon = 1e-12;

            if (movedPoint == null)
                return (Math.Clamp(targetX, 0.0, 1.0), Math.Clamp(targetY, 0.0, 1.0));

            if (_sortedPoints == null || _sortedPoints.Count == 0)
                return (Math.Clamp(targetX, 0.0, 1.0), Math.Clamp(targetY, 0.0, 1.0));

            CurvePoint? leftNeighbor = null;
            CurvePoint? rightNeighbor = null;

            foreach (var point in _sortedPoints)
            {
                if (point == null) continue;
                if (ReferenceEquals(point, movedPoint)) continue;
                if (point.IsFixed) continue;

                if (point.X <= targetX + epsilon)
                {
                    if (leftNeighbor == null || point.X > leftNeighbor.X)
                        leftNeighbor = point;
                }

                if (point.X >= targetX - epsilon)
                {
                    if (rightNeighbor == null || point.X < rightNeighbor.X)
                        rightNeighbor = point;
                }
            }

            double minX = minSpacing;
            double maxX = 1.0 - minSpacing;

            if (leftNeighbor != null)
            {
                double leftBound = leftNeighbor.X + minSpacing;
                if (leftBound > minX)
                    minX = leftBound;
            }

            if (rightNeighbor != null)
            {
                double rightBound = rightNeighbor.X - minSpacing;
                if (rightBound < maxX)
                    maxX = rightBound;
            }

            if (minX > maxX + epsilon)
            {
                double chosen = Math.Abs(targetX - minX) <= Math.Abs(targetX - maxX) ? minX : maxX;
                return (Math.Clamp(chosen, 0.0, 1.0), Math.Clamp(targetY, 0.0, 1.0));
            }

            double validX = Math.Clamp(targetX, minX, maxX);
            double validY = Math.Clamp(targetY, 0.0, 1.0);

            return (validX, validY);
        }

        /// <summary>
        /// Validates that a new point can be added at the given position.
        /// </summary>
        private bool IsValidNewPoint(double x, double y)
        {
            const double minSpacing = 0.02;

            foreach (var point in _curvePoints)
            {
                if (Math.Abs(point.X - x) < minSpacing)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that a point's position maintains minimum spacing from all other points.
        /// Allows points to cross over each other during drag.
        /// </summary>
        private bool IsValidPointPosition(CurvePoint movedPoint)
        {
            if (movedPoint.IsFixed)
                return true;

            const double minSpacing = 0.01;

            foreach (var otherPoint in _curvePoints)
            {
                if (otherPoint == movedPoint)
                    continue;

                if (Math.Abs(movedPoint.X - otherPoint.X) < minSpacing)
                    return false;
            }

            return true;
        }

        #endregion

        #region Button Handlers

        private void RemovePointButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPoint != null && !_selectedPoint.IsFixed)
            {
                _curvePoints.Remove(_selectedPoint);
                UpdateSortedPoints();
                _selectedPoint = null;
                RemovePointButton.IsEnabled = false;
                PointXInput.IsEnabled = false;
                PointYInput.IsEnabled = false;
                RedrawCurve();

                ApplyChangesToSelectedMappings();
            }
        }

        private async void ResetCurveButton_Click(object sender, RoutedEventArgs e)
        {
            bool hasAdjustablePoints = GetInteractivePointCount() > 0;
            if (hasAdjustablePoints && _dialogService != null)
            {
                bool confirmed = await _dialogService.ConfirmAsync("Reset curve", "Reset to the default linear curve?");
                if (!confirmed)
                {
                    return;
                }
            }

            _selectedPoint = null;
            _draggedPoint = null;
            _hoveredPoint = null;
            RemovePointButton.IsEnabled = false;

            _useSmoothCurve = false;
            SmoothCurveToggle.IsOn = false;
            _smoothCurveIsMixed = false;
            UpdateSmoothCurveToggleState();

            InitializeDefaultCurve();
            RedrawCurve();
            UpdateDeadZoneInputsState();

            ApplyChangesToSelectedMappings();
        }

        private bool _isUpdatingFromSlider = false;
        private bool _isUpdatingFromNumberBox = false;
        private bool _isUpdatingPointFromCanvas = false;

        private void DeadZoneSlider_RangeValueChanged(object sender, RangeValueChangedEventArgs e)
        {
            if (_isUpdatingFromNumberBox || _isUpdatingFromSelection)
                return;

            _isUpdatingFromSlider = true;
            try
            {
                int newInner = (int)Math.Clamp(Math.Round(e.LowerValue), 0, 100);
                int newOuter = (int)Math.Clamp(Math.Round(e.UpperValue), 0, 100);

                if (HasDeadZoneChanged(newInner, _innerDeadZone))
                {
                    _innerDeadZoneIsMixed = false;
                    _innerDeadZone = newInner;
                }

                if (HasDeadZoneChanged(newOuter, _outerDeadZone))
                {
                    _outerDeadZoneIsMixed = false;
                    _outerDeadZone = newOuter;
                }

                UpdateDeadZoneInputsState();

                if (e.IsUserInteraction)
                {
                    if (_deadZoneUpdateTimer == null)
                    {
                        StartDeadZoneSliderDragTimer();
                    }
                    _hasPendingMappingUpdate = true;
                }
                else
                {
                    StopDeadZoneSliderDragTimer();
                    ApplyChangesToSelectedMappings();
                }
            }
            finally
            {
                _isUpdatingFromSlider = false;
            }
        }

        /// <summary>
        /// Helper method to handle GotFocus event for dead zone NumberBox inputs.
        /// Clears "Mix" placeholder and shows percent sign when user focuses to edit.
        /// </summary>
        private void HandleDeadZoneInputGotFocus(NumberBox? numberBox, UIElement percentSign, UIElement tooltip, ref bool isMixedFlag)
        {
            if (numberBox != null && numberBox.PlaceholderText == "Mix")
            {
                numberBox.PlaceholderText = "";
                percentSign.Visibility = Visibility.Visible;
                tooltip.Visibility = Visibility.Collapsed;
                isMixedFlag = false;
            }
        }

        /// <summary>
        /// Helper method to handle LostFocus event for dead zone NumberBox inputs.
        /// Restores "Mix" placeholder if user didn't enter a value.
        /// </summary>
        private void HandleDeadZoneInputLostFocus(NumberBox? numberBox, UIElement percentSign, UIElement tooltip, ref bool isMixedFlag)
        {
            if (numberBox != null && double.IsNaN(numberBox.Value) && string.IsNullOrEmpty(numberBox.PlaceholderText))
            {
                numberBox.PlaceholderText = "Mix";
                percentSign.Visibility = Visibility.Collapsed;
                tooltip.Visibility = Visibility.Visible;
                isMixedFlag = true;
            }
        }

        private void DeadZoneLowerInput_GotFocus(object sender, RoutedEventArgs e)
        {
            HandleDeadZoneInputGotFocus(sender as NumberBox, DeadZoneLowerPercentSign, DeadZoneLowerTooltip, ref _innerDeadZoneIsMixed);
        }

        private void DeadZoneLowerInput_LostFocus(object sender, RoutedEventArgs e)
        {
            HandleDeadZoneInputLostFocus(sender as NumberBox, DeadZoneLowerPercentSign, DeadZoneLowerTooltip, ref _innerDeadZoneIsMixed);
        }

        /// <summary>
        /// Helper method to handle ValueChanged event for dead zone NumberBox inputs.
        /// Validates, clamps, and applies the new value while managing UI state.
        /// </summary>
        private void HandleDeadZoneInputValueChanged(
            NumberBox sender,
            NumberBoxValueChangedEventArgs args,
            bool isLowerInput,
            ref int targetDeadZone,
            UIElement percentSign,
            UIElement tooltip,
            ref bool isMixedFlag)
        {
            if (_isUpdatingFromSlider || _isUpdatingFromSelection)
                return;

            if (double.IsNaN(args.NewValue))
            {
                _isUpdatingFromNumberBox = true;
                try
                {
                    int defaultValue = isLowerInput ? 0 : Math.Min(100, _innerDeadZone + MinDeadZoneSeparation);
                    sender.Value = defaultValue;
                    targetDeadZone = defaultValue;

                    if (isLowerInput)
                        DeadZoneSlider.LowerValue = defaultValue;
                    else
                        DeadZoneSlider.UpperValue = defaultValue;

                    ApplyChangesToSelectedMappings();
                }
                finally
                {
                    _isUpdatingFromNumberBox = false;
                }
                return;
            }

            _isUpdatingFromNumberBox = true;
            try
            {
                int intValue = (int)Math.Clamp(Math.Round(args.NewValue), 0, 100);

                if (isLowerInput)
                {
                    if (intValue > _outerDeadZone - MinDeadZoneSeparation)
                        intValue = Math.Max(0, _outerDeadZone - MinDeadZoneSeparation);
                }
                else
                {
                    if (intValue < _innerDeadZone + MinDeadZoneSeparation)
                        intValue = Math.Min(100, _innerDeadZone + MinDeadZoneSeparation);
                }

                if (Math.Abs(args.NewValue - intValue) > 0.01)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (!_isUpdatingFromSlider && !_isUpdatingFromNumberBox && !_isUpdatingFromSelection)
                        {
                            sender.Value = intValue;
                        }
                    });
                }

                targetDeadZone = intValue;

                if (isLowerInput)
                    DeadZoneSlider.LowerValue = intValue;
                else
                    DeadZoneSlider.UpperValue = intValue;

                if (percentSign != null && !isMixedFlag)
                {
                    percentSign.Visibility = Visibility.Visible;
                    sender.PlaceholderText = "";
                    tooltip.Visibility = Visibility.Collapsed;
                }

                ApplyChangesToSelectedMappings();
            }
            finally
            {
                _isUpdatingFromNumberBox = false;
            }
        }

        private void DeadZoneLowerInput_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            HandleDeadZoneInputValueChanged(
                sender, args, true, ref _innerDeadZone,
                DeadZoneLowerPercentSign, DeadZoneLowerTooltip, ref _innerDeadZoneIsMixed);
        }

        private void DeadZoneUpperInput_GotFocus(object sender, RoutedEventArgs e)
        {
            HandleDeadZoneInputGotFocus(sender as NumberBox, DeadZoneUpperPercentSign, DeadZoneUpperTooltip, ref _outerDeadZoneIsMixed);
        }

        private void DeadZoneUpperInput_LostFocus(object sender, RoutedEventArgs e)
        {
            HandleDeadZoneInputLostFocus(sender as NumberBox, DeadZoneUpperPercentSign, DeadZoneUpperTooltip, ref _outerDeadZoneIsMixed);
        }

        private void DeadZoneUpperInput_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            HandleDeadZoneInputValueChanged(
                sender, args, false, ref _outerDeadZone,
                DeadZoneUpperPercentSign, DeadZoneUpperTooltip, ref _outerDeadZoneIsMixed);
        }


        private static void AnimateInnerCircle(ScaleTransform transform, double targetScale)
        {
            var easing = new CubicEase
            {
                EasingMode = EasingMode.EaseOut
            };

            var scaleXAnimation = new DoubleAnimation
            {
                To = targetScale,
                Duration = new Duration(InnerCircleAnimationDuration),
                EasingFunction = easing,
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(scaleXAnimation, transform);
            Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");

            var scaleYAnimation = new DoubleAnimation
            {
                To = targetScale,
                Duration = new Duration(InnerCircleAnimationDuration),
                EasingFunction = easing,
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(scaleYAnimation, transform);
            Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");

            var storyboard = new Storyboard();
            storyboard.Children.Add(scaleXAnimation);
            storyboard.Children.Add(scaleYAnimation);
            storyboard.Begin();
        }

        private void SmoothCurveToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingFromSelection)
                return;

            if (_smoothCurveIsMixed)
            {
                _smoothCurveIsMixed = false;
                UpdateSmoothCurveToggleState();
            }

            _useSmoothCurve = SmoothCurveToggle.IsOn;
            RedrawCurve();

            // Apply to selected mappings
            ApplyChangesToSelectedMappings();
        }

        private void NumberBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is NumberBox numberBox)
            {
                var textBox = FindDescendant<TextBox>(numberBox);
                if (textBox != null)
                {
                    textBox.TextAlignment = TextAlignment.Right;
                }

                if (numberBox.Tag is string tag && tag.Equals("HideClearButton", StringComparison.Ordinal))
                {
                    RemoveNumberBoxClearButton(numberBox);
                    numberBox.GotFocus -= NumberBox_RemoveClearButtonOnFocus;
                    numberBox.GotFocus += NumberBox_RemoveClearButtonOnFocus;
                }
            }
        }

        private void NumberBox_RemoveClearButtonOnFocus(object sender, RoutedEventArgs e)
        {
            if (sender is NumberBox numberBox && numberBox.Tag is string tag && tag.Equals("HideClearButton", StringComparison.Ordinal))
            {
                RemoveNumberBoxClearButton(numberBox);
            }
        }

        private void RemoveNumberBoxClearButton(NumberBox numberBox)
        {
            if (FindChildElementByName(numberBox, "DeleteButton") is Button deleteButton)
            {
                if (deleteButton.Parent is Panel panel)
                {
                    panel.Children.Remove(deleteButton);
                }
                else if (deleteButton.Parent is ContentControl contentControl && Equals(contentControl.Content, deleteButton))
                {
                    contentControl.Content = null;
                }
                else
                {
                    deleteButton.Visibility = Visibility.Collapsed;
                    deleteButton.IsHitTestVisible = false;
                }
            }
        }

        private void ControlsRowGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateControlsRowLayout(e.NewSize.Width);
        }

        private void UpdateControlsRowLayout(double width)
        {
            if (ControlsRowGrid == null || SmoothCurveToggle == null || CoordinateInputsPanel == null || ActionButtonsPanel == null)
                return;

            bool shouldCompact = width < ControlsCompactWidthThreshold && width > 0;
            if (shouldCompact == _isControlsRowCompact)
                return;

            _isControlsRowCompact = shouldCompact;

            if (shouldCompact)
            {
                Grid.SetColumn(SmoothCurveToggle, 0);
                Grid.SetRow(SmoothCurveToggle, 0);
                Grid.SetColumnSpan(SmoothCurveToggle, 3);
                SmoothCurveToggle.HorizontalAlignment = HorizontalAlignment.Left;

                Grid.SetColumn(CoordinateInputsPanel, 0);
                Grid.SetRow(CoordinateInputsPanel, 1);
                Grid.SetColumnSpan(CoordinateInputsPanel, 3);
                CoordinateInputsPanel.HorizontalAlignment = HorizontalAlignment.Left;
                CoordinateInputsPanel.Margin = new Thickness(0, 8, 0, 0);

                Grid.SetColumn(ActionButtonsPanel, 0);
                Grid.SetRow(ActionButtonsPanel, 2);
                Grid.SetColumnSpan(ActionButtonsPanel, 3);
                ActionButtonsPanel.HorizontalAlignment = HorizontalAlignment.Left;
                ActionButtonsPanel.Margin = new Thickness(0, 8, 0, 0);
            }
            else
            {
                Grid.SetColumn(SmoothCurveToggle, 0);
                Grid.SetRow(SmoothCurveToggle, 0);
                Grid.SetColumnSpan(SmoothCurveToggle, 1);
                SmoothCurveToggle.HorizontalAlignment = HorizontalAlignment.Stretch;

                Grid.SetColumn(CoordinateInputsPanel, 1);
                Grid.SetRow(CoordinateInputsPanel, 0);
                Grid.SetColumnSpan(CoordinateInputsPanel, 1);
                CoordinateInputsPanel.HorizontalAlignment = HorizontalAlignment.Center;
                CoordinateInputsPanel.Margin = new Thickness(0);

                Grid.SetColumn(ActionButtonsPanel, 2);
                Grid.SetRow(ActionButtonsPanel, 0);
                Grid.SetColumnSpan(ActionButtonsPanel, 1);
                ActionButtonsPanel.HorizontalAlignment = HorizontalAlignment.Right;
                ActionButtonsPanel.Margin = new Thickness(0);
            }
        }

        private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }

                var descendant = FindDescendant<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }

        private static FrameworkElement? FindChildElementByName(DependencyObject tree, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(tree);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(tree, i);
                if (child is FrameworkElement fe && fe.Name == name)
                {
                    return fe;
                }

                var descendant = FindChildElementByName(child, name);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }

        private void UpdatePointNumberBoxes()
        {
            if (_selectedPoint != null && !_selectedPoint.IsFixed)
            {
                PointXInput.IsEnabled = true;
                PointYInput.IsEnabled = true;

                _isUpdatingPointFromCanvas = true;
                try
                {
                    PointXInput.Value = Math.Round(_selectedPoint.X, 3);
                    PointYInput.Value = Math.Round(_selectedPoint.Y, 3);
                }
                finally
                {
                    _isUpdatingPointFromCanvas = false;
                }
            }
            else
            {
                PointXInput.IsEnabled = false;
                PointYInput.IsEnabled = false;
                PointXInput.Value = 0;
                PointYInput.Value = 0;
            }
        }

        private void PointXInput_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isUpdatingPointFromCanvas || _selectedPoint == null || _selectedPoint.IsFixed)
                return;

            if (double.IsNaN(args.NewValue))
            {
                sender.Value = _selectedPoint.X;
                return;
            }

            double roundedValue = Math.Round(args.NewValue, 3);
            double clampedValue = Math.Clamp(roundedValue, 0, 1);

            var validPosition = FindClosestValidPosition(_selectedPoint, clampedValue, _selectedPoint.Y);

            if (Math.Abs(args.NewValue - validPosition.X) > 0.0001)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!_isUpdatingPointFromCanvas && _selectedPoint != null)
                    {
                        sender.Value = Math.Round(validPosition.X, 3);
                    }
                });
            }

            _selectedPoint.X = validPosition.X;
            UpdateSortedPoints();
            RedrawCurve();

            ApplyChangesToSelectedMappings();
        }

        private void PointYInput_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isUpdatingPointFromCanvas || _selectedPoint == null || _selectedPoint.IsFixed)
                return;

            if (double.IsNaN(args.NewValue))
            {
                sender.Value = _selectedPoint.Y;
                return;
            }

            double roundedValue = Math.Round(args.NewValue, 3);
            double clampedValue = Math.Clamp(roundedValue, 0, 1);

            if (Math.Abs(args.NewValue - clampedValue) > 0.0001)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!_isUpdatingPointFromCanvas && _selectedPoint != null)
                    {
                        sender.Value = clampedValue;
                    }
                });
            }

            _selectedPoint.Y = clampedValue;
            RedrawCurve();

            ApplyChangesToSelectedMappings();
        }

        #endregion

        #region Selection Management (Master-Detail Interface)

        /// <summary>
        /// Updates the page to reflect the selected mappings from MappingsListControl.
        /// </summary>
        public void UpdateSelection(IEnumerable<KeyMapping> selectedMappings)
        {
            _selectedMappings = selectedMappings?.ToList() ?? new List<KeyMapping>();
            _statusMonitorService?.AppendLiveInput($"Updating selection ({_selectedMappings.Count} mapping(s)).");

            if (_selectedMappings.Count == 0)
            {
                _statusMonitorService?.AppendLiveInput("No mappings selected; clearing and disabling.");
                ClearAndDisable("No Mapping Selected", "Select a mapping to configure its response curve and dead zones.");
            }
            else
            {
                var analogMappings = _selectedMappings.Where(m => m.IsAnalog).ToList();
                var digitalCount = _selectedMappings.Count - analogMappings.Count;

                if (digitalCount > 0)
                {
                    _statusMonitorService?.AppendLiveInput($"Ignoring {digitalCount} digital mapping(s); processing {analogMappings.Count} analog mapping(s).");
                }

                if (analogMappings.Count == 0)
                {
                    _statusMonitorService?.AppendLiveInput("Only digital mappings selected; disabling editor.");
                    ClearAndDisable("Digital Mapping Selected", "Curves and dead zones only apply to analog controls (sticks and triggers), not buttons.");
                    _toastService?.ShowToast("Digital Mapping", "Buttons do not support curves or dead zones - only sticks and triggers do", Enums.ToastType.Info);
                    return;
                }

                var invalidMappings = analogMappings.Where(m => !m.IsValid).ToList();
                var conflictedMappings = analogMappings.Where(m => m.HasWarning).ToList();

                if (invalidMappings.Count > 0)
                {
                    _statusMonitorService?.AppendLiveInput($"Selection contains {invalidMappings.Count} invalid mapping(s); disabling editor.");
                    ClearAndDisable("Invalid Mapping Selected", "Complete the mapping (Key and Gamepad Control) before configuring curves.");
                    _toastService?.ShowToast("Invalid Mapping", "Please complete the mapping with a key and gamepad control first", Enums.ToastType.Warning);
                }
                else if (conflictedMappings.Count > 0)
                {
                    _statusMonitorService?.AppendLiveInput($"Selection contains {conflictedMappings.Count} conflicted mapping(s); disabling editor.");
                    ClearAndDisable("Conflicted Mapping Selected", "Resolve duplicate key conflicts before configuring curves.");
                    _toastService?.ShowToast("Duplicate Key Conflict", "Please resolve the duplicate key conflict first", Enums.ToastType.Warning);
                }
                else if (analogMappings.Count == 1)
                {
                    _statusMonitorService?.AppendLiveInput($"Loading analog mapping {analogMappings[0].KeyName}.");
                    LoadSingleMapping(analogMappings[0]);
                }
                else
                {
                    _statusMonitorService?.AppendLiveInput($"Loading {analogMappings.Count} analog mappings; checking for mixed values.");
                    LoadMultipleMappings(analogMappings);
                }
            }

            if (CurvePreviewCanvas?.ActualWidth > 0)
            {
                _statusMonitorService?.AppendLiveInput($"Canvas ready (width={CurvePreviewCanvas.ActualWidth:F1}); redrawing curve.");
                RedrawCurve();
            }
            else
            {
                _statusMonitorService?.AppendLiveInput("Canvas not ready yet; will draw on load.");
            }
        }

        /// <summary>
        /// Shows default values with overlay when no mapping is selected or selection is invalid.
        /// </summary>
        private void ClearAndDisable(string overlayTitle = "No Mapping Selected", string overlaySubtitle = "Select a mapping to configure its response curve and dead zones.")
        {
            _isUpdatingFromSelection = true;
            try
            {
                DeadZoneSlider.IsEnabled = true;
                DeadZoneLowerInput.IsEnabled = false;
                DeadZoneUpperInput.IsEnabled = false;
                CurvePreviewCanvas.Opacity = 1.0;
                CurvePreviewCanvas.IsHitTestVisible = true;
                SmoothCurveToggle.IsEnabled = true;
                ResetCurveButton.IsEnabled = true;
                RemovePointButton.IsEnabled = false;
                _selectedPoint = null;
                _draggedPoint = null;
                _hoveredPoint = null;

                InitializeDefaultCurve();
                DeadZoneSlider.LowerValue = 5;
                DeadZoneSlider.UpperValue = 95;
                _innerDeadZone = 5;
                _outerDeadZone = 95;
                _useSmoothCurve = false;
                SmoothCurveToggle.IsOn = false;
                _smoothCurveIsMixed = false;
                _curveIsMixed = false;
                UpdateDeadZoneInputsState();
                UpdateSmoothCurveToggleState();
                UpdateCurveMixedVisualState();

                DeadZoneOverlay.Visibility = Visibility.Visible;
                CurveEditorOverlay.Visibility = Visibility.Visible;
                CurveEditorOverlay.IsHitTestVisible = true;
                if (_curveOverlayDefaultBackground != null)
                {
                    CurveEditorOverlay.Background = _curveOverlayDefaultBackground;
                }
                if (_curveOverlayDefaultOpacity.HasValue)
                {
                    CurveEditorOverlay.Opacity = _curveOverlayDefaultOpacity.Value;
                }

                if (DeadZoneOverlay != null)
                {
                    ToolTipService.SetToolTip(DeadZoneOverlay, overlaySubtitle);
                }
                if (CurveEditorOverlay != null)
                {
                    ToolTipService.SetToolTip(CurveEditorOverlay, overlaySubtitle);
                }

                if (CurveCanvasOverlay != null)
                {
                    CurveCanvasOverlay.Visibility = Visibility.Collapsed;
                }
                if (CurveCanvasOverlayTitle != null)
                {
                    CurveCanvasOverlayTitle.Text = string.Empty;
                    CurveCanvasOverlayTitle.Visibility = Visibility.Collapsed;
                }
                if (CurveCanvasOverlaySubtitle != null)
                {
                    CurveCanvasOverlaySubtitle.Text = string.Empty;
                    CurveCanvasOverlaySubtitle.Visibility = Visibility.Collapsed;
                }

                _statusMonitorService?.AppendLiveInput($"Disabled with overlay: {overlayTitle}");

                if (CurvePreviewCanvas?.ActualWidth > 0)
                {
                    RedrawCurve();
                }
            }
            finally
            {
                _isUpdatingFromSelection = false;
            }
        }

        /// <summary>
        /// Updates the visual state of dead zone inputs based on values and mix state.
        /// Centralized method to ensure consistency.
        /// </summary>
        private void UpdateDeadZoneInputsState()
        {
            if (DeadZoneLowerInput == null || DeadZoneLowerPercentSign == null || DeadZoneLowerTooltip == null)
                return;
            if (DeadZoneUpperInput == null || DeadZoneUpperPercentSign == null || DeadZoneUpperTooltip == null)
                return;

            if (_innerDeadZoneIsMixed)
            {
                DeadZoneLowerInput.PlaceholderText = "Mix";
                DeadZoneLowerInput.Value = double.NaN;
                DeadZoneLowerPercentSign.Visibility = Visibility.Collapsed;
                if (DeadZoneLowerTooltip != null)
                    DeadZoneLowerTooltip.Visibility = Visibility.Visible;
            }
            else
            {
                DeadZoneLowerInput.PlaceholderText = "";
                DeadZoneLowerInput.Value = _innerDeadZone;
                DeadZoneLowerPercentSign.Visibility = Visibility.Visible;
                if (DeadZoneLowerTooltip != null)
                    DeadZoneLowerTooltip.Visibility = Visibility.Collapsed;
            }

            if (_outerDeadZoneIsMixed)
            {
                DeadZoneUpperInput.PlaceholderText = "Mix";
                DeadZoneUpperInput.Value = double.NaN;
                DeadZoneUpperPercentSign.Visibility = Visibility.Collapsed;
                if (DeadZoneUpperTooltip != null)
                    DeadZoneUpperTooltip.Visibility = Visibility.Visible;
            }
            else
            {
                DeadZoneUpperInput.PlaceholderText = "";
                DeadZoneUpperInput.Value = _outerDeadZone;
                DeadZoneUpperPercentSign.Visibility = Visibility.Visible;
                if (DeadZoneUpperTooltip != null)
                    DeadZoneUpperTooltip.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateSmoothCurveToggleState()
        {
            if (SmoothCurveToggle == null)
                return;

            SmoothCurveToggle.OnContent = _smoothCurveIsMixed ? SmoothToggleMixText : SmoothToggleOnText;
            SmoothCurveToggle.OffContent = _smoothCurveIsMixed ? SmoothToggleMixText : SmoothToggleOffText;

            if (SmoothCurveMixedTooltip != null)
            {
                SmoothCurveMixedTooltip.Visibility = _smoothCurveIsMixed ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Loads values from a single selected mapping.
        /// </summary>
        private void LoadSingleMapping(KeyMapping mapping)
        {
            _isUpdatingFromSelection = true;
            try
            {
                DeadZoneOverlay.Visibility = Visibility.Collapsed;
                CurveEditorOverlay.Visibility = Visibility.Collapsed;

                DeadZoneSlider.IsEnabled = true;
                DeadZoneLowerInput.IsEnabled = true;
                DeadZoneUpperInput.IsEnabled = true;
                CurvePreviewCanvas.Opacity = 1.0;
                CurvePreviewCanvas.IsHitTestVisible = true;
                SmoothCurveToggle.IsEnabled = true;
                ResetCurveButton.IsEnabled = true;
                _hoveredPoint = null;

                _innerDeadZoneIsMixed = false;
                _outerDeadZoneIsMixed = false;
                _smoothCurveIsMixed = false;
                _curveIsMixed = false;

                _innerDeadZone = (int)Math.Round(mapping.DeadZoneInner * 100);
                _outerDeadZone = (int)Math.Round(mapping.DeadZoneOuter * 100);
                DeadZoneSlider.LowerValue = _innerDeadZone;
                DeadZoneSlider.UpperValue = _outerDeadZone;

                UpdateDeadZoneInputsState();

                _useSmoothCurve = mapping.UseSmoothCurve;
                SmoothCurveToggle.IsOn = _useSmoothCurve;
                UpdateSmoothCurveToggleState();

                if (mapping.Curve == ResponseCurveTypes.Custom && mapping.CustomCurvePoints != null && mapping.CustomCurvePoints.Count > 0)
                {
                    _curvePoints.Clear();
                    foreach (var point in mapping.CustomCurvePoints)
                    {
                        bool isFixed = (point.X == 0 && point.Y == 0) || (point.X == 1 && point.Y == 1);
                        _curvePoints.Add(new CurvePoint(point.X, point.Y, isFixed));
                    }
                    UpdateSortedPoints();
                    UpdateCurveMixedVisualState();
                }
                else
                {
                    InitializeDefaultCurve();
                }
            }
            finally
            {
                _isUpdatingFromSelection = false;
            }
        }

        /// <summary>
        /// Loads values from multiple selected mappings, detecting mixed values individually per property.
        /// </summary>
        private void LoadMultipleMappings(List<KeyMapping> mappings)
        {
            _isUpdatingFromSelection = true;
            try
            {
                DeadZoneOverlay.Visibility = Visibility.Collapsed;
                CurveEditorOverlay.Visibility = Visibility.Collapsed;

                DeadZoneSlider.IsEnabled = true;
                DeadZoneLowerInput.IsEnabled = true;
                DeadZoneUpperInput.IsEnabled = true;
                CurvePreviewCanvas.Opacity = 1.0;
                CurvePreviewCanvas.IsHitTestVisible = true;
                SmoothCurveToggle.IsEnabled = true;
                ResetCurveButton.IsEnabled = true;
                RemovePointButton.IsEnabled = false;
                _selectedPoint = null;
                _draggedPoint = null;
                _hoveredPoint = null;

                var first = mappings[0];

                (_innerDeadZoneIsMixed, _outerDeadZoneIsMixed) = GetMixedDeadZoneState(mappings);

                _innerDeadZone = _innerDeadZoneIsMixed ? 0 : (int)Math.Round(first.DeadZoneInner * 100);
                _outerDeadZone = _outerDeadZoneIsMixed ? 100 : (int)Math.Round(first.DeadZoneOuter * 100);
                DeadZoneSlider.LowerValue = _innerDeadZone;
                DeadZoneSlider.UpperValue = _outerDeadZone;

                UpdateDeadZoneInputsState();

                bool mixedSmooth = mappings.Any(m => m.UseSmoothCurve != first.UseSmoothCurve);
                _smoothCurveIsMixed = mixedSmooth;
                if (mixedSmooth)
                {
                    SmoothCurveToggle.IsOn = false;
                    _useSmoothCurve = false;
                }
                else
                {
                    _useSmoothCurve = first.UseSmoothCurve;
                    SmoothCurveToggle.IsOn = _useSmoothCurve;
                }
                UpdateSmoothCurveToggleState();

                bool mixedCurves = HasMixedCurves(mappings);
                _curveIsMixed = mixedCurves;
                _curvePoints.Clear();

                if (mixedCurves)
                {
                    _curvePoints.Add(new CurvePoint(0, 0, isFixed: true));
                    _curvePoints.Add(new CurvePoint(1, 1, isFixed: true));
                    UpdateSortedPoints();
                }
                else
                {
                    if (first.Curve == ResponseCurveTypes.Custom && first.CustomCurvePoints != null && first.CustomCurvePoints.Count > 0)
                    {
                        _curvePoints.Clear();
                        foreach (var point in first.CustomCurvePoints)
                        {
                            bool isFixed = (point.X == 0 && point.Y == 0) || (point.X == 1 && point.Y == 1);
                            _curvePoints.Add(new CurvePoint(point.X, point.Y, isFixed));
                        }
                        UpdateSortedPoints();
                    }
                    else
                    {
                        InitializeDefaultCurve();
                    }
                }
                UpdateCurveMixedVisualState();
            }
            finally
            {
                _isUpdatingFromSelection = false;
            }
        }

        /// <summary>
        /// Checks if selected mappings have different dead zone values.
        /// Returns (mixedInner, mixedOuter).
        /// </summary>
        private (bool mixedInner, bool mixedOuter) GetMixedDeadZoneState(List<KeyMapping> mappings)
        {
            if (mappings.Count <= 1)
                return (false, false);

            var first = mappings[0];
            bool mixedInner = mappings.Any(m => Math.Abs(m.DeadZoneInner - first.DeadZoneInner) > DeadZoneEpsilon);
            bool mixedOuter = mappings.Any(m => Math.Abs(m.DeadZoneOuter - first.DeadZoneOuter) > DeadZoneEpsilon);

            return (mixedInner, mixedOuter);
        }

        /// <summary>
        /// Checks if a dead zone value has changed significantly (beyond epsilon).
        /// </summary>
        private bool HasDeadZoneChanged(int newValue, int oldValue)
        {
            return Math.Abs(newValue - oldValue) >= 1;
        }

        /// <summary>
        /// Checks if selected mappings have different curve configurations.
        /// </summary>
        private bool HasMixedCurves(List<KeyMapping> mappings)
        {
            if (mappings.Count <= 1) return false;

            var first = mappings[0];

            if (mappings.Any(m => m.Curve != first.Curve))
                return true;

            if (first.Curve == ResponseCurveTypes.Custom)
            {
                foreach (var mapping in mappings.Skip(1))
                {
                    if (!AreCustomCurvePointsEqual(first.CustomCurvePoints, mapping.CustomCurvePoints))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Compares two custom curve point lists for equality.
        /// </summary>
        private bool AreCustomCurvePointsEqual(List<(double X, double Y)>? points1, List<(double X, double Y)>? points2)
        {
            if (points1 == null && points2 == null) return true;
            if (points1 == null || points2 == null) return false;
            if (points1.Count != points2.Count) return false;

            for (int i = 0; i < points1.Count; i++)
            {
                if (Math.Abs(points1[i].X - points2[i].X) > 0.001 ||
                    Math.Abs(points1[i].Y - points2[i].Y) > 0.001)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Applies current UI values to all selected mappings.
        /// </summary>
        private void ApplyChangesToSelectedMappings()
        {
            if (_isUpdatingFromSelection || _selectedMappings.Count == 0)
                return;

            foreach (var mapping in _selectedMappings)
            {
                if (!_innerDeadZoneIsMixed)
                {
                    mapping.DeadZoneInner = _innerDeadZone / 100.0;
                }

                if (!_outerDeadZoneIsMixed)
                {
                    mapping.DeadZoneOuter = _outerDeadZone / 100.0;
                }

                if (!_smoothCurveIsMixed)
                {
                    mapping.UseSmoothCurve = _useSmoothCurve;
                }

                if (_curveIsMixed)
                {
                    continue;
                }

                if (_curvePoints.Count > 2)
                {
                    mapping.Curve = ResponseCurveTypes.Custom;
                    mapping.CustomCurvePoints = _curvePoints
                        .Select(p => (p.X, p.Y))
                        .ToList();
                }
                else
                {
                    if (mapping.Curve == ResponseCurveTypes.Custom)
                        mapping.Curve = ResponseCurveTypes.Linear;
                    mapping.CustomCurvePoints = null;
                }

            }

            DebounceSyncToRust();
        }

        /// <summary>
        /// Debounces synchronization to Rust service.
        /// Resets 1-second timer on each call - sync happens after 1 second of inactivity.
        /// </summary>
        private void DebounceSyncToRust()
        {
            _rustSyncDebounceTimer?.Stop();

            if (_rustSyncDebounceTimer == null)
            {
                _rustSyncDebounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(RustSyncDebounceMs)
                };
                _rustSyncDebounceTimer.Tick += RustSyncDebounceTimer_Tick;
            }

            _rustSyncDebounceTimer.Start();
        }

        /// <summary>Debounced sync of valid analog mappings to Rust.</summary>
        private void RustSyncDebounceTimer_Tick(object? sender, object e)
        {
            _rustSyncDebounceTimer?.Stop();

            foreach (var mapping in _selectedMappings)
            {
                if (mapping.IsValid && !mapping.HasWarning && mapping.IsAnalog)
                {
                    _mappingService?.SyncMappingToRust(mapping);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents a control point on the response curve.
    /// Coordinates are normalized (0.0 to 1.0).
    /// </summary>
    public class CurvePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public bool IsFixed { get; set; }
        public ScaleTransform? InnerScaleTransform { get; set; }
        public double LastTargetScale { get; set; } = double.NaN;

        public CurvePoint(double x, double y, bool isFixed = false)
        {
            X = x;
            Y = y;
            IsFixed = isFixed;
        }
    }
}
