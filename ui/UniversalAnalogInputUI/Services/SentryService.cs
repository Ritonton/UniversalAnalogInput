using Sentry;
using System;

namespace UniversalAnalogInputUI.Services;

/// <summary>
/// Service for initializing and managing Sentry error monitoring and release health tracking.
/// </summary>
public static class SentryService
{
    private static bool _isInitialized = false;

    /// <summary>
    /// Initialize Sentry monitoring with configuration from environment variables.
    /// Reads UI_SENTRY_DSN and SENTRY_ENVIRONMENT from environment.
    /// </summary>
    /// <returns>True if Sentry was initialized, false if disabled or failed</returns>
    public static bool Initialize()
    {
        if (_isInitialized)
        {
            return true;
        }

        var dsn = Environment.GetEnvironmentVariable("UI_SENTRY_DSN");

        if (string.IsNullOrWhiteSpace(dsn))
        {
#if DEBUG
            Console.WriteLine("[SENTRY] No UI_SENTRY_DSN configured - Sentry disabled");
#endif
            return false;
        }

        var environment = Environment.GetEnvironmentVariable("SENTRY_ENVIRONMENT") ?? "development";

        try
        {
            SentrySdk.Init(options =>
            {
                // Core configuration
                options.Dsn = dsn;
                options.Environment = environment;

                // Release version
                options.Release = $"universal-analog-input@{GetAssemblyVersion()}";

                // Enable Release Health tracking (crash-free sessions/users)
                options.AutoSessionTracking = true;

                // Client application mode (required for WinUI/desktop apps)
                options.IsGlobalModeEnabled = true;

                // Without this, crashed sessions that can't send data won't be detected
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var sentryCache = System.IO.Path.Combine(appDataPath, "UniversalAnalogInput", "SentryCache");
                System.IO.Directory.CreateDirectory(sentryCache);
                options.CacheDirectoryPath = sentryCache;

                // Capture unhandled exceptions automatically
                options.CaptureFailedRequests = false; // Not applicable for desktop apps

                // Privacy: Don't send PII by default
                options.SendDefaultPii = false;

                // Attach stack traces to all events
                options.AttachStacktrace = true;

#if DEBUG
                // Only enable debug mode in development environment to avoid performance impact
                if (environment != "production")
                {
                    options.Debug = true;
                    options.DiagnosticLevel = SentryLevel.Debug;
                }
#endif
            });

            _isInitialized = true;
#if DEBUG
            Console.WriteLine($"[SENTRY] Initialized successfully - Environment: {environment}, Release: {GetAssemblyVersion()}");
#endif
            return true;
        }
        catch (Exception ex)
        {
#if DEBUG
            Console.WriteLine($"[SENTRY] Failed to initialize: {ex.Message}");
#endif
            return false;
        }
    }

    /// <summary>
    /// Capture a critical error to Sentry.
    /// Use this for errors that prevent the application from functioning correctly.
    /// </summary>
    public static void CaptureCriticalError(string context, Exception exception)
    {
        if (!_isInitialized)
        {
            return;
        }

        // Use CaptureException with scope callback
        SentrySdk.CaptureException(exception, scope =>
        {
            scope.SetTag("error_type", "critical");
            scope.SetTag("context", context);
            scope.Level = SentryLevel.Fatal;
        });
    }

    /// <summary>
    /// Check if Sentry is currently initialized and active.
    /// </summary>
    public static bool IsEnabled => _isInitialized && SentrySdk.IsEnabled;

    /// <summary>
    /// Flush all pending events before shutdown.
    /// Called automatically by Sentry on app exit, but can be called manually.
    /// </summary>
    public static void Flush()
    {
        if (_isInitialized)
        {
#if DEBUG
            Console.WriteLine("[SENTRY] Flushing pending events...");
#endif
            SentrySdk.FlushAsync(TimeSpan.FromSeconds(2)).Wait();
#if DEBUG
            Console.WriteLine("[SENTRY] Flush complete");
#endif
        }
    }

    /// <summary>
    /// Close Sentry SDK and end the current session.
    /// </summary>
    public static void Close()
    {
        if (_isInitialized)
        {
#if DEBUG
            Console.WriteLine("[SENTRY] Closing SDK and ending session...");
#endif
            SentrySdk.Close();
            _isInitialized = false;
#if DEBUG
            Console.WriteLine("[SENTRY] SDK closed");
#endif
        }
    }

    private static string GetAssemblyVersion()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.1";
    }
}
