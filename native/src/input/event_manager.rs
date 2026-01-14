use std::collections::{hash_map::Entry, HashMap};
use std::sync::atomic::{AtomicPtr, AtomicU16, AtomicUsize, Ordering as AtomicOrdering};
use std::sync::{mpsc, Arc, Mutex};
use std::thread::{self, JoinHandle};
use uuid::Uuid;
use winapi::shared::minwindef::{HINSTANCE, LPARAM, LRESULT, WPARAM};
use winapi::um::errhandlingapi::GetLastError;
use winapi::um::processthreadsapi::GetCurrentThreadId;
use winapi::um::winuser::{
    CallNextHookEx, GetAsyncKeyState, GetMessageW, PostThreadMessageW, SetWindowsHookExW,
    UnhookWindowsHookEx, HC_ACTION, KBDLLHOOKSTRUCT, LLKHF_ALTDOWN, LLKHF_INJECTED, MSG,
    WH_KEYBOARD_LL, WM_KEYDOWN, WM_KEYUP, WM_QUIT, WM_SYSKEYDOWN, WM_SYSKEYUP,
};

use crate::conversions::{vk, vk_to_key_name};
use crate::profile::profiles::HotKey;
use log::{debug, error, info};

/// Key event types.
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum KeyEvent {
    KeyDown,
    KeyUp,
    SystemKeyDown, // Alt+Key combinations
    SystemKeyUp,
}

/// Key input event with context information.
#[derive(Debug, Clone)]
pub struct KeyInput {
    pub vk_code: u16,                  // Virtual key code
    pub event_type: KeyEvent,          // Press/Release/System
    pub modifiers: u16,                // Current modifier state (Ctrl, Alt, Shift, Win)
    pub key_name: &'static str,        // Human readable name ("W", "Space", ect...)
    pub timestamp: std::time::Instant, // High-precision timestamp
}

/// Callback types for different input scenarios.
pub type HotkeyCallback = Arc<dyn Fn(Uuid, Uuid, &str, &str) + Send + Sync>;
pub type ProfileCycleCallback = Arc<dyn Fn(Uuid) + Send + Sync>;
pub type ButtonCallback = Arc<dyn Fn(bool) + Send + Sync>; // is_pressed -> atomic update

#[derive(Clone)]
enum HotkeyTarget {
    Switch(HotkeySwitchTarget),
    Cycle(HotkeyCycleTarget),
}

#[derive(Clone)]
struct HotkeySwitchTarget {
    profile_id: Uuid,
    profile_name: String,
    sub_profile_id: Uuid,
    sub_profile_name: String,
    callback: HotkeyCallback,
}

#[derive(Clone)]
struct HotkeyCycleTarget {
    profile_id: Uuid,
    callback: ProfileCycleCallback,
}

/// Event-based input manager built on a Windows low-level keyboard hook.
pub struct EventInputManager {
    // Event processing.
    event_sender: Option<mpsc::SyncSender<KeyInput>>,
    hook_thread: Option<JoinHandle<()>>, // Thread with message loop + hook
    processing_thread: Option<JoinHandle<()>>, // Thread for event processing
    hook_thread_id: Option<u32>,

    // Hotkey management (profile switching).
    hotkey_mappings: Arc<Mutex<HashMap<HotKey, Vec<HotkeyTarget>>>>,
    hotkey_suppression: Arc<AtomicUsize>,

    // Button callback system - only active for mapped keys.
    button_callbacks: Arc<Mutex<HashMap<u16, ButtonCallback>>>, // vk_code -> atomic callback

    // State tracking.
    key_states: Arc<Mutex<HashMap<u16, bool>>>, // vk_code -> is_pressed
    modifier_state: Arc<AtomicU16>,             // Atomic modifier combination for hook

    // Performance metrics.
    events_processed: Arc<std::sync::atomic::AtomicU64>,
    events_dropped: Arc<std::sync::atomic::AtomicU64>,
    is_running: Arc<std::sync::atomic::AtomicBool>,
}

// Shared context for hook (atomic access only).
struct HookContext {
    event_sender: mpsc::SyncSender<KeyInput>,
    events_dropped: Arc<std::sync::atomic::AtomicU64>,
}

unsafe impl Send for EventInputManager {}
unsafe impl Sync for EventInputManager {}

// Global hook context pointer (atomic) for safe cross-callback access.
static HOOK_CONTEXT_PTR: AtomicPtr<HookContext> = AtomicPtr::new(std::ptr::null_mut());

