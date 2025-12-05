using System.Text.Json.Serialization;

namespace UniversalAnalogInputUI.Models;

/// <summary>Performance metrics returned from the Rust system API.</summary>
public class PerformanceMetrics
{
    [JsonPropertyName("system")]
    public SystemMetrics System { get; set; } = new();

    [JsonPropertyName("components")]
    public ComponentStatus Components { get; set; } = new();

    [JsonPropertyName("cache")]
    public CacheMetrics Cache { get; set; } = new();
}

public class SystemMetrics
{
    /// <summary>Frames per second of the mapping loop.</summary>
    [JsonPropertyName("mapping_fps")]
    public double MappingFps { get; set; }

    /// <summary>Hotkey detection frequency.</summary>
    [JsonPropertyName("hotkey_detection_hz")]
    public double HotkeyDetectionHz { get; set; }

    /// <summary>Profile switch latency in microseconds.</summary>
    [JsonPropertyName("profile_switch_time_us")]
    public uint ProfileSwitchTimeUs { get; set; }

    /// <summary>Indicates ultra-performance mode is active.</summary>
    [JsonPropertyName("ultra_performance_mode")]
    public bool UltraPerformanceMode { get; set; }
}

public class ComponentStatus
{
    /// <summary>Status of the Wooting SDK integration.</summary>
    [JsonPropertyName("wooting_sdk")]
    public ComponentState WootingSdk { get; set; } = new();

    /// <summary>Status of the ViGEm client.</summary>
    [JsonPropertyName("vigem_client")]
    public ComponentState VigemClient { get; set; } = new();

    /// <summary>Indicates whether the mapping thread is running.</summary>
    [JsonPropertyName("mapping_thread")]
    public bool MappingThread { get; set; }

    /// <summary>Indicates whether the hotkey manager is running.</summary>
    [JsonPropertyName("hotkey_manager")]
    public bool HotkeyManager { get; set; }
}

public class ComponentState
{
    /// <summary>Health status for a component.</summary>
    [JsonPropertyName("status")]
    public ComponentStatusType Status { get; set; }

    /// <summary>Error details when unhealthy.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    public bool IsHealthy => Status == ComponentStatusType.Ok;

    public string? ErrorMessage => Error;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComponentStatusType
{
    Ok,
    Missing,
    NotInitialized
}

public class CacheMetrics
{
    /// <summary>Total loaded profiles.</summary>
    [JsonPropertyName("total_profiles")]
    public uint TotalProfiles { get; set; }

    /// <summary>Total loaded sub-profiles.</summary>
    [JsonPropertyName("total_sub_profiles")]
    public uint TotalSubProfiles { get; set; }

    /// <summary>Indicates whether a profile is currently active.</summary>
    [JsonPropertyName("current_active")]
    public bool CurrentActive { get; set; }

    /// <summary>Memory usage of the mapping cache in kilobytes.</summary>
    [JsonPropertyName("memory_usage_kb")]
    public uint MemoryUsageKb { get; set; }

    /// <summary>Current switch strategy or method name.</summary>
    [JsonPropertyName("switch_method")]
    public string SwitchMethod { get; set; } = string.Empty;
}
