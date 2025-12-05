use crate::profile::profiles::GamepadControl;
use arc_swap::ArcSwap;
use log::{debug, error};
use std::sync::{
    atomic::{AtomicBool, AtomicU64, Ordering},
    Arc, Mutex,
};
use std::thread::{self, JoinHandle};
use std::time::{Duration, Instant};

pub struct MappingEngine {
    mapping_active: Arc<AtomicBool>,
    mapping_thread: Arc<Mutex<Option<JoinHandle<()>>>>,
    // Performance metrics.
    frame_count: Arc<AtomicU64>,
    total_frame_time: Arc<AtomicU64>, // microseconds
    max_frame_time: Arc<AtomicU64>,   // microseconds
    mapping_hits: Arc<AtomicU64>,
    mapping_misses: Arc<AtomicU64>,
    frames_over_budget: Arc<AtomicU64>,
    // Thread-safe profile storage.
    current_profile: Arc<ArcSwap<Option<Arc<crate::profile::profiles::CompiledProfile>>>>,
}

impl MappingEngine {
    pub fn new() -> Self {
        Self {
            mapping_active: Arc::new(AtomicBool::new(false)),
            mapping_thread: Arc::new(Mutex::new(None)),
            frame_count: Arc::new(AtomicU64::new(0)),
            total_frame_time: Arc::new(AtomicU64::new(0)),
            max_frame_time: Arc::new(AtomicU64::new(0)),
            mapping_hits: Arc::new(AtomicU64::new(0)),
            mapping_misses: Arc::new(AtomicU64::new(0)),
            frames_over_budget: Arc::new(AtomicU64::new(0)),
            current_profile: Arc::new(ArcSwap::from_pointee(None)),
        }
    }

    /// Get performance statistics.
    pub fn get_performance_metrics(&self) -> (f64, f64, f64, u64, u64) {
        let frames = self.frame_count.load(Ordering::Relaxed);
        let total_time = self.total_frame_time.load(Ordering::Relaxed);
        let max_time = self.max_frame_time.load(Ordering::Relaxed);
        let hits = self.mapping_hits.load(Ordering::Relaxed);
        let misses = self.mapping_misses.load(Ordering::Relaxed);

        let avg_frame_time = if frames > 0 {
            total_time as f64 / frames as f64
        } else {
            0.0
        };
        let avg_fps = if avg_frame_time > 0.0 {
            1_000_000.0 / avg_frame_time
        } else {
            0.0
        };
        let max_frame_ms = max_time as f64 / 1000.0;

        (avg_fps, avg_frame_time / 1000.0, max_frame_ms, hits, misses)
    }

    /// Get count of frames that exceeded the target frame time budget.
    pub fn get_frames_over_budget(&self) -> u64 {
        self.frames_over_budget.load(Ordering::Relaxed)
    }

    /// Replace the cached profile used by the mapping loop.
    pub fn update_cached_profile(&self, compiled: Arc<crate::profile::profiles::CompiledProfile>) {
        self.current_profile.store(Arc::new(Some(compiled)));
    }

    pub fn clear_cached_profile(&self) {
        self.current_profile.store(Arc::new(None));
    }

    pub fn start_mapping(
        &self,
        wooting_sdk: &'static Mutex<Option<crate::wooting::WootingSDK>>,
        vigem_client: &'static Mutex<Option<crate::gamepad::ViGEmClient>>,
    ) -> Result<(), &'static str> {
        self.stop_mapping();

        {
            use crate::{VIGEM_INIT_STATUS, WOOTING_INIT_STATUS};

            let wooting_status = WOOTING_INIT_STATUS.read().unwrap();
            if !wooting_status
                .as_ref()
                .map(|result| result.is_ok())
                .unwrap_or(false)
            {
                return Err("Wooting SDK not initialized or failed to initialize");
            }

            let vigem_status = VIGEM_INIT_STATUS.read().unwrap();
            if !vigem_status
                .as_ref()
                .map(|result| result.is_ok())
                .unwrap_or(false)
            {
                return Err("ViGEm Bus Driver not initialized or failed to initialize");
            }

            let wooting_guard = wooting_sdk.lock().unwrap();
            let vigem_guard = vigem_client.lock().unwrap();

            if wooting_guard.is_none() || vigem_guard.is_none() {
                return Err("Systems not initialized");
            }
        }

