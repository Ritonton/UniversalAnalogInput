using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using UniversalAnalogInputUI.Services;
using UniversalAnalogInputUI.Services.Interfaces;
using Windows.Gaming.Input;

namespace UniversalAnalogInputUI.Views
{
    /// <summary>Visual gamepad tester that polls live input and updates UI indicators.</summary>
    public sealed partial class TesterPage : Page
    {
        private DispatcherQueueTimer? _pollTimer;
        private readonly IGamepadService _gamepadService;
        private bool _isPageActive = false;

        public TesterPage()
        {
            this.InitializeComponent();
            _gamepadService = (App.Services.GetService(typeof(IGamepadService)) as IGamepadService)!;

            this.Unloaded += TesterPage_Unloaded;
        }

        private void TesterPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            GamepadVisual?.PlayTriggersIntroAnimation();
            GamepadVisual?.PlayControllerIntroAnimation();
        }

        private void TesterPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _isPageActive = false;

            if (_pollTimer != null)
            {
                _pollTimer.Tick -= PollTimer_Tick;
                _pollTimer.Stop();
                _pollTimer = null;
            }

            this.Unloaded -= TesterPage_Unloaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _isPageActive = true;

            _pollTimer = DispatcherQueue.CreateTimer();
            _pollTimer.Interval = TimeSpan.FromMilliseconds(16);
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _isPageActive = false;

