using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace UniversalAnalogInputUI.Models;

public class KeyMapping : INotifyPropertyChanged
{
    private string _keyName = "";
    private string _gamepadControl = "";
    internal string _originalKeyName = "";
    internal string _originalGamepadControl = "";
    private string _curve = ResponseCurveTypes.Linear;
    private double _deadZoneInner = 0.05;
    private double _deadZoneOuter = 0.95;
    private string _curveDescription = "";
    private DateTime _createdAt = DateTime.MinValue;
    private List<(double X, double Y)>? _customCurvePoints = null;
    private bool _useSmoothCurve = false;

    private static readonly SolidColorBrush ValidBrush = new(Color.FromArgb(255, 22, 160, 133));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromArgb(255, 255, 193, 7));
    private static readonly SolidColorBrush ErrorBrush = new(Color.FromArgb(255, 220, 38, 38));
    private static readonly SolidColorBrush NeutralBrush = new(Color.FromArgb(255, 142, 146, 151));

    public string KeyName
    {
        get => _keyName;
        set
        {
            if (SetProperty(ref _keyName, value))
            {
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(ValidationState));
                OnPropertyChanged(nameof(ValidationGlyph));
                OnPropertyChanged(nameof(ValidationBrush));
                OnPropertyChanged(nameof(ValidationOpacity));
            }
        }
    }

    public string GamepadControl
    {
        get => _gamepadControl;
        set
        {
            if (SetProperty(ref _gamepadControl, value))
            {
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(ValidationState));
                OnPropertyChanged(nameof(ValidationGlyph));
                OnPropertyChanged(nameof(ValidationBrush));
                OnPropertyChanged(nameof(ValidationOpacity));
            }
        }
    }

    public string Curve
    {
        get => _curve;
        set
        {
            if (SetProperty(ref _curve, value))
            {
                UpdateCurveDescription();
            }
        }
    }

    public double DeadZoneInner
    {
        get => _deadZoneInner;
        set
        {
            if (SetProperty(ref _deadZoneInner, value))
            {
                UpdateCurveDescription();
            }
        }
    }

    public double DeadZoneOuter
    {
        get => _deadZoneOuter;
        set
        {
            if (SetProperty(ref _deadZoneOuter, value))
            {
                UpdateCurveDescription();
            }
        }
    }

    public string CurveDescription
    {
        get => _curveDescription;
        private set => SetProperty(ref _curveDescription, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public List<(double X, double Y)>? CustomCurvePoints
    {
        get => _customCurvePoints;
        set => SetProperty(ref _customCurvePoints, value);
    }

    public bool UseSmoothCurve
    {
        get => _useSmoothCurve;
        set => SetProperty(ref _useSmoothCurve, value);
    }

    private bool _hasWarning = false;

    public bool IsValid => !string.IsNullOrEmpty(KeyName) && !string.IsNullOrEmpty(GamepadControl);

    /// <summary>
    /// Returns true if this mapping is analog (sticks/triggers) and supports curves/dead zones.
    /// Digital mappings (buttons) should not use curves or dead zones.
    /// </summary>
    public bool IsAnalog
    {
        get
        {
            if (string.IsNullOrEmpty(GamepadControl))
                return false;

            return GamepadControl.Contains("Stick", StringComparison.OrdinalIgnoreCase) ||
                   GamepadControl.Contains("Trigger", StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool HasWarning
    {
        get => _hasWarning;
        set
        {
            if (SetProperty(ref _hasWarning, value))
            {
                OnPropertyChanged(nameof(ValidationState));
                OnPropertyChanged(nameof(ValidationGlyph));
                OnPropertyChanged(nameof(ValidationBrush));
                OnPropertyChanged(nameof(ValidationOpacity));
            }
        }
    }

    public ValidationState ValidationState
    {
        get
        {
            if (!IsValid) return ValidationState.Invalid;
            if (HasWarning) return ValidationState.Warning;
            return ValidationState.Valid;
        }
    }

    public string ValidationGlyph
    {
        get
        {
            if (HasWarning)
            {
                return "\uE7BA";
            }

            if (!IsValid)
            {
                return "\uE783";
            }

            return "\uE73E";
        }
    }

    public Brush ValidationBrush
    {
        get
        {
            if (HasWarning)
            {
                return WarningBrush;
            }

            if (!IsValid)
            {
                return ErrorBrush;
            }

            return ValidBrush;
        }
    }

    public double ValidationOpacity => 1d;

    public KeyMapping()
    {
        UpdateCurveDescription();
    }

    internal void MarkAsOriginal()
    {
        _originalKeyName = _keyName;
        _originalGamepadControl = _gamepadControl;
    }

    internal bool HasBeenModified => _originalKeyName != _keyName || _originalGamepadControl != _gamepadControl;

    private void UpdateCurveDescription()
    {
        var deadZoneDesc = DeadZoneInner > 0.01 || DeadZoneOuter < 0.99
            ? $"DZ:{DeadZoneInner:P0}-{DeadZoneOuter:P0}"
            : "No DZ";
        CurveDescription = $"{Curve}, {deadZoneDesc}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public static class ResponseCurveTypes
{
    public const string Linear = "Linear";
    public const string Custom = "Custom";

    public static readonly string[] AllTypes = { Linear, Custom };
}

public enum ValidationState
{
    Invalid,
    Warning,
    Valid
}

