use crate::api::types::{ComponentState, ComponentStatus, PerformanceMetrics};
use crate::mapping::MAPPING_ENGINE;
use crate::{EVENT_INPUT_MANAGER, VIGEM_INIT_STATUS, WOOTING_INIT_STATUS};

/// Collect component health metrics used for dependency checking.
pub fn get_performance_metrics() -> PerformanceMetrics {
    let wooting_state = WOOTING_INIT_STATUS
        .read()
        .ok()
        .and_then(|status| status.clone())
        .map(|result| match result {
            Ok(_) => ComponentState::ok(),
            Err(err) => ComponentState::missing(err),
        })
        .unwrap_or_else(|| ComponentState::not_initialized());

    let vigem_state = VIGEM_INIT_STATUS
        .read()
        .ok()
        .and_then(|status| status.clone())
        .map(|result| match result {
            Ok(_) => ComponentState::ok(),
            Err(err) => ComponentState::missing(err),
        })
        .unwrap_or_else(|| ComponentState::not_initialized());

    let mapping_active = match MAPPING_ENGINE.lock() {
        Ok(engine_guard) => engine_guard.as_ref().map_or(false, |e| e.is_active()),
        Err(_) => false,
    };

    PerformanceMetrics {
        components: ComponentStatus {
            wooting_sdk: wooting_state,
            vigem_client: vigem_state,
            mapping_thread: mapping_active,
            hotkey_manager: true,
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