            if (_pollTimer != null)
            {
                _pollTimer.Tick -= PollTimer_Tick;
                _pollTimer.Stop();
                _pollTimer = null;
            }
        }

        private void PollTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            if (!_isPageActive) return;

            try
            {
                var gamepad = _gamepadService.GetGamepad();
                if (gamepad == null)
                {
                    ResetAllValues();
                    return;
                }

                var reading = gamepad.GetCurrentReading();

                if (GamepadVisual != null)
                {
                    GamepadVisual.SetButtonA(reading.Buttons.HasFlag(GamepadButtons.A));
                    GamepadVisual.SetButtonB(reading.Buttons.HasFlag(GamepadButtons.B));
                    GamepadVisual.SetButtonX(reading.Buttons.HasFlag(GamepadButtons.X));
                    GamepadVisual.SetButtonY(reading.Buttons.HasFlag(GamepadButtons.Y));
                    GamepadVisual.SetButtonView(reading.Buttons.HasFlag(GamepadButtons.View));
                    GamepadVisual.SetButtonMenu(reading.Buttons.HasFlag(GamepadButtons.Menu));

                    GamepadVisual.SetDPadUp(reading.Buttons.HasFlag(GamepadButtons.DPadUp));
                    GamepadVisual.SetDPadDown(reading.Buttons.HasFlag(GamepadButtons.DPadDown));
                    GamepadVisual.SetDPadLeft(reading.Buttons.HasFlag(GamepadButtons.DPadLeft));
                    GamepadVisual.SetDPadRight(reading.Buttons.HasFlag(GamepadButtons.DPadRight));

                    GamepadVisual.SetLeftShoulder(reading.Buttons.HasFlag(GamepadButtons.LeftShoulder));
                    GamepadVisual.SetRightShoulder(reading.Buttons.HasFlag(GamepadButtons.RightShoulder));

                    double leftX = (reading.LeftThumbstickX + 1.0) / 2.0;
                    double leftY = (1.0 - reading.LeftThumbstickY) / 2.0;
                    double rightX = (reading.RightThumbstickX + 1.0) / 2.0;
                    double rightY = (1.0 - reading.RightThumbstickY) / 2.0;

                    GamepadVisual.SetLeftStick(leftX, leftY);
                    GamepadVisual.SetRightStick(rightX, rightY);

                    GamepadVisual.SetLeftTrigger(reading.LeftTrigger);
                    GamepadVisual.SetRightTrigger(reading.RightTrigger);
                }

                UpdateTriggerIndicator(
                    reading.LeftTrigger,
                    LeftTriggerBar
                );

                UpdateTriggerIndicator(
                    reading.RightTrigger,
                    RightTriggerBar
                );

                UpdateStickIndicator(
                    reading.LeftThumbstickX,
                    reading.LeftThumbstickY,
                    LeftStickPoint,
                    LeftStickLine,
                    LeftStickXValue,
                    LeftStickYValue
                );

                UpdateStickIndicator(
                    reading.RightThumbstickX,
                    reading.RightThumbstickY,
                    RightStickPoint,
                    RightStickLine,
                    RightStickXValue,
                    RightStickYValue
                );
            }
            catch (Exception ex)
            {
                Services.CrashLogger.LogException(ex, "TesterPage.PollTimer_Tick");

                if (_isPageActive)
                {
                    try { ResetAllValues(); } catch { /* Page is being destroyed */ }
                }
            }
        }

        private void ResetAllValues()
        {
            if (!_isPageActive) return;

            if (GamepadVisual != null) GamepadVisual.ResetSticks();

            UpdateStickIndicator(0, 0, LeftStickPoint, LeftStickLine, LeftStickXValue, LeftStickYValue);
            UpdateStickIndicator(0, 0, RightStickPoint, RightStickLine, RightStickXValue, RightStickYValue);

            UpdateTriggerIndicator(0, LeftTriggerBar);
            UpdateTriggerIndicator(0, RightTriggerBar);
        }

        /// <summary>
        /// Updates a circular stick indicator with position and line
        /// </summary>
        /// <param name="x">X value from -1.0 to 1.0</param>
        /// <param name="y">Y value from -1.0 to 1.0</param>
        /// <param name="point">The moving point ellipse</param>
        /// <param name="line">The direction line</param>
        /// <param name="xValueText">TextBlock for X percentage</param>
        /// <param name="yValueText">TextBlock for Y percentage</param>
        private void UpdateStickIndicator(
            double x,
            double y,
            Microsoft.UI.Xaml.Shapes.Ellipse point,
            Microsoft.UI.Xaml.Shapes.Line line,
            Microsoft.UI.Xaml.Controls.TextBlock xValueText,
            Microsoft.UI.Xaml.Controls.TextBlock yValueText)
        {
            if (point == null || line == null || xValueText == null || yValueText == null) return;

            const double centerX = 85;
            const double centerY = 85;
            const double maxRadius = 77;

            double posX = centerX + (x * maxRadius);
            double posY = centerY - (y * maxRadius);

            Microsoft.UI.Xaml.Controls.Canvas.SetLeft(point, posX - 8);
            Microsoft.UI.Xaml.Controls.Canvas.SetTop(point, posY - 8);

            line.X2 = posX;
            line.Y2 = posY;

            xValueText.Text = $"{(x * 100):F0}%";
            yValueText.Text = $"{(y * 100):F0}%";
        }

        /// <summary>
        /// Updates a trigger indicator bar and percentage text.
        /// </summary>
        /// <param name="value">Trigger value from 0.0 to 1.0.</param>
        /// <param name="bar">The rectangle representing the filled trigger amount.</param>
        /// <param name="valueText">Text block displaying the percentage.</param>
        private void UpdateTriggerIndicator(
            double value,
            Microsoft.UI.Xaml.Shapes.Rectangle bar)
        {
            if (bar == null) return;

            const double canvasPadding = 2.0;
            const double maxFillHeight = 166.0;

            double clampedValue = value;
            if (clampedValue < 0) clampedValue = 0;
            if (clampedValue > 1) clampedValue = 1;

            double fillHeight = clampedValue * maxFillHeight;
            bar.Height = fillHeight;

            Microsoft.UI.Xaml.Controls.Canvas.SetTop(
                bar,
                canvasPadding + (maxFillHeight - fillHeight)
            );

        }
    }
}
