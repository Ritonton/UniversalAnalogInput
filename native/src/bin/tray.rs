// Console window only in debug builds
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]
#![cfg_attr(debug_assertions, windows_subsystem = "console")]

extern crate universal_analog_input;
use log::{error, info};

#[path = "tray_modules/handler.rs"]
mod handler;
#[path = "tray_modules/tray_ui.rs"]
mod tray_ui;

use std::sync::atomic::{AtomicU8, Ordering};
use std::sync::{Arc, Condvar, Mutex};
use std::thread;
use std::time::Duration;
use windows::core::{Error as Win32Error, PCWSTR};
use windows::Win32::Foundation::{
    CloseHandle, GetLastError, ERROR_ALREADY_EXISTS, HANDLE, WAIT_FAILED, WAIT_OBJECT_0,
};
use windows::Win32::System::Threading::{
    CreateEventW, CreateMutexW, OpenEventW, ReleaseMutex, ResetEvent, SetEvent,
    WaitForSingleObject, EVENT_MODIFY_STATE,
};

/// Global IPC connection state
static IPC_STATE: AtomicU8 = AtomicU8::new(IpcState::Idle as u8);

/// Global IPC server reference for shutdown signaling
static IPC_SERVER: once_cell::sync::OnceCell<Arc<universal_analog_input::ipc::IpcServer>> =
    once_cell::sync::OnceCell::new();

/// Global keyboard connection status
static KEYBOARD_CONNECTED: AtomicU8 = AtomicU8::new(1); // 1 = connected, 0 = disconnected (assume connected initially)

/// IPC connection states
#[derive(Debug, Clone, Copy, PartialEq)]
#[repr(u8)]
enum IpcState {
    Idle = 0,         // Not waiting for connection
    WaitingForUi = 1, // Waiting for UI to connect (after launching)
    Connected = 2,    // UI connected and active
}

/// Signal to wake IPC thread when UI should be launched
static IPC_WAKE_SIGNAL: Mutex<bool> = Mutex::new(false);
static IPC_WAKE_CONDVAR: Condvar = Condvar::new();
const TRAY_INSTANCE_MUTEX: &str = "Global\\UniversalAnalogInput_Tray";
const TRAY_SHOW_UI_EVENT: &str = "Global\\UniversalAnalogInput_Tray_ShowUI";

