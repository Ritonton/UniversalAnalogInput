use crate::{mapping::MAPPING_ENGINE, ATOMIC_GAMEPAD_STATE, EVENT_INPUT_MANAGER, PROFILE_MANAGER};

/// Single source of truth for refreshing systems after a profile switch.
/// ArcSwap ensures thread-safe updates without pausing the mapping loop.
pub fn update_systems_after_profile_switch() {
    ATOMIC_GAMEPAD_STATE.set_buttons(0);

    let manager_guard = PROFILE_MANAGER.lock().unwrap();
    if let Some(ref manager) = *manager_guard {
        if let Some(current_profile) = manager.get_current_profile() {
            let mut event_guard = EVENT_INPUT_MANAGER.lock().unwrap();
            if let Some(ref mut event_manager) = *event_guard {
                event_manager.update_button_callbacks(&current_profile);
            }

            // ArcSwap handles thread-safe profile updates atomically
            let engine_guard = MAPPING_ENGINE.lock().unwrap();
            if let Some(ref engine) = *engine_guard {
                engine.update_cached_profile(current_profile);
            }
        } else {
            let engine_guard = MAPPING_ENGINE.lock().unwrap();
            if let Some(ref engine) = *engine_guard {
                engine.clear_cached_profile();
            }
        }
    }
}
