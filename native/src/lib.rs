pub mod api;
pub mod conversions;
pub mod curves;
pub mod gamepad;
pub mod input;
pub mod ipc;
pub mod logging;
pub mod mapping;
pub mod profile;
pub mod ui_notifier;
pub mod wooting;

use gamepad::ViGEmClient;
use input::{EventInputManager, HotkeyManager};
use log::{debug, info, warn};
use mapping::{MappingEngine, MAPPING_ENGINE};
use profile::ProfileManager;

use once_cell::sync::Lazy;
use std::sync::Mutex;
use std::time::Instant;
use wooting::WootingSDK;

// Mapping status callback used by the tray application.
static MAPPING_STATUS_CALLBACK: Lazy<Mutex<Option<Box<dyn Fn(bool) + Send + Sync>>>> =
    Lazy::new(|| Mutex::new(None));

// Global instances guarded by mutexes.
pub static WOOTING_SDK: Mutex<Option<WootingSDK>> = Mutex::new(None);
pub static VIGEM_CLIENT: Mutex<Option<ViGEmClient>> = Mutex::new(None);
pub static PROFILE_MANAGER: Mutex<Option<ProfileManager>> = Mutex::new(None);

pub static EVENT_INPUT_MANAGER: Mutex<Option<EventInputManager>> = Mutex::new(None);

// Dependency initialization status tracking.
use std::sync::RwLock;
pub static WOOTING_INIT_STATUS: RwLock<Option<Result<(), String>>> = RwLock::new(None);
pub static VIGEM_INIT_STATUS: RwLock<Option<Result<(), String>>> = RwLock::new(None);

// Shared atomic gamepad state updated by event and mapping threads.
pub static ATOMIC_GAMEPAD_STATE: gamepad::AtomicGamepadState = gamepad::AtomicGamepadState::new();

// Re-export core types and helpers for internal Rust use.
pub use conversions::{
    gamepad_control_to_name, key_name_to_vk, name_to_gamepad_control, vk_to_key_name,
};
pub use profile::{GameProfile, KeyMapping, SubProfile};

pub use input::{
    rebuild_hotkeys_from_metadata, remove_hotkeys_for_profile, sync_hotkeys_for_profile,
};

/// Callback used by the SDK to report device connection changes.
extern "C" fn wooting_device_event_callback(
    event_type: wooting_analog_wrapper::DeviceEventType,
    _device_info: *mut wooting_analog_wrapper::DeviceInfo_FFI,
) {
    use wooting_analog_wrapper::DeviceEventType;

    let connected = match event_type {
        DeviceEventType::Connected => {
            debug!("[WOOTING] Device CONNECTED event (SDK callback - resuming polling)");

            wooting::resume_disconnect_polling();
            true
        }
        DeviceEventType::Disconnected => {
            debug!("[WOOTING] Device DISCONNECTED event (SDK callback)");
            false
        }
    };

    ui_notifier::send_keyboard_status_notification(connected);
}

/// Callback for keyboard status changes detected by periodic polling.
fn keyboard_status_changed_callback(connected: bool) {
    debug!(
        "[WOOTING] Keyboard status changed: {} (detected by has_devices)",
        if connected {
            "CONNECTED"
        } else {
            "DISCONNECTED"
        }
    );

    ui_notifier::send_keyboard_status_notification(connected);
}

