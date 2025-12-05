using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Numerics;
using Windows.UI;

namespace UniversalAnalogInputUI.Controls
{
    public sealed partial class XboxGamepadControl : UserControl
    {
        private const double LEFT_STICK_CENTER_X = 209.274;
        private const double LEFT_STICK_CENTER_Y = 168.079;
        private const double RIGHT_STICK_CENTER_X = 526.431;
        private const double RIGHT_STICK_CENTER_Y = 295.43;
        private const double STICK_MAX_RADIUS = 23.0;

        private const float ANIMATION_ROTATION_ANGLE = 30f;
        private const float ANIMATION_LIFT_DISTANCE = 24f;
        private const double ANIMATION_ROTATION_DURATION_MS = 2500.0;
        private const double ANIMATION_LIFT_DURATION_MS = 3000.0;

        private const float CONTROLLER_FINAL_POSITION_Y = 165f;
        private const float CONTROLLER_SLIDE_OFFSET = -30f;
        private const float CONTROLLER_INITIAL_SCALE = 1.03f;
        private const double CONTROLLER_SLIDE_DURATION_MS = 2100.0;

        private static readonly SolidColorBrush COLOR_BACKGROUND = new SolidColorBrush(Color.FromArgb(0xFF, 0x29, 0x2B, 0x2A));
        private static readonly SolidColorBrush COLOR_LETTER = new SolidColorBrush(Color.FromArgb(0xFF, 0xBB, 0xBD, 0xBD));

        private SolidColorBrush? _xboxLogoBrush;
        private SolidColorBrush? _xboxLogoBottomBrush;
        private static bool _hasPlayedStartupAnimation = false;

        public XboxGamepadControl()
        {
            this.InitializeComponent();
            this.Loaded += OnControlLoaded;
        }

        /// <summary>
        /// Called when control finishes loading; initializes Xbox logo appearance
        /// </summary>
        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            InitializeXboxLogo();
        }

        /// <summary>
        /// Configures Xbox logo colors and triggers startup fade-in on first load
        /// </summary>
        private void InitializeXboxLogo()
        {
            _xboxLogoBrush ??= XboxLogoArcTop?.Fill as SolidColorBrush;
            _xboxLogoBottomBrush ??= XboxLogoArcBottom?.Fill as SolidColorBrush;

            if (_xboxLogoBrush == null || _xboxLogoBottomBrush == null)
            {
                return;
            }

            if (_hasPlayedStartupAnimation)
            {
                _xboxLogoBrush.Color = Color.FromArgb(255, 255, 255, 255);
                _xboxLogoBottomBrush.Color = Color.FromArgb(255, 255, 255, 255);
            }
            else
            {
                _xboxLogoBrush.Color = Color.FromArgb(255, 0, 0, 0);
                _xboxLogoBottomBrush.Color = Color.FromArgb(255, 0, 0, 0);
                _hasPlayedStartupAnimation = true;
                PlayXboxLogoStartupAnimation();
            }
        }