        {
            use crate::PROFILE_MANAGER;
            let manager_guard = PROFILE_MANAGER.lock().unwrap();
            if let Some(ref manager) = *manager_guard {
                if let Some(current_profile) = manager.get_current_profile() {
                    self.update_cached_profile(current_profile);
                } else {
                    self.clear_cached_profile();
                }
            }
        }

        self.mapping_active.store(true, Ordering::Relaxed);

        let mapping_active = Arc::clone(&self.mapping_active);

        let frame_count = Arc::clone(&self.frame_count);
        let total_frame_time = Arc::clone(&self.total_frame_time);
        let max_frame_time = Arc::clone(&self.max_frame_time);
        let mapping_hits = Arc::clone(&self.mapping_hits);
        let mapping_misses = Arc::clone(&self.mapping_misses);

        let wooting_sdk_arc = Arc::new(wooting_sdk);
        let vigem_client_arc = Arc::new(vigem_client);
        let current_profile = Arc::clone(&self.current_profile);
        let frames_over_budget = Arc::clone(&self.frames_over_budget);
        let mapping_thread_handle = thread::spawn(move || {
            #[cfg(debug_assertions)]
            debug!("[MAPPING] Mapping loop started (120 FPS)");

            Self::mapping_loop_optimized(
                mapping_active,
                wooting_sdk_arc,
                vigem_client_arc,
                frame_count,
                total_frame_time,
                max_frame_time,
                mapping_hits,
                mapping_misses,
                current_profile,
                frames_over_budget,
            );
        });

        {
            let mut mapping_thread_guard = self.mapping_thread.lock().unwrap();
            *mapping_thread_guard = Some(mapping_thread_handle);
        }

        #[cfg(debug_assertions)]
        debug!("[ENGINE] Mapping started");

        // Notify callback that mapping has started
        crate::notify_mapping_status_change(true);