fn main() {
    // Load or ignore .env file
    let _ = dotenvy::dotenv();

    // Initialize logging
    universal_analog_input::logging::init_logger();
    universal_analog_input::logging::init_crash_logger();

    // Initialize Sentry monitoring from .env file or environment variables
    // Priority: .env file > system environment variables
    // Native component uses NATIVE_SENTRY_DSN (separate from UI component)
    if let Ok(dsn) = std::env::var("NATIVE_SENTRY_DSN") {
        let environment = std::env::var("SENTRY_ENVIRONMENT").ok();
        if universal_analog_input::api::logging::init_sentry(
            Some(&dsn),
            environment.as_deref(),
        ) {
            info!("[TRAY] Sentry monitoring initialized (native) - Environment: {:?}", environment);
        }
    } else {
        info!("[TRAY] Sentry monitoring disabled (no NATIVE_SENTRY_DSN configured)");
    }

    // Enforce single tray instance (prevents IPC corruption)
    let _single_instance_guard = match SingleInstanceGuard::acquire(TRAY_INSTANCE_MUTEX) {
        Ok(guard) => guard,
        Err(SingleInstanceError::AlreadyRunning) => {
            info!("[TRAY] Instance already running - forwarding UI launch request");
            if let Err(e) = signal_existing_tray_show_ui() {
                error!("[TRAY] Failed to signal existing tray instance: {}", e);
            }
            return;
        }
        Err(err) => {
            let error_msg = format!("{}", err);
            error!("[TRAY] Impossible de vérifier l'instance active: {}", err);
            universal_analog_input::api::logging::capture_critical_error(
                "Tray Instance Check",
                &error_msg,
            );
            show_error_dialog(
                "Erreur système",
                "Impossible de vérifier si le tray est déjà lancé. Merci de réessayer.",
            );
            return;
        }
    };

    info!("[TRAY] Universal Analog Input - Tray Application");

    if let Err(e) = start_external_ui_signal_listener() {
        error!("[TRAY] Failed to start UI signal listener: {}", e);
    }

    // ===== OPTIMIZATION: Pre-cache UI path BEFORE any I/O =====
    // This happens in < 1ms and avoids searching later
    handler::cache_ui_path();

    info!("[TRAY] Initializing Rust core library...");

    // ===== OPTIMIZATION: Parallel initialization =====
    // Spawn IPC server thread BEFORE library init (they're independent)
    let _ipc_thread = thread::spawn(|| {
        run_ipc_server();
    });

    info!(
        "[TRAY] IPC server starting on pipe: {}",
        universal_analog_input::ipc::PIPE_NAME
    );

    // Initialize the Rust library (Wooting SDK, ViGEm, etc.)
    // This runs in parallel with IPC server setup
    if let Err(e) = initialize_library() {
        error!("[TRAY] Failed to initialize library: {}", e);
        universal_analog_input::api::logging::capture_critical_error(
            "Library Initialization",
            &e,
        );
        show_error_dialog("Initialization Error", &e);
        return;
    }

    info!("[TRAY] Library initialized successfully");

    // Register tray keyboard status callback (independent of IPC)
    // This ensures badge updates work even when UI is not connected
    universal_analog_input::ui_notifier::register_tray_keyboard_callback(|connected| {
        update_keyboard_status(connected);
    });
    info!("[TRAY] Keyboard status callback registered");

    // Register mapping engine status callback
    // This updates the tooltip when mapping starts/stops
    universal_analog_input::register_mapping_status_callback(|active| {
        update_mapping_status(active);
    });
    info!("[TRAY] Mapping status callback registered");

    // Check initial keyboard status and update badge
    let initial_status = {
        use universal_analog_input::WOOTING_SDK;
        let sdk_guard = WOOTING_SDK.lock().unwrap();
        if let Some(ref sdk) = *sdk_guard {
            sdk.has_devices()
        } else {
            false
        }
    };
    update_keyboard_status(initial_status);
    info!(
        "[TRAY] Initial keyboard status: {}",
        if initial_status {
            "CONNECTED"
        } else {
            "DISCONNECTED"
        }
    );

    // Auto-launch UI when tray starts
    // UI will launch IMMEDIATELY (< 5ms) thanks to cached path + detached spawn
    info!("[TRAY] Auto-launching UI...");
    request_ui_launch();

    info!("[TRAY] Starting tray UI...");

    // Run the tray UI on the main thread (Win32 UI requires main thread)
    tray_ui::TrayApp::run();

    info!("[TRAY] Tray UI closed, shutting down...");

    // Signal UI to close gracefully via IPC (if connected)
    request_ui_shutdown();

    // Cleanup library AFTER signaling UI
    cleanup_library();

    info!("[TRAY] Shutdown complete");
    std::process::exit(0);
}

/// Initialize the Rust core library
fn initialize_library() -> Result<(), String> {
    universal_analog_input::initialize_internal().map_err(|e| e.to_string())
}

/// Cleanup the Rust core library
fn cleanup_library() {
    universal_analog_input::cleanup_internal();
}

/// Update keyboard connection status (called by notification callback)
pub fn update_keyboard_status(connected: bool) {
    let new_value = if connected { 1 } else { 0 };
    KEYBOARD_CONNECTED.store(new_value, Ordering::Release);

    info!(
        "[TRAY] Keyboard status updated: {}",
        if connected {
            "CONNECTED"
        } else {
            "DISCONNECTED"
        }
    );

    // Notify tray UI to update icon and tooltip
    tray_ui::update_keyboard_status(connected);
}

/// Update mapping engine status (called by notification callback)
pub fn update_mapping_status(active: bool) {
    info!(
        "[TRAY] Mapping status updated: {}",
        if active { "ACTIVE" } else { "INACTIVE" }
    );

    // Notify tray UI to update tooltip
    tray_ui::update_mapping_status(active);
}

/// Get current keyboard connection status
#[allow(dead_code)]
pub fn get_keyboard_status() -> bool {
    KEYBOARD_CONNECTED.load(Ordering::Acquire) != 0
}

/// Request UI launch (called from tray menu or auto-launch at startup)
pub fn request_ui_launch() {
    info!("[TRAY] UI launch requested");

    // Check if UI is already connected or waiting for connection
    let state = IPC_STATE.load(Ordering::Acquire);
    if state == IpcState::Connected as u8 {
        info!("[TRAY] UI already connected - sending BringToFront notification");
        universal_analog_input::ui_notifier::send_bring_to_front_notification();
        return;
    }
    if state == IpcState::WaitingForUi as u8 {
        info!("[TRAY] Already waiting for UI connection - ignoring launch request");
        return;
    }

    // Signal IPC thread to start waiting for connection
    *IPC_WAKE_SIGNAL.lock().unwrap() = true;
    IPC_WAKE_CONDVAR.notify_one();
}