pub fn initialize_internal() -> Result<(), Box<dyn std::error::Error>> {
    // Initialize logging before any component can panic.
    logging::init_logger();
    logging::init_crash_logger();

    info!("[INIT] Starting core initialization...");
    let init_start = Instant::now();

    // Initialize Profile Manager
    let step_start = Instant::now();
    let mut profile_manager =
        ProfileManager::new().map_err(|e| format!("Profile Manager error: {}", e))?;
    debug!(
        "[INIT] Profile Manager initialized in {:?}",
        step_start.elapsed()
    );

    // Initialize Event Input Manager
    let step_start = Instant::now();
    let mut event_input_manager = EventInputManager::new();
    debug!(
        "[INIT] Event Input Manager initialized in {:?}",
        step_start.elapsed()
    );

    // Initialize Wooting SDK
    let step_start = Instant::now();
    let mut wooting_sdk = WootingSDK::new();
    let wooting_result = wooting_sdk.initialize();

    match &wooting_result {
        Ok(_) => {
            // Configure Wooting SDK to use Windows Virtual Key codes instead of HID
            use wooting_analog_wrapper::KeycodeType;
            if let Err(e) = wooting_sdk.set_keycode_mode(KeycodeType::VirtualKey) {
                warn!("[WOOTING] Failed to set VK mode: {}", e);
            }

            // Register device event callback for connect/disconnect events
            if let Err(e) = wooting_sdk.set_device_event_callback(wooting_device_event_callback) {
                warn!("[WOOTING] Failed to set device event callback: {}", e);
            }

            // Register keyboard status callback for has_devices() detection
            wooting::set_keyboard_status_callback(keyboard_status_changed_callback);

            // Check initial keyboard status
            let keyboard_connected = wooting_sdk.has_devices();
            debug!(
                "[INIT] Initial keyboard status: {}",
                if keyboard_connected {
                    "CONNECTED"
                } else {
                    "DISCONNECTED"
                }
            );

            {
                debug!("[INIT] Wooting SDK configured for VK codes");
                debug!(
                    "[INIT] Wooting SDK initialized in {:?}",
                    step_start.elapsed()
                );
            }
        }
        Err(e) => {
            warn!("[WOOTING] Initialization failed: {}", e);
            warn!("[WOOTING] Application will continue in degraded mode - analog input disabled");
        }
    }

    // Store initialization status for UI diagnostics
    {
        let mut status = WOOTING_INIT_STATUS.write().unwrap();
        *status = Some(wooting_result.clone());
    }

    // Initialize ViGEm Client
    let step_start = Instant::now();
    let mut vigem_client = ViGEmClient::new();
    let vigem_result = vigem_client.initialize();

    match &vigem_result {
        Ok(_) => {
            debug!(
                "[INIT] ViGEm Client initialized in {:?}",
                step_start.elapsed()
            );
        }
        Err(e) => {
            warn!("[VIGEM] Bus driver initialization failed: {}", e);
            warn!(
                "[VIGEM] Application will continue in degraded mode - gamepad emulation disabled"
            );
        }
    }

    // Store initialization status for UI diagnostics
    {
        let mut status = VIGEM_INIT_STATUS.write().unwrap();
        *status = Some(vigem_result.clone());
    }

    debug!(
        "[INIT] Profile lazy loading system ready - {} profiles available",
        profile_manager.get_profile_names().len()
    );

    // Initialize Event Input Manager
    let step_start = Instant::now();
    event_input_manager
        .start()
        .map_err(|e| format!("Event input manager error: {}", e))?;
    debug!(
        "[INIT] Event-based input system started in {:?}",
        step_start.elapsed()
    );

    // Register hotkeys from profile metadata
    let step_start = Instant::now();
    let hotkey_manager = HotkeyManager::new();
    let registered_hotkeys =
        hotkey_manager.register_from_metadata(&profile_manager, &mut event_input_manager);
    let profile_count = profile_manager.get_profile_metadata_count();
    debug!(
        "[INIT] Hotkey registration completed in {:?} ({} hotkeys)",
        step_start.elapsed(),
        registered_hotkeys
    );

    if let Some(default_profile) = profile_manager.get_profile("Default Game") {
        if let Some(movement_sub) = default_profile
            .sub_profiles
            .iter()
            .find(|sp| sp.name == "Movement")
        {
            let profile_id = default_profile.id.clone();
            let sub_id = movement_sub.id.clone();

            if let Err(e) = profile_manager.switch_profile(&profile_id, &sub_id) {
                warn!("[PROFILE] Could not activate default profile: {}", e);
            } else {
                debug!(
                    "[INIT] Default profile 'Default Game::Movement' activated via lazy loading"
                );
            }
        }
    }

    {
        let mut profile_guard = PROFILE_MANAGER.lock().unwrap();
        *profile_guard = Some(profile_manager);
    }

    // Store Wooting SDK (even if initialization failed, for status queries)
    {
        let mut wooting_guard = WOOTING_SDK.lock().unwrap();
        *wooting_guard = Some(wooting_sdk);
    }

    {
        let mut event_guard = EVENT_INPUT_MANAGER.lock().unwrap();
        *event_guard = Some(event_input_manager);
    }

    // Initialize button callbacks for the current profile if available
    {
        let profile_guard = PROFILE_MANAGER.lock().unwrap();
        if let Some(ref manager) = *profile_guard {
            if let Some(current_profile) = manager.get_current_profile() {
                let mut event_guard = EVENT_INPUT_MANAGER.lock().unwrap();
                if let Some(ref mut event_manager) = *event_guard {
                    event_manager.update_button_callbacks(&current_profile);
                    debug!("[INIT] Button callbacks initialized for default profile");
                }
            }
        }
    }

    // Store ViGEm client (even if initialization failed, for status queries)
    {
        let mut vigem_guard = VIGEM_CLIENT.lock().unwrap();
        *vigem_guard = Some(vigem_client);
    }

    // Initialize Mapping Engine
    let step_start = Instant::now();
    let mapping_engine = MappingEngine::new();
    {
        let mut engine_guard = MAPPING_ENGINE.lock().unwrap();
        *engine_guard = Some(mapping_engine);
    }
    debug!(
        "[INIT] Mapping Engine initialized in {:?}",
        step_start.elapsed()
    );

    let init_time = init_start.elapsed();
    info!("[INIT] Core systems ready in {:?}", init_time);
    info!(
        "[INIT] - Profile lazy loading ready with {} profiles available",
        profile_count
    );
    info!("[INIT] - Hotkey detection active for instant switching");
    info!("[INIT] - Mapping engine ready for 120 FPS operation");

    Ok(())
}

