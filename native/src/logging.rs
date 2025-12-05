use log::{error, info, LevelFilter};
use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::PathBuf;
use std::sync::Mutex;

static CRASH_LOG_PATH: Mutex<Option<PathBuf>> = Mutex::new(None);

/// Initialize env_logger with a default WARN level unless `RUST_LOG` overrides it.
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