        /// <summary>
        /// Fades Xbox logo from black to white after 1-second delay; plays once per session
        /// </summary>
        private async void PlayXboxLogoStartupAnimation()
        {
            if (_xboxLogoBrush == null || _xboxLogoBottomBrush == null) return;

            await System.Threading.Tasks.Task.Delay(1000);

            var colorAnimation = new ColorAnimation
            {
                From = Color.FromArgb(255, 0, 0, 0),
                To = Color.FromArgb(255, 255, 255, 255),
                Duration = TimeSpan.FromMilliseconds(1000),
                EnableDependentAnimation = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            var storyboard = new Storyboard();
            Storyboard.SetTarget(colorAnimation, _xboxLogoBrush);
            Storyboard.SetTargetProperty(colorAnimation, "(SolidColorBrush.Color)");
            storyboard.Children.Add(colorAnimation);

            var bottomAnimation = new ColorAnimation
            {
                From = Color.FromArgb(255, 0, 0, 0),
                To = Color.FromArgb(255, 255, 255, 255),
                Duration = TimeSpan.FromMilliseconds(1000),
                EnableDependentAnimation = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            Storyboard.SetTarget(bottomAnimation, _xboxLogoBottomBrush);
            Storyboard.SetTargetProperty(bottomAnimation, "(SolidColorBrush.Color)");
            storyboard.Children.Add(bottomAnimation);
            storyboard.Begin();
        }

        /// <summary>
        /// Animates triggers overlay with 3D rotation and lift effect; called once at startup
        /// </summary>
        public void PlayTriggersIntroAnimation()
        {
            if (TriggersCanvas == null)
            {
                return;
            }

            if (!EnsureTriggersCanvasSize())
            {
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(TriggersCanvas);
            var compositor = visual.Compositor;

            visual.StopAnimation(nameof(visual.RotationAngleInDegrees));
            visual.StopAnimation(nameof(visual.Offset));

            var centerX = (float)TriggersCanvas.ActualWidth / 2f;
            var centerY = (float)TriggersCanvas.ActualHeight / 2f;
            visual.CenterPoint = new Vector3(centerX, centerY, 0f);
            visual.RotationAxis = Vector3.UnitX;

            TriggersCanvas.Opacity = 1;
            visual.RotationAngleInDegrees = ANIMATION_ROTATION_ANGLE;
            visual.Offset = new Vector3(0f, ANIMATION_LIFT_DISTANCE, 0f);

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.15f, 0.75f), new Vector2(0.2f, 1.0f));

            var rotationAnimation = compositor.CreateScalarKeyFrameAnimation();
            rotationAnimation.Duration = TimeSpan.FromMilliseconds(ANIMATION_ROTATION_DURATION_MS);
            rotationAnimation.InsertKeyFrame(0f, ANIMATION_ROTATION_ANGLE);
            rotationAnimation.InsertKeyFrame(1f, 0f, easing);

            var liftAnimation = compositor.CreateVector3KeyFrameAnimation();
            liftAnimation.Duration = TimeSpan.FromMilliseconds(ANIMATION_LIFT_DURATION_MS);
            liftAnimation.InsertKeyFrame(0f, new Vector3(0f, ANIMATION_LIFT_DISTANCE, 0f));
            liftAnimation.InsertKeyFrame(1f, Vector3.Zero, easing);

            visual.StartAnimation(nameof(visual.RotationAngleInDegrees), rotationAnimation);
            visual.StartAnimation(nameof(visual.Offset), liftAnimation);
        }

        /// <summary>
        /// Verifies TriggersCanvas has valid dimensions for animation; defers to Loaded event if not ready
        /// </summary>
        private bool EnsureTriggersCanvasSize()
        {
            if (TriggersCanvas == null)
            {
                return false;
            }

            if (TriggersCanvas.ActualWidth <= 0 || TriggersCanvas.ActualHeight <= 0)
            {
                TriggersCanvas.Loaded -= OnTriggersCanvasLoaded;
                TriggersCanvas.Loaded += OnTriggersCanvasLoaded;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Triggers animation retry after canvas finishes layout and has valid dimensions
        /// </summary>
        private void OnTriggersCanvasLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Loaded -= OnTriggersCanvasLoaded;
            }

            PlayTriggersIntroAnimation();
        }

        /// <summary>
        /// Slides controller canvas down while shrinking from 110% to 100%; called once at startup
        /// </summary>
        public void PlayControllerIntroAnimation()
        {
            if (ControllerCanvas == null)
            {
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(ControllerCanvas);
            var compositor = visual.Compositor;

            visual.StopAnimation(nameof(visual.Offset));
            visual.StopAnimation(nameof(visual.Scale));

            ControllerCanvas.Opacity = 1;

            var centerX = (float)ControllerCanvas.ActualWidth / 2f;
            var centerY = (float)ControllerCanvas.ActualHeight / 2f;
            visual.CenterPoint = new Vector3(centerX, centerY, 0f);

            var startPosition = CONTROLLER_FINAL_POSITION_Y + CONTROLLER_SLIDE_OFFSET;
            var endPosition = CONTROLLER_FINAL_POSITION_Y;

            visual.Offset = new Vector3(0f, startPosition, 0f);
            visual.Scale = new Vector3(CONTROLLER_INITIAL_SCALE, CONTROLLER_INITIAL_SCALE, 1f);

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.15f, 0.75f), new Vector2(0.2f, 1.0f));

            var slideAnimation = compositor.CreateVector3KeyFrameAnimation();
            slideAnimation.Duration = TimeSpan.FromMilliseconds(CONTROLLER_SLIDE_DURATION_MS);
            slideAnimation.InsertKeyFrame(0f, new Vector3(0f, startPosition, 0f));
            slideAnimation.InsertKeyFrame(1f, new Vector3(0f, endPosition, 0f), easing);

            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(CONTROLLER_SLIDE_DURATION_MS);
            scaleAnimation.InsertKeyFrame(0f, new Vector3(CONTROLLER_INITIAL_SCALE, CONTROLLER_INITIAL_SCALE, 1f));
            scaleAnimation.InsertKeyFrame(1f, Vector3.One, easing);
            visual.StartAnimation(nameof(visual.Offset), slideAnimation);
            visual.StartAnimation(nameof(visual.Scale), scaleAnimation);
        }

