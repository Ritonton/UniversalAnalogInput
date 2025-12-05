using System;
using System.Text;

namespace UniversalAnalogInputUI.Interop;

/// <summary>IPC contract structs mirroring the Rust types</summary>
public struct CCurvePoint
{
    public float X;
    public float Y;
}

public struct CMappingInfo
{
    public byte[] KeyName;
    public byte[] GamepadControl;
    public byte[] ResponseCurve;
    public float DeadZoneInner;
    public float DeadZoneOuter;
    public byte UseSmoothCurve;
    public byte CustomPointCount;
    public CCurvePoint[] CustomPoints;
    public ulong CreatedAt;

    public CMappingInfo()
    {
        KeyName = new byte[32];
        GamepadControl = new byte[32];
        ResponseCurve = new byte[32];
        CustomPoints = new CCurvePoint[16];
    }
}

public struct CProfileMetadata
{
    public byte[] Id;
    public byte[] Name;
    public byte[] Description;
    public uint SubProfileCount;
    public ulong CreatedAt;
    public ulong ModifiedAt;
    public byte[] Hotkey;

    public CProfileMetadata()
    {
        Id = new byte[16];
        Name = new byte[64];
        Description = new byte[256];
        Hotkey = new byte[32];
    }
}

public struct CSubProfileMetadata
{
    public byte[] Id;
    public byte[] ParentProfileId;
    public byte[] Name;
    public byte[] Description;
    public byte[] Hotkey;
    public ulong CreatedAt;
    public ulong ModifiedAt;

    public CSubProfileMetadata()
    {
        Id = new byte[16];
        ParentProfileId = new byte[16];
        Name = new byte[64];
        Description = new byte[256];
        Hotkey = new byte[32];
    }
}

public struct CUiEvent
{
    public uint EventType;
    public byte[] ProfileId;
    public byte[] SubProfileId;

    public CUiEvent()
    {
        ProfileId = new byte[16];
        SubProfileId = new byte[16];
    }
}

public static class InteropEncoding
{
    /// <summary>Decodes a UTF-8 fixed buffer up to the first null terminator.</summary>
    public static string DecodeUtf8(byte[]? buffer)
    {
        if (buffer == null || buffer.Length == 0)
            return string.Empty;

        int terminator = Array.IndexOf(buffer, (byte)0);
        if (terminator >= 0)
        {
            return Encoding.UTF8.GetString(buffer, 0, terminator);
        }

        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>Encodes a string to a fixed-size UTF-8 buffer, reserving space for a terminator.</summary>
    public static byte[] EncodeUtf8(string? value, int fixedSize)
    {
        var output = new byte[fixedSize];
        if (string.IsNullOrEmpty(value))
            return output;

        var bytes = Encoding.UTF8.GetBytes(value);
        Array.Copy(bytes, output, Math.Min(bytes.Length, fixedSize - 1));
        return output;
    }

    /// <summary>Parses a Guid from a 16-byte buffer.</summary>
    public static Guid GuidFromBuffer(byte[]? buffer)
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

    /// <summary>Converts a Guid to a 16-byte buffer.</summary>
    public static byte[] GuidToBuffer(Guid guid) => guid.ToByteArray();
}
