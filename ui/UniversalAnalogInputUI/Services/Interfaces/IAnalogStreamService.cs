using System;

namespace UniversalAnalogInputUI.Services.Interfaces;

public interface IAnalogStreamService
{
    event EventHandler<AnalogSnapshot>? DataUpdated;

    // Must be called on the UI thread.
    void Start();
    void Stop();
}

public sealed class AnalogKeyEntry
{
    public ushort KeyCode { get; init; }
    public string KeyName { get; init; } = "";
    public float Value { get; init; }
}

/// <summary>Snapshot of pressed keys sorted descending by value.</summary>
public sealed class AnalogSnapshot
{
    public static readonly AnalogSnapshot Empty = new();

    // Rust set stream_active=0, distinct from an empty keys array.
    public static readonly AnalogSnapshot StreamStopped = new() { IsStreamStopped = true };

    public AnalogKeyEntry[] Keys { get; init; } = Array.Empty<AnalogKeyEntry>();
    public bool IsStreamStopped { get; private init; }
    public AnalogKeyEntry? TopKey => Keys.Length > 0 ? Keys[0] : null;
}