        /// <summary>
        /// Updates left joystick visual position
        /// </summary>
        /// <param name="x">Horizontal position (0.0 = left, 0.5 = center, 1.0 = right)</param>
        /// <param name="y">Vertical position (0.0 = top, 0.5 = center, 1.0 = bottom)</param>
        public void SetLeftStick(double x, double y)
        {
            double centeredX = (x - 0.5) * 2.0;
            double centeredY = (y - 0.5) * 2.0;

            double newX = LEFT_STICK_CENTER_X + (centeredX * STICK_MAX_RADIUS);
            double newY = LEFT_STICK_CENTER_Y + (centeredY * STICK_MAX_RADIUS);

            UpdateStickPosition(LeftStickInner, newX, newY);
        }

        /// <summary>
        /// Updates right joystick visual position
        /// </summary>
        /// <param name="x">Horizontal position (0.0 = left, 0.5 = center, 1.0 = right)</param>
        /// <param name="y">Vertical position (0.0 = top, 0.5 = center, 1.0 = bottom)</param>
        public void SetRightStick(double x, double y)
        {
            double centeredX = (x - 0.5) * 2.0;
            double centeredY = (y - 0.5) * 2.0;

            double newX = RIGHT_STICK_CENTER_X + (centeredX * STICK_MAX_RADIUS);
            double newY = RIGHT_STICK_CENTER_Y + (centeredY * STICK_MAX_RADIUS);

            UpdateStickPosition(RightStickInner, newX, newY);
        }

        /// <summary>
        /// Resets both joysticks to center position
        /// </summary>
        public void ResetSticks()
        {
            SetLeftStick(0.5, 0.5);
            SetRightStick(0.5, 0.5);
        }

        /// <summary>
        /// Updates button A visual state
        /// </summary>
        public void SetButtonA(bool pressed)
        {
            UpdateButtonState(ButtonACircle, ButtonALetter, pressed);
        }

        /// <summary>
        /// Updates button B visual state
        /// </summary>
        public void SetButtonB(bool pressed)
        {
            UpdateButtonState(ButtonBCircle, ButtonBLetter, pressed);
        }

        /// <summary>
        /// Updates button X visual state
        /// </summary>
        public void SetButtonX(bool pressed)
        {
            UpdateButtonState(ButtonXCircle, ButtonXLetter, pressed);
        }

        /// <summary>
        /// Updates button Y visual state
        /// </summary>
        public void SetButtonY(bool pressed)
        {
            UpdateButtonState(ButtonYCircle, ButtonYLetter, pressed);
        }

        /// <summary>
        /// Updates View button visual state
        /// </summary>
        public void SetButtonView(bool pressed)
        {
            UpdateButtonStateWithIcons(
                ButtonViewCircle,
                pressed,
                ButtonViewIcon1,
                ButtonViewIcon2
            );
        }

        /// <summary>
        /// Updates Menu button visual state
        /// </summary>
        public void SetButtonMenu(bool pressed)
        {
            UpdateButtonStateWithIcons(
                ButtonMenuCircle,
                pressed,
                ButtonMenuIcon1,
                ButtonMenuIcon2,
                ButtonMenuIcon3
            );
        }

        /// <summary>
        /// Updates D-Pad Up visual state
        /// </summary>
        public void SetDPadUp(bool pressed)
        {
            UpdateDPadState(DPadUpFill, pressed);
        }

        /// <summary>
        /// Updates D-Pad Down visual state
        /// </summary>
        public void SetDPadDown(bool pressed)
        {
            UpdateDPadState(DPadDownFill, pressed);
        }

        /// <summary>
        /// Updates D-Pad Left visual state
        /// </summary>
        public void SetDPadLeft(bool pressed)
        {
            UpdateDPadState(DPadLeftFill, pressed);
        }

