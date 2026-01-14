/// Retrieve the crash log path if the logger has been initialized.
pub fn get_crash_log_path() -> Option<String> {
    crate::logging::get_crash_log_path()
}

/// Clear the current crash log file.
pub fn clear_crash_log() -> Result<(), String> {
    crate::logging::clear_crash_log().map_err(|e| e.to_string())
}

/// Forward critical errors from higher layers into the crash logger.
pub fn log_critical_error(context: &str, error: &str) {
    crate::logging::log_critical_error(context, error);
}

/// Initialize Sentry monitoring with DSN and environment.
/// Returns true if Sentry was successfully initialized, false otherwise.
pub fn init_sentry(dsn: Option<&str>, environment: Option<&str>) -> bool {
    crate::logging::init_sentry(dsn, environment)
}

/// Check if Sentry is currently enabled and monitoring.
pub fn is_sentry_enabled() -> bool {
    crate::logging::is_sentry_enabled()
}

/// Capture a critical error to Sentry (blocking startup or core functionality).
/// Use this ONLY for errors that prevent the application from functioning.
pub fn capture_critical_error(context: &str, error: &str) {
    crate::logging::capture_critical_error(context, error);
}