impl EventInputManager {
    pub fn new() -> Self {
        Self {
            event_sender: None,
            hook_thread: None,
            processing_thread: None,
            hook_thread_id: None,
            hotkey_mappings: Arc::new(Mutex::new(HashMap::new())),
            hotkey_suppression: Arc::new(AtomicUsize::new(0)),
            button_callbacks: Arc::new(Mutex::new(HashMap::new())),
            key_states: Arc::new(Mutex::new(HashMap::new())),
            modifier_state: Arc::new(AtomicU16::new(0)),
            events_processed: Arc::new(std::sync::atomic::AtomicU64::new(0)),
            events_dropped: Arc::new(std::sync::atomic::AtomicU64::new(0)),
            is_running: Arc::new(std::sync::atomic::AtomicBool::new(false)),
        }
    }

    /// Start the input system.
    pub fn start(&mut self) -> Result<(), Box<dyn std::error::Error>> {
        if self.is_running.load(std::sync::atomic::Ordering::Relaxed) {
            return Ok(());
        }

        // Create event channel with bounded capacity to avoid unbounded memory growth.
        let (sender, receiver) = mpsc::sync_channel::<KeyInput>(1000);
        self.event_sender = Some(sender.clone());

        // Clone Arc references for threads.
        let hotkey_mappings = Arc::clone(&self.hotkey_mappings);
        let hotkey_suppression = Arc::clone(&self.hotkey_suppression);
        let button_callbacks = Arc::clone(&self.button_callbacks);
        let key_states = Arc::clone(&self.key_states);
        let modifier_state = Arc::clone(&self.modifier_state);
        let events_processed = Arc::clone(&self.events_processed);
        let _events_dropped = Arc::clone(&self.events_dropped);
        let is_running = Arc::clone(&self.is_running);

        // THREAD A: Hook installation + Windows message loop (critical for hook delivery).
        let hook_thread_sender = sender.clone();
        let hook_events_dropped = Arc::clone(&self.events_dropped);
        let (thread_id_sender, thread_id_receiver) = mpsc::sync_channel::<u32>(1);

        let hook_thread = thread::spawn(move || {
            #[cfg(debug_assertions)]
            debug!("[INPUT] Starting keyboard hook thread with message loop");

            unsafe {
                // Send thread ID back to main thread.
                let _ = thread_id_sender.send(GetCurrentThreadId());

                // Create hook context for low-latency processing.
                let hook_context = Box::into_raw(Box::new(HookContext {
                    event_sender: hook_thread_sender,
                    events_dropped: hook_events_dropped,
                }));
                HOOK_CONTEXT_PTR.store(hook_context, AtomicOrdering::SeqCst);

                // Install low-level keyboard hook.
                let hook = SetWindowsHookExW(
                    WH_KEYBOARD_LL,
                    Some(keyboard_hook_proc),
                    std::ptr::null_mut() as HINSTANCE,
                    0,
                );

                if hook.is_null() {
                    error!("[INPUT] Failed to install keyboard hook");
                    crate::api::logging::capture_critical_error(
                        "Keyboard Hook Installation",
                        "SetWindowsHookExW returned null - input system cannot function",
                    );
                    HOOK_CONTEXT_PTR.store(std::ptr::null_mut(), AtomicOrdering::SeqCst);
                    let _ = Box::from_raw(hook_context);
                    return;
                }

                #[cfg(debug_assertions)]
                debug!("[INPUT] Keyboard hook installed, starting message loop");

                // Windows message loop for hook event delivery.
                let mut msg: MSG = std::mem::zeroed();
                loop {
                    let ret = GetMessageW(&mut msg, std::ptr::null_mut(), 0, 0);
                    if ret > 0 {
                        // Message loop processes hook events.
                        continue;
                    } else if ret == 0 {
                        // WM_QUIT received.
                        break;
                    } else {
                        // Error
                        let err = GetLastError();
                        error!("[INPUT] GetMessageW error: {}", err);
                        break;
                    }
                }

                // Cleanup on exit.
                UnhookWindowsHookEx(hook);
                HOOK_CONTEXT_PTR.store(std::ptr::null_mut(), AtomicOrdering::SeqCst);
                let _ = Box::from_raw(hook_context);

                #[cfg(debug_assertions)]
                debug!("[INPUT] Hook thread stopped cleanly");
            }
        });

        // Get hook thread ID for cleanup.
        self.hook_thread_id = thread_id_receiver.recv().ok();
        self.hook_thread = Some(hook_thread);

        // THREAD B: Event processing.
        let processing_thread = thread::spawn(move || {
            #[cfg(debug_assertions)]
            debug!("[INPUT] Starting event processing thread");
            is_running.store(true, std::sync::atomic::Ordering::Relaxed);

            // Process incoming key events.
            while let Ok(key_input) = receiver.recv() {
                events_processed.fetch_add(1, std::sync::atomic::Ordering::Relaxed);

                // Determine state and detect repeats.
                let is_pressed = matches!(
                    key_input.event_type,
                    KeyEvent::KeyDown | KeyEvent::SystemKeyDown
                );
                let mut process_event = true;
                {
                    let mut states = key_states.lock().unwrap();
                    let prev = states.get(&key_input.vk_code).copied().unwrap_or(false);
                    if prev == is_pressed {
                        // Ignore auto-repeat.
                        process_event = false;
                    } else {
                        states.insert(key_input.vk_code, is_pressed);
                    }
                }

                // Update atomic modifier state for next hook calls.
                Self::update_atomic_modifier_state(&modifier_state, key_input.vk_code, is_pressed);

                if process_event {
                    // Process hotkeys (on key press only)
                    if matches!(
                        key_input.event_type,
                        KeyEvent::KeyDown | KeyEvent::SystemKeyDown
                    ) {
                        Self::process_hotkeys(&hotkey_mappings, &hotkey_suppression, &key_input);
                    }

                    // Invoke button callbacks after processing the event.
                    Self::process_button_callbacks(&button_callbacks, &key_input);
                }
            }

            #[cfg(debug_assertions)]
            debug!("[INPUT] Event processing thread stopped");
        });

        self.processing_thread = Some(processing_thread);

        #[cfg(debug_assertions)]
        info!("[INPUT] Event-based input manager started");
        Ok(())
    }

