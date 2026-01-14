use log::{error, info, LevelFilter};
use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::PathBuf;
use std::sync::Mutex;

static CRASH_LOG_PATH: Mutex<Option<PathBuf>> = Mutex::new(None);
static SENTRY_GUARD: Mutex<Option<sentry::ClientInitGuard>> = Mutex::new(None);

/// Initialize Sentry monitoring with optional DSN.
/// If DSN is None or empty, Sentry will be disabled.
pub fn init_sentry(dsn: Option<&str>, environment: Option<&str>) -> bool {
    let dsn_value = match dsn {
        Some(d) if !d.is_empty() => d,
        _ => {
            info!("[SENTRY] No DSN provided - Sentry disabled");
            return false;
        }
    };

    let env_cow = environment.map(|e| std::borrow::Cow::Owned(e.to_string()));

    let guard = sentry::init((
        dsn_value,
        sentry::ClientOptions {
            release: sentry::release_name!(),
            environment: env_cow,
            attach_stacktrace: true,
            send_default_pii: false,
            // Enable Release Health tracking (crash-free sessions/users, adoption)
            auto_session_tracking: true,
            session_mode: sentry::SessionMode::Application,
            ..Default::default()
        },
    ));

    if guard.is_enabled() {
        let mut guard_lock = SENTRY_GUARD.lock().unwrap();
        *guard_lock = Some(guard);
        info!("[SENTRY] Initialized successfully - Environment: {:?}", environment);
        true
    } else {
        info!("[SENTRY] Failed to initialize");
        false
    }
}

/// Check if Sentry is currently enabled.
pub fn is_sentry_enabled() -> bool {
    let guard = SENTRY_GUARD.lock().unwrap();
    guard.as_ref().map_or(false, |g| g.is_enabled())
}

/// Shutdown Sentry and end the current session.
/// This ensures the session is marked as "ended" normally and data is flushed.
pub fn shutdown_sentry() {
    let mut guard_lock = SENTRY_GUARD.lock().unwrap();
    if let Some(guard) = guard_lock.take() {
        info!("[SENTRY] Shutting down - ending session and flushing events");
        // Dropping the guard will:
        // 1. End the current session
        // 2. Flush all pending events
        // 3. Close the HTTP connection
        drop(guard);
        info!("[SENTRY] Shutdown complete");
    }
}

/// Capture a critical error to Sentry (blocking startup or core functionality).
/// Use this ONLY for errors that prevent the application from functioning.
pub fn capture_critical_error(context: &str, error: &str) {
    if is_sentry_enabled() {
        sentry::with_scope(
            |scope| {
                scope.set_tag("error_type", "critical");
                scope.set_tag("context", context);
                scope.set_level(Some(sentry::Level::Fatal));
            },
            || {
                sentry::capture_message(
                    &format!("[CRITICAL] {}: {}", context, error),
                    sentry::Level::Fatal,
                );
            },
        );
    }
}

/// Initialize env_logger with Sentry integration.
pub fn init_logger() {
    let mut builder = env_logger::Builder::from_default_env();

    if std::env::var("RUST_LOG").is_err() {
        builder.filter_level(LevelFilter::Warn);
    }

    builder.format(|buf, record| {
        use std::io::Write;
        writeln!(
            buf,
            "[{}] {}: {}",
            record.level(),
            record.target(),
            record.args()
        )
    });

    let _ = builder.try_init();
}

/// Initialize crash logging and panic hook.
pub fn init_crash_logger() {
    let log_dir = if let Some(local_data) = dirs::data_local_dir() {
        local_data.join("UniversalAnalogInput")
    } else {
        PathBuf::from(".")
    };

    let _ = fs::create_dir_all(&log_dir);

    let log_path = log_dir.join("rust_crash.log");

    {
        let mut path_guard = CRASH_LOG_PATH.lock().unwrap();
        *path_guard = Some(log_path.clone());
    }

    std::panic::set_hook(Box::new(move |panic_info| {
        let crash_msg = format_panic_message(panic_info);

        if is_sentry_enabled() {
            sentry::capture_message(&crash_msg, sentry::Level::Fatal);
        }

        if let Err(e) = write_crash_log(&crash_msg) {
            error!("[CRASH LOGGER] Failed to write crash log: {}", e);
        }

        error!("\n{}", crash_msg);
    }));

    info!("[CRASH LOGGER] Initialized - Log: {:?}", log_path);
}

/// Format panic message with timestamp, location, payload, and backtrace.
fn format_panic_message(panic_info: &std::panic::PanicHookInfo) -> String {
    let timestamp = chrono::Local::now().format("%Y-%m-%d %H:%M:%S%.3f");

    let payload = if let Some(s) = panic_info.payload().downcast_ref::<&str>() {
        s.to_string()
    } else if let Some(s) = panic_info.payload().downcast_ref::<String>() {
        s.clone()
    } else {
        "Unknown panic payload".to_string()
    };

    let location = if let Some(location) = panic_info.location() {
        format!(
            "{}:{}:{}",
            location.file(),
            location.line(),
            location.column()
        )
    } else {
        "Unknown location".to_string()
    };

    format!(
        r#"
===== RUST PANIC =====
Timestamp: {}
Location: {}
Message: {}
Thread: {:?}

Backtrace:
{:?}

"#,
        timestamp,
        location,
        payload,
        std::thread::current().name().unwrap_or("unnamed"),
        std::backtrace::Backtrace::capture()
    )
}

/// Append a crash message to the crash log.
fn write_crash_log(message: &str) -> std::io::Result<()> {
    let path_guard = CRASH_LOG_PATH.lock().unwrap();
    if let Some(ref log_path) = *path_guard {
        let mut file = OpenOptions::new()
            .create(true)
            .append(true)
            .open(log_path)?;

        writeln!(file, "{}", message)?;
    }
    Ok(())
}

/// Log a critical error to the crash log without panicking.
pub fn log_critical_error(context: &str, error: &str) {
    let timestamp = chrono::Local::now().format("%Y-%m-%d %H:%M:%S%.3f");
    let message = format!(
        r#"
===== CRITICAL ERROR =====
Timestamp: {}
Context: {}
Error: {}
Thread: {:?}

"#,
        timestamp,
        context,
        error,
        std::thread::current().name().unwrap_or("unnamed")
    );

    if is_sentry_enabled() {
        sentry::with_scope(
            |scope| {
                scope.set_tag("context", context);
                scope.set_level(Some(sentry::Level::Error));
            },
            || {
                sentry::capture_message(error, sentry::Level::Error);
            },
        );
    }

    if let Err(e) = write_crash_log(&message) {
        error!("[CRASH LOGGER] Failed to write critical error: {}", e);
    }

    error!("{}", message);
}

/// Get the crash log file path
pub fn get_crash_log_path() -> Option<String> {
    let path_guard = CRASH_LOG_PATH.lock().unwrap();
    path_guard.as_ref().map(|p| p.to_string_lossy().to_string())
}

/// Clear the crash log file
pub fn clear_crash_log() -> std::io::Result<()> {
    let path_guard = CRASH_LOG_PATH.lock().unwrap();
    if let Some(ref log_path) = *path_guard {
        if log_path.exists() {
            fs::remove_file(log_path)?;
        }
    }
    Ok(())
}