pub fn cleanup_internal() {
    info!("[CLEANUP] Shutting down core systems...");

    // Stop mapping engine first
    {
        let engine_guard = MAPPING_ENGINE.lock().unwrap();
        if let Some(ref engine) = *engine_guard {
            engine.stop_mapping();
        }
    }

    // Stop event input manager to prevent new input events
    {
        let mut event_guard = EVENT_INPUT_MANAGER.lock().unwrap();
        if let Some(mut manager) = event_guard.take() {
            manager.stop();
        }
    }

    // Cleanup Wooting SDK
    {
        let mut wooting_guard = WOOTING_SDK.lock().unwrap();
        if let Some(mut sdk) = wooting_guard.take() {
            sdk.cleanup();
        }
    }

    // Cleanup ViGEm client
    {
        let mut vigem_guard = VIGEM_CLIENT.lock().unwrap();
        if let Some(mut client) = vigem_guard.take() {
            client.cleanup();
        }
    }

    info!("[CLEANUP] Core systems shut down");
}

pub fn get_version() -> String {
    env!("CARGO_PKG_VERSION").to_string()
}

/// Register a callback for mapping engine status changes.
pub fn register_mapping_status_callback<F>(callback: F)
where
    F: Fn(bool) + Send + Sync + 'static,
{
    let mut cb = MAPPING_STATUS_CALLBACK.lock().unwrap();
    *cb = Some(Box::new(callback));
}

/// Notify the registered mapping status callback.
pub fn notify_mapping_status_change(active: bool) {
    if let Some(ref callback) = *MAPPING_STATUS_CALLBACK.lock().unwrap() {
        callback(active);
    }
}
