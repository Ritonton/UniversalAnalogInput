using System;

namespace UniversalAnalogInputUI.Models;

/// <summary>Event args fired after profiles are refreshed.</summary>
public class ProfileRefreshedEventArgs : EventArgs
{
    public int ProfileCount { get; init; }
    public int SubProfileCount { get; init; }
    public bool Success { get; init; }
    public string? Message { get; init; }
}

/// <summary>Event args fired when profile or sub-profile selection changes.</summary>
public class ProfileSelectionChangedEventArgs : EventArgs
{
    public GameProfile? Profile { get; init; }
    public SubProfile? SubProfile { get; init; }
    public bool WasActivatedInRust { get; init; }
}