        Ok(())
    }

    pub fn stop_mapping(&self) {
        #[cfg(debug_assertions)]
        debug!("[STOP] Stopping mapping system...");

        // Signal mapping thread to stop
        self.mapping_active.store(false, Ordering::Relaxed);

        // Wait for mapping thread to finish
        let mut mapping_thread_guard = self.mapping_thread.lock().unwrap();
        if let Some(handle) = mapping_thread_guard.take() {
            drop(mapping_thread_guard); // Release lock before joining
            let _ = handle.join(); // Wait for thread to finish

            #[cfg(debug_assertions)]
            debug!("[MAPPING] Thread stopped");
        }

        // Notify callback that mapping has stopped
        crate::notify_mapping_status_change(false);

        #[cfg(debug_assertions)]
        debug!("[STOP] Hotkey system remains active for profile management");
    }

    pub fn is_active(&self) -> bool {
        self.mapping_active.load(Ordering::Relaxed)
    }

    // Mapping loop that reuses buffers and uses ArcSwap for profile access.
    #[allow(clippy::too_many_arguments)]
    fn mapping_loop_optimized(
        mapping_active: Arc<AtomicBool>,
        wooting_sdk: Arc<&'static Mutex<Option<crate::wooting::WootingSDK>>>,
        vigem_client: Arc<&'static Mutex<Option<crate::gamepad::ViGEmClient>>>,
        frame_count: Arc<AtomicU64>,
        total_frame_time: Arc<AtomicU64>,
        max_frame_time: Arc<AtomicU64>,
        mapping_hits: Arc<AtomicU64>,
        mapping_misses: Arc<AtomicU64>,
        current_profile: Arc<ArcSwap<Option<Arc<crate::profile::profiles::CompiledProfile>>>>,
        frames_over_budget: Arc<AtomicU64>,
    ) {
        const TARGET_FPS: u64 = 120; // Target loop rate in Hz.
        const FRAME_TIME: Duration = Duration::from_micros(1_000_000 / TARGET_FPS);
        const WARN_TARGET_MICROS: u64 = 8333; // 8.33ms warn target (120 FPS)

        // Pre-allocate input buffer.
        let mut input_buffer = Vec::with_capacity(256); // Max possible keys, allocated once.

        let mut _last_frame = Instant::now(); // Track for potential future use
        #[cfg(debug_assertions)]
        let mut last_log_time = Instant::now();
        #[cfg(debug_assertions)]
        let mut last_logged_frames_over_budget: u64 = 0;

        while mapping_active.load(Ordering::Relaxed) {
            let frame_start = Instant::now();
            frame_count.fetch_add(1, Ordering::Relaxed);

            // Read inputs while reusing the pre-allocated buffer.
            let input_success = {
                let wooting_guard = wooting_sdk.as_ref().lock().unwrap();
                if let Some(ref sdk) = *wooting_guard {
                    sdk.fill_analog_inputs(&mut input_buffer).is_ok()
                } else {
                    input_buffer.clear(); // No SDK, just clear buffer.
                    false
                }
            };

            if input_success && !input_buffer.is_empty() {
                let profile_guard = current_profile.load();

                if let Some(ref profile) = profile_guard.as_ref() {
                    // Reset per-frame analog outputs.
                    let mut left_trigger_val: f64 = 0.0;
                    let mut right_trigger_val: f64 = 0.0;

                    // Process each input through active mappings.
                    let mut left_x_positive = 0.0f32;
                    let mut left_x_negative = 0.0f32;
                    let mut left_y_positive = 0.0f32;
                    let mut left_y_negative = 0.0f32;
                    let mut right_x_positive = 0.0f32;
                    let mut right_x_negative = 0.0f32;
                    let mut right_y_positive = 0.0f32;
                    let mut right_y_negative = 0.0f32;

                    // Only analog inputs are processed here; digital buttons are handled by the event manager.
                    for input in &input_buffer {
                        if let Some(compiled_mapping) =
                            profile.mappings.get(&(input.key_code as u16))
                        {
                            // Skip digital button mappings in this loop.
                            let is_analog_control = matches!(
                                compiled_mapping.gamepad_control,
                                GamepadControl::LeftStickUp
                                    | GamepadControl::LeftStickDown
                                    | GamepadControl::LeftStickLeft
                                    | GamepadControl::LeftStickRight
                                    | GamepadControl::RightStickUp
                                    | GamepadControl::RightStickDown
                                    | GamepadControl::RightStickLeft
                                    | GamepadControl::RightStickRight
                                    | GamepadControl::LeftTrigger
                                    | GamepadControl::RightTrigger
                            );

                            if is_analog_control {
                                mapping_hits.fetch_add(1, Ordering::Relaxed);

                                let processed_value =
                                    compiled_mapping.process_input(input.analog_value as f32);

                                match compiled_mapping.gamepad_control {
                                    GamepadControl::LeftStickUp => {
                                        left_y_positive = processed_value.max(left_y_positive)
                                    }
                                    GamepadControl::LeftStickDown => {
                                        left_y_negative = processed_value.max(left_y_negative)
                                    }
                                    GamepadControl::LeftStickLeft => {
                                        left_x_negative = processed_value.max(left_x_negative)
                                    }
                                    GamepadControl::LeftStickRight => {
                                        left_x_positive = processed_value.max(left_x_positive)
                                    }

                                    GamepadControl::RightStickUp => {
                                        right_y_positive = processed_value.max(right_y_positive)
                                    }
                                    GamepadControl::RightStickDown => {
                                        right_y_negative = processed_value.max(right_y_negative)
                                    }
                                    GamepadControl::RightStickLeft => {
                                        right_x_negative = processed_value.max(right_x_negative)
                                    }
                                    GamepadControl::RightStickRight => {
                                        right_x_positive = processed_value.max(right_x_positive)
                                    }

                                    GamepadControl::LeftTrigger => {
                                        left_trigger_val =
                                            (processed_value as f64).max(left_trigger_val)
                                    }
                                    GamepadControl::RightTrigger => {
                                        right_trigger_val =
                                            (processed_value as f64).max(right_trigger_val)
                                    }

                                    _ => {}
                                }
                            }
                        } else {
                            mapping_misses.fetch_add(1, Ordering::Relaxed);
                        }
                    }

                    use crate::ATOMIC_GAMEPAD_STATE;

                    let left_stick_x = (left_x_positive - left_x_negative).clamp(-1.0, 1.0);
                    let left_stick_y = (left_y_positive - left_y_negative).clamp(-1.0, 1.0);
                    let right_stick_x = (right_x_positive - right_x_negative).clamp(-1.0, 1.0);
                    let right_stick_y = (right_y_positive - right_y_negative).clamp(-1.0, 1.0);

                    ATOMIC_GAMEPAD_STATE.set_sticks(
                        left_stick_x as f64,
                        left_stick_y as f64,
                        right_stick_x as f64,
                        right_stick_y as f64,
                    );
                    ATOMIC_GAMEPAD_STATE.set_triggers(left_trigger_val, right_trigger_val);

                    // Create unified ViGEm report from atomic state (includes digital buttons from events)
                    let vigem_gamepad = ATOMIC_GAMEPAD_STATE.to_vigem_gamepad();

                    // Update ViGEm with complete state (analog + digital)
                    let mut vigem_guard = vigem_client.as_ref().lock().unwrap();
                    if let Some(ref mut client) = *vigem_guard {
                        if let Err(e) = client.update_from_vigem_gamepad(&vigem_gamepad) {
                            error!("[ENGINE] ViGEm update failed: {}", e);
                        }
                    }
                }
            }

            let frame_time = frame_start.elapsed();
            let frame_micros = frame_time.as_micros() as u64;

            total_frame_time.fetch_add(frame_micros, Ordering::Relaxed);

            let mut current_max = max_frame_time.load(Ordering::Relaxed);
            while frame_micros > current_max {
                match max_frame_time.compare_exchange_weak(
                    current_max,
                    frame_micros,
                    Ordering::Relaxed,
                    Ordering::Relaxed,
                ) {
                    Ok(_) => break,
                    Err(actual) => current_max = actual,
                }
            }

            if frame_micros > WARN_TARGET_MICROS {
                frames_over_budget.fetch_add(1, Ordering::Relaxed);
            }

            if frame_time < FRAME_TIME {
                thread::sleep(FRAME_TIME - frame_time);
            }

            #[cfg(debug_assertions)]
            {
                if last_log_time.elapsed() >= Duration::from_secs(10) {
                    let current_frames_over_budget = frames_over_budget.load(Ordering::Relaxed);
                    let current_total_frames = frame_count.load(Ordering::Relaxed);
                    let new_frames_over_budget =
                        current_frames_over_budget - last_logged_frames_over_budget;

                    debug!(
                        "[PERF] Last 10s: {} frames over budget ({:.2}%)",
                        new_frames_over_budget,
                        if current_total_frames > 0 {
                            (new_frames_over_budget as f64 / (TARGET_FPS * 10) as f64) * 100.0
                        } else {
                            0.0
                        }
                    );

                    last_log_time = Instant::now();
                    last_logged_frames_over_budget = current_frames_over_budget;
                }
            }

            _last_frame = frame_start;
        }

        #[cfg(debug_assertions)]
        debug!("[INFO] Mapping loop stopped");
    }
}