/// Request UI shutdown (called when tray is closing)
fn request_ui_shutdown() {
    info!("[TRAY] Requesting UI shutdown via IPC...");

    // Check if IPC is connected
    let state = IPC_STATE.load(Ordering::Acquire);
    if state != IpcState::Connected as u8 {
        info!("[TRAY] UI not connected, skipping shutdown signal");
        return;
    }

    // Signal IPC server to send Shutdown notification and get completion receiver
    if let Some(server) = IPC_SERVER.get() {
        let shutdown_complete_rx = server.request_shutdown();
        info!("[TRAY] Shutdown signal sent to IPC server");

        // Wait for shutdown completion (event-driven, zero polling)
        // The IPC thread will signal when client disconnects
        match shutdown_complete_rx.recv_timeout(Duration::from_secs(2)) {
            Ok(_) => {
                info!("[TRAY] Shutdown complete (signaled by IPC thread)");
            }
            Err(std::sync::mpsc::RecvTimeoutError::Timeout) => {
                info!("[TRAY] Shutdown timeout after 2s - continuing anyway");
            }
            Err(std::sync::mpsc::RecvTimeoutError::Disconnected) => {
                info!("[TRAY] Shutdown channel disconnected unexpectedly");
            }
        }
    } else {
        info!("[TRAY] IPC server not initialized");
    }
}

/// Run the IPC server with intelligent 3-state connection management
/// State 1: IDLE - Not waiting (0% CPU)
/// State 2: WAITING_FOR_UI - Waiting with timeout after UI launch
/// State 3: CONNECTED - Active communication
fn run_ipc_server() {
    // Arc for safe shared ownership (no raw pointers)
    let server = match universal_analog_input::ipc::IpcServer::new().map(Arc::new) {
        Ok(s) => s,
        Err(e) => {
            let error_msg = format!("{}", e);
            error!("[IPC] Failed to create IPC server: {}", e);
            universal_analog_input::api::logging::capture_critical_error(
                "IPC Server Creation",
                &error_msg,
            );
            return;
        }
    };

    // Store server reference globally for shutdown signaling
    if IPC_SERVER.set(Arc::clone(&server)).is_err() {
        error!("[IPC] Failed to set global server reference");
    }

    info!("[IPC] Server created, entering IDLE state (not waiting)");
    IPC_STATE.store(IpcState::Idle as u8, Ordering::Release);

    loop {
        // State 1: IDLE - Wait for signal to launch UI (Condvar blocks, 0% CPU)
        {
            let wake = IPC_WAKE_SIGNAL.lock().unwrap();
            info!("[IPC] State: IDLE - sleeping until UI launch requested");

            // wait_while: blocks until predicate returns false
            // More idiomatic than manual while + wait
            let _wake = IPC_WAKE_CONDVAR.wait_while(wake, |flag| !*flag).unwrap();
        }

        // Reset signal
        *IPC_WAKE_SIGNAL.lock().unwrap() = false;

        info!("[IPC] UI launch requested - transitioning to WAITING_FOR_UI");
        IPC_STATE.store(IpcState::WaitingForUi as u8, Ordering::Release);

        // Launch UI
        if let Err(e) = launch_ui() {
            error!("[IPC] Failed to launch UI: {}", e);
            IPC_STATE.store(IpcState::Idle as u8, Ordering::Release);
            continue;
        }

        info!("[IPC] UI launched, waiting for connection (30s timeout)...");

        // State 2: WAITING_FOR_UI - Wait for connection with timeout
        match server.wait_for_connection_with_timeout(Duration::from_secs(30)) {
            Ok(()) => {
                info!("[IPC] Client connected - transitioning to CONNECTED");
                IPC_STATE.store(IpcState::Connected as u8, Ordering::Release);

                // Notify tray UI that UI is now open
                tray_ui::notify_ui_opened();
            }
            Err(e) => {
                error!("[IPC] Connection timeout or error: {}", e);
                IPC_STATE.store(IpcState::Idle as u8, Ordering::Release);
                continue;
            }
        }

        // Register notification callback with Weak reference (safe lifetime)
        {
            let weak = Arc::downgrade(&server);
            universal_analog_input::ui_notifier::register_notification_callback(
                move |notification| {
                    // Upgrade Weak -> Arc, fails gracefully if server dropped
                    if let Some(server) = weak.upgrade() {
                        server.queue_notification(notification);
                    } else {
                        error!("[IPC] Notification dropped - server no longer alive");
                    }
                },
            );
        }

        info!("[IPC] Notification callback registered");

        // State 3: CONNECTED - Run event loop (tokio::select! blocks, 0% CPU)
        // Note: Initial keyboard status is sent inside run_event_loop after channels are ready
        if let Err(e) =
            server.run_event_loop(|command| handler::CommandHandler::handle_command(command))
        {
            error!("[IPC] Event loop error: {}", e);
        }

        info!("[IPC] Client disconnected - returning to IDLE state");
        server.disconnect();
        IPC_STATE.store(IpcState::Idle as u8, Ordering::Release);

        // Notify tray UI that UI is now closed
        tray_ui::notify_ui_closed();

        // De-register notification callback (no-op when disconnected)
        universal_analog_input::ui_notifier::register_notification_callback(|_| {
            // No-op
        });
    }
}