    /// Register hotkey for sub-profile switching.
    pub fn register_switch_hotkey(
        &mut self,
        hotkey: HotKey,
        profile_id: Uuid,
        profile_name: String,
        sub_profile_id: Uuid,
        sub_profile_name: String,
        callback: HotkeyCallback,
    ) -> Result<bool, Box<dyn std::error::Error>> {
        let mut mappings = self.hotkey_mappings.lock().unwrap();
        match mappings.entry(hotkey.clone()) {
            Entry::Occupied(mut entry) => {
                let targets = entry.get_mut();
                if targets.iter().any(|target| matches!(target, HotkeyTarget::Switch(existing) if existing.profile_id == profile_id && existing.sub_profile_id == sub_profile_id)) {
                    return Ok(false);
                }
                targets.push(HotkeyTarget::Switch(HotkeySwitchTarget {
                    profile_id,
                    profile_name,
                    sub_profile_id,
                    sub_profile_name,
                    callback,
                }));
            }
            Entry::Vacant(entry) => {
                entry.insert(vec![HotkeyTarget::Switch(HotkeySwitchTarget {
                    profile_id,
                    profile_name,
                    sub_profile_id,
                    sub_profile_name,
                    callback,
                })]);
            }
        }
        Ok(true)
    }

    /// Register hotkey to cycle through a profile's sub-profiles.
    pub fn register_cycle_hotkey(
        &mut self,
        hotkey: HotKey,
        profile_id: Uuid,
        callback: ProfileCycleCallback,
    ) -> Result<bool, Box<dyn std::error::Error>> {
        let mut mappings = self.hotkey_mappings.lock().unwrap();
        match mappings.entry(hotkey.clone()) {
            Entry::Occupied(mut entry) => {
                let targets = entry.get_mut();
                if targets.iter().any(|target| matches!(target, HotkeyTarget::Cycle(existing) if existing.profile_id == profile_id)) {
                    return Ok(false);
                }
                targets.push(HotkeyTarget::Cycle(HotkeyCycleTarget {
                    profile_id,
                    callback,
                }));
            }
            Entry::Vacant(entry) => {
                entry.insert(vec![HotkeyTarget::Cycle(HotkeyCycleTarget {
                    profile_id,
                    callback,
                })]);
            }
        }
        Ok(true)
    }

    /// Remove a previously registered hotkey.
    pub fn unregister_hotkey(&mut self, hotkey: &HotKey) {
        let mut mappings = self.hotkey_mappings.lock().unwrap();
        mappings.remove(hotkey);
    }

    /// Clear all registered hotkeys.
    pub fn clear_hotkeys(&mut self) {
        let mut mappings = self.hotkey_mappings.lock().unwrap();
        mappings.clear();
    }

