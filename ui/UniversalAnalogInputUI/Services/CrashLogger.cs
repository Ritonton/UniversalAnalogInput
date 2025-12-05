using System;
using System.IO;

namespace UniversalAnalogInputUI.Services
{
    /// <summary>
    /// Service to log crashes and exceptions for debugging.
    /// Writes to %LOCALAPPDATA%/UniversalAnalogInput/crash.log
    /// </summary>
    public static class CrashLogger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UniversalAnalogInput"
        );

        private static readonly string LogFile = Path.Combine(LogDirectory, "crash.log");
        private static readonly object _logLock = new object();

        static CrashLogger()
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Log an exception with context information
        /// </summary>
        public static void LogException(Exception ex, string context = "")
        {
            try
            {
                lock (_logLock)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logMessage = $"""
                        ===== CRASH REPORT =====
                        Timestamp: {timestamp}
                        Context: {context}
                        Exception Type: {ex.GetType().Name}
                        Message: {ex.Message}
                        Stack Trace:
                        {ex.StackTrace}

                        Inner Exception: {ex.InnerException?.Message ?? "None"}

                        """;

                    File.AppendAllText(LogFile, logMessage);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Log a simple message
        /// </summary>
        public static void LogMessage(string message, string context = "")
        {
            try
            {
                lock (_logLock)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logMessage = $"[{timestamp}] {context}: {message}\n";

                    File.AppendAllText(LogFile, logMessage);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Get the full path to the crash log file
        /// </summary>
        public static string GetLogFilePath() => LogFile;

        /// <summary>
        /// Clear the crash log
        /// </summary>
        public static void ClearLog()
        {
            try
            {
                lock (_logLock)
                {
                    if (File.Exists(LogFile))
                    {
                        File.Delete(LogFile);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
