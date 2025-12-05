using System;

namespace UniversalAnalogInputUI.Models;

/// <summary>Tracks a mapping pending conflict resolution before syncing to Rust.</summary>
internal class PendingMappingState
{
    /// <summary>Profile owning the pending mapping.</summary>
    public Guid ProfileId { get; set; }

    /// <summary>Sub-profile owning the pending mapping.</summary>
    public Guid SubProfileId { get; set; }

    /// <summary>User-selected key name (may conflict).</summary>
    public string KeyName { get; set; } = "";

    /// <summary>User-selected gamepad control.</summary>
    public string GamepadControl { get; set; } = "";

    /// <summary>User-selected response curve.</summary>
    public string ResponseCurve { get; set; } = "";

    /// <summary>Original Rust key name used for deletion when conflicts arise.</summary>
    public string OriginalKeyInRust { get; set; } = "";

    /// <summary>Inner dead zone value.</summary>
    public double DeadZoneInner { get; set; }

    /// <summary>Outer dead zone value.</summary>
    public double DeadZoneOuter { get; set; }
}