    pub fn remove_hotkeys_for_profile(&mut self, profile_id: Uuid) {
        let mut mappings = self.hotkey_mappings.lock().unwrap();
        mappings.retain(|_, targets| {
            targets.retain(|target| match target {
                HotkeyTarget::Switch(target) => target.profile_id != profile_id,
                HotkeyTarget::Cycle(target) => target.profile_id != profile_id,
            });
            !targets.is_empty()
        });
    }

    pub fn suspend_hotkeys(&mut self) {
        self.hotkey_suppression.fetch_add(1, AtomicOrdering::SeqCst);
    }

    pub fn resume_hotkeys(&mut self) {
        let mut current = self.hotkey_suppression.load(AtomicOrdering::SeqCst);
        while current > 0 {
            match self.hotkey_suppression.compare_exchange(
                current,
                current - 1,
                AtomicOrdering::SeqCst,
                AtomicOrdering::SeqCst,
            ) {
                Ok(_) => break,
                Err(actual) => current = actual,
            }
        }
    }

    /// Update button callbacks based on current profile mappings.
    /// Only registers callbacks for keys that are mapped to digital buttons.
    pub fn update_button_callbacks(
        &mut self,
        compiled_profile: &crate::profile::profiles::CompiledProfile,
    ) {
        use crate::gamepad::AtomicGamepadState;
        use crate::ATOMIC_GAMEPAD_STATE;

        let mut callbacks = self.button_callbacks.lock().unwrap();
        callbacks.clear();

        // Pre-register callbacks only for keys mapped to digital buttons.
        for (vk_code, compiled_mapping) in &compiled_profile.mappings {
            if let Some(xbox_button) = AtomicGamepadState::gamepad_control_to_xbox_button(
                &compiled_mapping.gamepad_control,
            ) {
                // Create a callback that directly updates the atomic state.
                let callback: ButtonCallback = Arc::new(move |is_pressed: bool| {
                    ATOMIC_GAMEPAD_STATE.set_button(xbox_button, is_pressed);
                });
                callbacks.insert(*vk_code, callback);
            }
        }
    }

    /// Check if key is currently pressed.
    pub fn is_key_pressed(&self, vk_code: u16) -> bool {
        let states = self.key_states.lock().unwrap();
        states.get(&vk_code).copied().unwrap_or(false)
    }

    /// Get current modifier state.
    pub fn get_modifier_state(&self) -> u16 {
        self.modifier_state
            .load(std::sync::atomic::Ordering::Relaxed)
    }

    /// Get events dropped count.
    pub fn get_events_dropped(&self) -> u64 {
        self.events_dropped
            .load(std::sync::atomic::Ordering::Relaxed)
    }

    /// Get performance metrics.
    pub fn get_events_processed(&self) -> u64 {
        self.events_processed
            .load(std::sync::atomic::Ordering::Relaxed)
    }

    /// Stop the input system and cleanup.
    pub fn stop(&mut self) {
        if !self.is_running.load(std::sync::atomic::Ordering::Relaxed) {
            return;
        }

        self.is_running
            .store(false, std::sync::atomic::Ordering::Relaxed);

        if let Some(thread_id) = self.hook_thread_id {
            unsafe {
                PostThreadMessageW(thread_id, WM_QUIT, 0, 0);
            }
        }

        // Close event channel to stop processing thread.
        self.event_sender = None;

        // Wait for threads to finish.
        if let Some(handle) = self.hook_thread.take() {
            let _ = handle.join();
        }
        if let Some(handle) = self.processing_thread.take() {
            let _ = handle.join();
        }

        info!("[INPUT] Event-based input system stopped");
    }

    // Internal helper methods.
    fn update_atomic_modifier_state(
        modifier_state: &Arc<AtomicU16>,
        vk_code: u16,
        is_pressed: bool,
    ) {
        loop {
            let current = modifier_state.load(std::sync::atomic::Ordering::Relaxed);
            let new_state = match vk_code {
                vk::LCONTROL | vk::RCONTROL => {
                    if is_pressed {
                        current | 1
                    } else {
                        current & !1
                    }
                }
                vk::LMENU | vk::RMENU => {
                    if is_pressed {
                        current | 2
                    } else {
                        current & !2
                    }
                }
                vk::LSHIFT | vk::RSHIFT => {
                    if is_pressed {
                        current | 4
                    } else {
                        current & !4
                    }
                }
                vk::LWIN | vk::RWIN => {
                    if is_pressed {
                        current | 8
                    } else {
                        current & !8
                    }
                }
                _ => current,
            };

            if modifier_state
                .compare_exchange_weak(
                    current,
                    new_state,
                    std::sync::atomic::Ordering::Relaxed,
                    std::sync::atomic::Ordering::Relaxed,
                )
                .is_ok()
            {
                break;
            }
        }
    }

