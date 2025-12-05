use crate::api::types::{
    CacheMetrics, ComponentState, ComponentStatus, PerformanceMetrics, SystemMetrics,
};
use crate::mapping::MAPPING_ENGINE;
use crate::{EVENT_INPUT_MANAGER, PROFILE_MANAGER, VIGEM_INIT_STATUS, WOOTING_INIT_STATUS};

/// Collect performance metrics for diagnostics dashboards.
pub fn get_performance_metrics() -> PerformanceMetrics {
    // Get detailed initialization status for Wooting SDK
    let wooting_state = WOOTING_INIT_STATUS
        .read()
        .ok()
        .and_then(|status| status.clone())
        .map(|result| match result {
            Ok(_) => ComponentState::ok(),
            Err(err) => ComponentState::missing(err),
        })
        .unwrap_or_else(|| ComponentState::not_initialized());

    // Get detailed initialization status for ViGEm
    let vigem_state = VIGEM_INIT_STATUS
        .read()
        .ok()
        .and_then(|status| status.clone())
        .map(|result| match result {
            Ok(_) => ComponentState::ok(),
            Err(err) => ComponentState::missing(err),
        })
        .unwrap_or_else(|| ComponentState::not_initialized());

    let (
        mapping_active,
        measured_fps,
        _avg_frame_time_ms,
        max_frame_time_ms,
        mapping_hits,
        mapping_misses,
    ) = match MAPPING_ENGINE.lock() {
        Ok(engine_guard) => {
            if let Some(engine) = engine_guard.as_ref() {
                let (fps, avg_ms, max_ms, hits, misses) = engine.get_performance_metrics();
                (engine.is_active(), fps, avg_ms, max_ms, hits, misses)
            } else {
                (false, 0.0, 0.0, 0.0, 0, 0)
            }
        }
        Err(_) => (false, 0.0, 0.0, 0.0, 0, 0),
    };

    let ultra_performance = measured_fps >= 240.0 && max_frame_time_ms <= 4.0;

    let (
        hotkey_detection_hz,
        profile_switch_time_us,
        total_profiles,
        total_sub_profiles,
        memory_usage_kb,
    ) = match PROFILE_MANAGER.lock() {
        Ok(guard) => {
            if let Some(manager) = guard.as_ref() {
                let profile_count = manager.get_profile_names().len();
                let sub_profile_count = profile_count * 2; // conservative estimate
                let memory_estimate =
                    (profile_count * 8) + (mapping_hits + mapping_misses) as usize / 100;
                (
                    120.0,
                    500u32,
                    profile_count as u32,
                    sub_profile_count as u32,
                    memory_estimate as u32,
                )
            } else {
                (0.0, 0u32, 0u32, 0u32, 0u32)
            }
        }
        Err(_) => (0.0, 0u32, 0u32, 0u32, 0u32),
    };

    PerformanceMetrics {
        system: SystemMetrics {
            mapping_fps: measured_fps,
            hotkey_detection_hz,
            profile_switch_time_us,
            ultra_performance_mode: ultra_performance,
        },
        components: ComponentStatus {
            wooting_sdk: wooting_state,
            vigem_client: vigem_state,
            mapping_thread: mapping_active,
            hotkey_manager: true,
        },
        cache: CacheMetrics {
            total_profiles,
            total_sub_profiles,
            current_active: mapping_active,
            memory_usage_kb,
            switch_method: "atomic_pointer".to_string(),
        },
    }
}

/// Pause hotkey processing.
pub fn suspend_hotkeys() {
    if let Ok(mut event_guard) = EVENT_INPUT_MANAGER.lock() {
        if let Some(manager) = event_guard.as_mut() {
            manager.suspend_hotkeys();
        }
    }
}

/// Resume hotkey processing.
pub fn resume_hotkeys() {
    if let Ok(mut event_guard) = EVENT_INPUT_MANAGER.lock() {
        if let Some(manager) = event_guard.as_mut() {
            manager.resume_hotkeys();
        }
    }
}
