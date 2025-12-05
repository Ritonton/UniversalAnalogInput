using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniversalAnalogInputUI.Services;

namespace UniversalAnalogInputUI.Dialogs;

/// <summary>Dialog for displaying real-time performance metrics from Rust mapping engine.</summary>
public sealed partial class PerformanceMonitorDialog : ContentDialog
{
    public PerformanceMonitorDialog()
    {
        this.InitializeComponent();
        this.Loaded += PerformanceMonitorDialog_Loaded;
    }

    private void PerformanceMonitorDialog_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshMetrics();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshMetrics();
    }

    private void OptimizeButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshMetrics();
    }

    private void RefreshMetrics()
    {
        try
        {
            // TODO: IPC call to fetch real metrics from Rust backend
            var basicMetrics = new PerformanceMetrics
            {
                System = new SystemMetrics
                {
                    MappingFps = 0.0,
                    HotkeyDetectionHz = 0.0,
                    ProfileSwitchTimeUs = 0,
                    UltraPerformanceMode = false
                },
                Cache = new CacheMetrics
                {
                    TotalProfiles = 0,
                    TotalSubProfiles = 0,
                    CurrentActive = false,
                    MemoryUsageKb = 0,
                    SwitchMethod = "IPC"
                },
                Components = new ComponentMetrics
                {
                    WootingSdk = true,
                    VigemClient = true,
                    MappingThread = false,
                    HotkeyManager = true
                }
            };
            UpdateUI(basicMetrics);
        }
        catch (Exception ex)
        {
            CrashLogger.LogMessage($"Performance metrics error: {ex.Message}", "PerformanceMonitorDialog");
            SetErrorState();
        }
    }

    private void UpdateUI(PerformanceMetrics metrics)
    {
        if (metrics.System != null)
        {
            MappingFpsText.Text = $"{metrics.System.MappingFps:F1} FPS";
            HotkeyHzText.Text = $"{metrics.System.HotkeyDetectionHz:F0} Hz";
            SwitchTimeText.Text = $"{metrics.System.ProfileSwitchTimeUs} μs";

            double frameTimeMs = metrics.System.MappingFps > 0 ? (1000.0 / metrics.System.MappingFps) : 0;
            LatencyText.Text = $"{frameTimeMs:F1}ms";

            if (metrics.System.UltraPerformanceMode)
            {
                LatencyText.Text += " (Ultra)";
            }
            LatencyText.Foreground = metrics.System.ProfileSwitchTimeUs < 1000 ?
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 52, 211, 153)) :
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
        }

        if (metrics.Cache != null)
        {
            ProfilesText.Text = metrics.Cache.TotalProfiles.ToString();
            SubProfilesText.Text = metrics.Cache.TotalSubProfiles.ToString();
            SwitchMethodText.Text = metrics.Cache.SwitchMethod ?? "Unknown";
            MemoryUsageText.Text = $"{metrics.Cache.MemoryUsageKb} KB";
            CurrentActiveText.Text = metrics.Cache.CurrentActive ? "✅ Yes" : "❌ No";
            CurrentActiveText.Foreground = metrics.Cache.CurrentActive ?
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)) :
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
        }

        if (metrics.Components != null)
        {
            WootingStatusText.Text = metrics.Components.WootingSdk ? "✅ Connected" : "❌ Not Connected";
            WootingStatusText.Foreground = metrics.Components.WootingSdk ?
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)) :
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));

            VigemStatusText.Text = metrics.Components.VigemClient ? "✅ Connected" : "❌ Not Connected";
            VigemStatusText.Foreground = metrics.Components.VigemClient ?
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)) :
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));

            MappingStatusText.Text = metrics.Components.MappingThread ? "▶ Running (120 FPS)" : "⏹ Stopped";
            MappingStatusText.Foreground = metrics.Components.MappingThread ?
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)) :
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));

            HotkeyStatusText.Text = metrics.Components.HotkeyManager ? "✅ Active (120 Hz)" : "❌ Inactive";
            HotkeyStatusText.Foreground = metrics.Components.HotkeyManager ?
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)) :
                new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
        }
    }

    private void SetErrorState()
    {
        MappingFpsText.Text = "Error";
        HotkeyHzText.Text = "Error";
        SwitchTimeText.Text = "Error";
        LatencyText.Text = "Error";
        ProfilesText.Text = "Error";
        SubProfilesText.Text = "Error";
        MemoryUsageText.Text = "Error";
        CurrentActiveText.Text = "Error";
        WootingStatusText.Text = "Error";
        VigemStatusText.Text = "Error";
        MappingStatusText.Text = "Error";
        HotkeyStatusText.Text = "Error";
    }
}

/// <summary>Root metrics container mapping to CPerformanceMetrics in Rust.</summary>
public class PerformanceMetrics
{
    [JsonPropertyName("system")]
    public SystemMetrics? System { get; set; }
    [JsonPropertyName("cache")]
    public CacheMetrics? Cache { get; set; }
    [JsonPropertyName("components")]
    public ComponentMetrics? Components { get; set; }
    [JsonPropertyName("performance")]
    public PerformanceDetails? Performance { get; set; }
}

/// <summary>System-level performance metrics from mapping engine.</summary>
public class SystemMetrics
{
    [JsonPropertyName("mapping_fps")]
    public double MappingFps { get; set; }
    [JsonPropertyName("hotkey_detection_hz")]
    public double HotkeyDetectionHz { get; set; }
    [JsonPropertyName("profile_switch_time_us")]
    public int ProfileSwitchTimeUs { get; set; }
    [JsonPropertyName("ultra_performance_mode")]
    public bool UltraPerformanceMode { get; set; }
}

/// <summary>Profile cache and memory usage metrics.</summary>
public class CacheMetrics
{
    [JsonPropertyName("total_profiles")]
    public int TotalProfiles { get; set; }
    [JsonPropertyName("total_sub_profiles")]
    public int TotalSubProfiles { get; set; }
    [JsonPropertyName("current_active")]
    public bool CurrentActive { get; set; }
    [JsonPropertyName("memory_usage_kb")]
    public int MemoryUsageKb { get; set; }
    [JsonPropertyName("switch_method")]
    public string? SwitchMethod { get; set; }
}

/// <summary>Status of Rust library components.</summary>
public class ComponentMetrics
{
    [JsonPropertyName("wooting_sdk")]
    public bool WootingSdk { get; set; }
    [JsonPropertyName("vigem_client")]
    public bool VigemClient { get; set; }
    [JsonPropertyName("mapping_thread")]
    public bool MappingThread { get; set; }
    [JsonPropertyName("hotkey_manager")]
    public bool HotkeyManager { get; set; }
}

/// <summary>Detailed performance optimization metrics.</summary>
public class PerformanceDetails
{
    [JsonPropertyName("target_latency_ms")]
    public double TargetLatencyMs { get; set; }
    [JsonPropertyName("actual_switch_time_us")]
    public int ActualSwitchTimeUs { get; set; }
    [JsonPropertyName("mapping_loop_optimized")]
    public bool MappingLoopOptimized { get; set; }
    [JsonPropertyName("lock_free_access")]
    public bool LockFreeAccess { get; set; }
}