    fn process_hotkeys(
        hotkey_mappings: &Arc<Mutex<HashMap<HotKey, Vec<HotkeyTarget>>>>,
        hotkey_suppression: &Arc<AtomicUsize>,
        key_input: &KeyInput,
    ) {
        if hotkey_suppression.load(AtomicOrdering::Relaxed) > 0 {
            return;
        }
        let mappings = hotkey_mappings.lock().unwrap();
        for (registered_hotkey, targets) in mappings.iter() {
            if registered_hotkey.get_vk_code() == key_input.vk_code
                && registered_hotkey.modifiers as u16 == key_input.modifiers
            {
                for target in targets {
                    match target {
                        HotkeyTarget::Switch(target) => {
                            (target.callback)(
                                target.profile_id,
                                target.sub_profile_id,
                                &target.profile_name,
                                &target.sub_profile_name,
                            );
                        }
                        HotkeyTarget::Cycle(target) => {
                            (target.callback)(target.profile_id);
                        }
                    }
                }
                // Hotkey executed.
                break;
            }
        }
    }

    /// Process button callbacks for keys with registered handlers.
    fn process_button_callbacks(
        callbacks: &Arc<Mutex<HashMap<u16, ButtonCallback>>>,
        key_input: &KeyInput,
    ) {
        let callback_option = {
            let callback_map = callbacks.lock().unwrap();
            callback_map.get(&key_input.vk_code).cloned() // Clone Arc for execution outside lock.
        };

        // Execute callback if this key is mapped to a digital button.
        if let Some(callback) = callback_option {
            let is_pressed = matches!(
                key_input.event_type,
                KeyEvent::KeyDown | KeyEvent::SystemKeyDown
            );
            callback(is_pressed); // Direct atomic update via pre-registered callback.
        }
    }
}

impl Drop for EventInputManager {
    fn drop(&mut self) {
        self.stop();
    }
}

// Low-level keyboard hook that minimizes locking.
unsafe extern "system" fn keyboard_hook_proc(code: i32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    if code == HC_ACTION as i32 {
        let hook_context_ptr = HOOK_CONTEXT_PTR.load(AtomicOrdering::Relaxed);
        if !hook_context_ptr.is_null() {
            let hook_context = &*hook_context_ptr;
            let kb_struct = &*(lparam as *const KBDLLHOOKSTRUCT);
            let vk_code = kb_struct.vkCode as u16;
            let flags = kb_struct.flags;

            // Skip injected events to avoid loops.
            if (flags & LLKHF_INJECTED) != 0 {
                return CallNextHookEx(std::ptr::null_mut(), code, wparam, lparam);
            }

            let event_type = match wparam as u32 {
                WM_KEYDOWN => KeyEvent::KeyDown,
                WM_KEYUP => KeyEvent::KeyUp,
                WM_SYSKEYDOWN => KeyEvent::SystemKeyDown,
                WM_SYSKEYUP => KeyEvent::SystemKeyUp,
                _ => return CallNextHookEx(std::ptr::null_mut(), code, wparam, lparam),
            };

            // Read modifier state using GetAsyncKeyState without locking.
            let mut modifiers = 0u16;
            if (flags & LLKHF_ALTDOWN) != 0 {
                modifiers |= 2;
            } // Alt down
            if GetAsyncKeyState(vk::LSHIFT as i32) < 0 || GetAsyncKeyState(vk::RSHIFT as i32) < 0 {
                modifiers |= 4;
            }
            if GetAsyncKeyState(vk::LCONTROL as i32) < 0
                || GetAsyncKeyState(vk::RCONTROL as i32) < 0
            {
                modifiers |= 1;
            }
            if GetAsyncKeyState(vk::LWIN as i32) < 0 || GetAsyncKeyState(vk::RWIN as i32) < 0 {
                modifiers |= 8;
            }

            // Create key input.
            let key_input = KeyInput {
                vk_code,
                event_type,
                modifiers,
                key_name: vk_to_key_name(vk_code),
                timestamp: std::time::Instant::now(),
            };

            // Try send without blocking
            match hook_context.event_sender.try_send(key_input) {
                Ok(_) => {} // Success
                Err(_) => {
                    // Channel full
                    hook_context
                        .events_dropped
                        .fetch_add(1, std::sync::atomic::Ordering::Relaxed);
                }
            }
        }
    }

    CallNextHookEx(std::ptr::null_mut(), code, wparam, lparam)
}
