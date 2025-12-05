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
