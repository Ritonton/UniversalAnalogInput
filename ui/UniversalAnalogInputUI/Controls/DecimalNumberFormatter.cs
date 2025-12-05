using System;
using Windows.Globalization.NumberFormatting;

namespace UniversalAnalogInputUI.Controls
{
    /// <summary>Custom number formatter for NumberBox with fixed decimal precision.</summary>
    public sealed class DecimalNumberFormatter : INumberFormatter2, INumberParser
    {
        private readonly DecimalFormatter _formatter;
        private int _fractionDigits;

        public DecimalNumberFormatter()
        {
            _fractionDigits = 3;
            _formatter = new DecimalFormatter();
            _formatter.IntegerDigits = 1;
            _formatter.FractionDigits = _fractionDigits;
        }

        public int FractionDigits
        {
            get => _fractionDigits;
            set
            {
                _fractionDigits = value;
                _formatter.FractionDigits = value;
            }
        }

        public string FormatInt(long value)
        {
            return _formatter.FormatInt(value);
        }

        public string FormatUInt(ulong value)
        {
            return _formatter.FormatUInt(value);
        }

        public string FormatDouble(double value)
        {
            double roundedValue = Math.Round(value, _fractionDigits);
            return _formatter.FormatDouble(roundedValue);
        }

        public long? ParseInt(string text)
        {
            return _formatter.ParseInt(text);
        }

        public ulong? ParseUInt(string text)
        {
            return _formatter.ParseUInt(text);
        }

        public double? ParseDouble(string text)
        {
            return _formatter.ParseDouble(text);
        }
    }
}