/// Launch the WinUI 3 UI application or bring existing instance to front
fn launch_ui() -> std::result::Result<(), String> {
    // Delegate to handler module to avoid duplication
    handler::launch_ui_or_bring_to_front()
}

/// Show an error dialog using Win32 MessageBox
fn show_error_dialog(title: &str, message: &str) {
    use windows::Win32::UI::WindowsAndMessaging::*;

    unsafe {
        let title_wide = to_wide(title);
        let message_wide = to_wide(message);

        MessageBoxW(
            None,
            PCWSTR(message_wide.as_ptr()),
            PCWSTR(title_wide.as_ptr()),
            MB_OK | MB_ICONERROR,
        );
    }
}

fn to_wide(s: &str) -> Vec<u16> {
    use std::ffi::OsStr;
    use std::iter::once;
    use std::os::windows::ffi::OsStrExt;

    OsStr::new(s).encode_wide().chain(once(0)).collect()
}

struct SingleInstanceGuard {
    handle: HANDLE,
}

impl SingleInstanceGuard {
    fn acquire(name: &str) -> Result<Self, SingleInstanceError> {
        let wide_name = to_wide(name);

        unsafe {
            let handle = CreateMutexW(None, true.into(), PCWSTR(wide_name.as_ptr()))
                .map_err(SingleInstanceError::CreateFailed)?;

            let last_error = GetLastError();
            if last_error == ERROR_ALREADY_EXISTS {
                let _ = CloseHandle(handle);
                return Err(SingleInstanceError::AlreadyRunning);
            }

            Ok(Self { handle })
        }
    }
}

impl Drop for SingleInstanceGuard {
    fn drop(&mut self) {
        unsafe {
            if self.handle.is_invalid() {
                return;
            }
            let _ = ReleaseMutex(self.handle);
            let _ = CloseHandle(self.handle);
            self.handle = HANDLE::default();
        }
    }
}

#[derive(Debug)]
enum SingleInstanceError {
    AlreadyRunning,
    CreateFailed(Win32Error),
}

impl std::fmt::Display for SingleInstanceError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            SingleInstanceError::AlreadyRunning => write!(f, "instance déjà active"),
            SingleInstanceError::CreateFailed(err) => write!(f, "erreur système: {}", err),
        }
    }
}

fn start_external_ui_signal_listener() -> Result<(), String> {
    let event_name = to_wide(TRAY_SHOW_UI_EVENT);

    unsafe {
        let event = CreateEventW(
            None,
            true.into(),  // manual reset
            false.into(), // initial state non-signaled
            PCWSTR(event_name.as_ptr()),
        )
        .map_err(|e| format!("CreateEventW failed: {}", e))?;

        let event_handle = TrayUiEventHandle(event);

        thread::spawn(move || {
            let handle = event_handle.raw();
            loop {
                let wait_result = WaitForSingleObject(handle, u32::MAX);
                match wait_result {
                    WAIT_OBJECT_0 => {
                        info!("[TRAY] External UI launch signal received");
                        request_ui_launch();
                        let _ = ResetEvent(handle);
                    }
                    WAIT_FAILED => {
                        error!("[TRAY] WaitForSingleObject failed for UI signal event");
                        break;
                    }
                    _ => {}
                }
            }
        });
    }

    Ok(())
}

fn signal_existing_tray_show_ui() -> Result<(), String> {
    let event_name = to_wide(TRAY_SHOW_UI_EVENT);

    unsafe {
        let event = OpenEventW(EVENT_MODIFY_STATE, false, PCWSTR(event_name.as_ptr()))
            .map_err(|e| format!("OpenEventW failed: {}", e))?;

        SetEvent(event).map_err(|e| format!("SetEvent failed: {}", e))?;

        let _ = CloseHandle(event);
    }

    Ok(())
}

struct TrayUiEventHandle(HANDLE);

impl TrayUiEventHandle {
    fn raw(&self) -> HANDLE {
        self.0
    }
}

unsafe impl Send for TrayUiEventHandle {}

impl Drop for TrayUiEventHandle {
    fn drop(&mut self) {
        unsafe {
            let _ = CloseHandle(self.0);
        }
    }
}