        /// <summary>
        /// Updates D-Pad Right visual state
        /// </summary>
        public void SetDPadRight(bool pressed)
        {
            UpdateDPadState(DPadRightFill, pressed);
        }

        /// <summary>
        /// Updates left trigger visual state with analog pressure
        /// </summary>
        /// <param name="value">Trigger pressure from 0.0 to 1.0</param>
        public void SetLeftTrigger(double value)
        {
            UpdateTriggerState(LeftTriggerFill, value);
        }

        /// <summary>
        /// Updates right trigger visual state with analog pressure
        /// </summary>
        /// <param name="value">Trigger pressure from 0.0 to 1.0</param>
        public void SetRightTrigger(double value)
        {
            UpdateTriggerState(RightTriggerFill, value);
        }

        /// <summary>
        /// Updates left shoulder button visual state
        /// </summary>
        public void SetLeftShoulder(bool pressed)
        {
            UpdateShoulderState(LeftShoulderFill, pressed);
        }

        /// <summary>
        /// Updates right shoulder button visual state
        /// </summary>
        public void SetRightShoulder(bool pressed)
        {
            UpdateShoulderState(RightShoulderFill, pressed);
        }

        /// <summary>
        /// Moves joystick visual by updating ellipse geometry center point
        /// </summary>
        private void UpdateStickPosition(Path stickPath, double centerX, double centerY)
        {
            if (stickPath?.Data is Microsoft.UI.Xaml.Media.EllipseGeometry ellipse)
            {
                ellipse.Center = new Windows.Foundation.Point(centerX, centerY);
            }
        }

        /// <summary>
        /// Inverts button colors when pressed (filled circle, dark letter)
        /// </summary>
        private void UpdateButtonState(Path circle, Path letter, bool pressed)
        {
            if (circle == null || letter == null) return;

            if (pressed)
            {
                circle.Fill = COLOR_LETTER;
                letter.Fill = COLOR_BACKGROUND;
            }
            else
            {
                circle.Fill = null;
                letter.Fill = COLOR_LETTER;
            }
        }

        /// <summary>
        /// Inverts button colors when pressed for View/Menu buttons
        /// </summary>
        private void UpdateButtonStateWithIcons(Path circle, bool pressed, params Path[] icons)
        {
            if (circle == null) return;

            if (pressed)
            {
                circle.Fill = COLOR_LETTER;
                foreach (var icon in icons)
                {
                    if (icon != null)
                    {
                        if (icon.Fill != null)
                        {
                            icon.Fill = COLOR_BACKGROUND;
                        }
                        if (icon.Stroke != null)
                        {
                            icon.Stroke = COLOR_BACKGROUND;
                        }
                    }
                }
            }
            else
            {
                circle.Fill = null;
                foreach (var icon in icons)
                {
                    if (icon != null)
                    {
                        if (icon.Fill != null)
                        {
                            icon.Fill = COLOR_LETTER;
                        }
                        if (icon.Stroke != null)
                        {
                            icon.Stroke = COLOR_LETTER;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fills D-Pad direction with color when pressed
        /// </summary>
        private void UpdateDPadState(Path fill, bool pressed)
        {
            if (fill == null) return;

            if (pressed)
            {
                fill.Fill = COLOR_LETTER;
            }
            else
            {
                fill.Fill = new SolidColorBrush(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
            }
        }

        /// <summary>
        /// Adjusts trigger fill opacity based on analog pressure value
        /// </summary>
        private void UpdateTriggerState(Path triggerFill, double value)
        {
            if (triggerFill == null) return;

            value = Math.Clamp(value, 0.0, 1.0);

            if (value > 0.0)
            {
                var triggerColor = new SolidColorBrush(Color.FromArgb(0xFF, 0xBB, 0xBD, 0xBD));
                triggerFill.Fill = triggerColor;
                triggerFill.Opacity = value;
            }
            else
            {
                triggerFill.Fill = new SolidColorBrush(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
                triggerFill.Opacity = 0.0;
            }
        }

        /// <summary>
        /// Fills shoulder button with color when pressed
        /// </summary>
        private void UpdateShoulderState(Path shoulderFill, bool pressed)
        {
            if (shoulderFill == null) return;

            if (pressed)
            {
                shoulderFill.Fill = COLOR_LETTER;
            }
            else
            {
                shoulderFill.Fill = new SolidColorBrush(Color.FromArgb(0x00, 0x00, 0x00, 0x00));
            }
        }
    }
}
