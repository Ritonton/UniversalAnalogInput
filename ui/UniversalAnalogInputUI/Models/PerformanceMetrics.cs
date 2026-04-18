using System.Text.Json.Serialization;

namespace UniversalAnalogInputUI.Models;

/// <summary>Component health returned by GetPerformanceMetrics, used for dependency checking.</summary>
public class PerformanceMetrics
{
    [JsonPropertyName("components")]
    public ComponentStatus Components { get; set; } = new();
}

public class ComponentStatus
{
    [JsonPropertyName("wooting_sdk")]
    public ComponentState WootingSdk { get; set; } = new();

    [JsonPropertyName("vigem_client")]
    public ComponentState VigemClient { get; set; } = new();

    [JsonPropertyName("mapping_thread")]
    public bool MappingThread { get; set; }

    [JsonPropertyName("hotkey_manager")]
    public bool HotkeyManager { get; set; }
}

public class ComponentState
{
    [JsonPropertyName("status")]
    public ComponentStatusType Status { get; set; }

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
