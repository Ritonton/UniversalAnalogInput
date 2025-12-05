using System;
using System.Text;

namespace UniversalAnalogInputUI.Helpers;

/// <summary>Converts data between Rust and C# types.</summary>
public static class RustDataConverter
{
    /// <summary>Converts null-terminated UTF-8 byte array from Rust to C# string.</summary>
    public static string DecodeUtf8(byte[]? buffer)
    {
        if (buffer == null || buffer.Length == 0) return string.Empty;
        var text = Encoding.UTF8.GetString(buffer);
        int terminator = text.IndexOf('\0');
        return terminator >= 0 ? text[..terminator] : text;
    }

    /// <summary>Converts 16-byte GUID buffer from Rust to C# Guid.</summary>
    public static Guid DecodeGuid(byte[]? buffer)
    {
        if (buffer == null || buffer.Length != 16)
            return Guid.Empty;

        try
        {
            return new Guid(buffer);
        }
        catch
        {
            return Guid.Empty;
        }
    }

    /// <summary>Converts Unix timestamp to DateTime.</summary>
    public static DateTime UnixTimestampToDateTime(ulong unixTimestamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds((long)unixTimestamp).DateTime;
    }
}
