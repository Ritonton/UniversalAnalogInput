use std::sync::Arc;
#[cfg(debug_assertions)]
use std::time::Instant;

use crate::conversions::metadata_hotkey_to_struct;
use crate::profile::{
    update_systems_after_profile_switch, ProfileError, ProfileManager, ProfileMetadata,
    SubProfileMetadata,
};
use crate::ui_notifier::notify_sub_profile_switch;
use crate::{EVENT_INPUT_MANAGER, PROFILE_MANAGER};
use log::warn;
use uuid::Uuid;

use super::{EventInputManager, HotkeyCallback, ProfileCycleCallback};

/// Registers and executes profile/sub-profile hotkeys based on stored metadata.
pub struct HotkeyManager {
    switch_callback: HotkeyCallback,
    cycle_callback: ProfileCycleCallback,
}

impl HotkeyManager {
    /// Create a new hotkey manager with callbacks that switch profiles and notify the UI.
    pub fn new() -> Self {
        let switch_callback: HotkeyCallback = Arc::new(
            move |profile_id, sub_profile_id, _profile_name, _sub_profile_name| {
                #[cfg(debug_assertions)]
                let switch_start = Instant::now();

                let result = {
                    let mut manager_guard = PROFILE_MANAGER.lock().unwrap();
                    if let Some(ref mut manager) = *manager_guard {
                        let current_profile_id = manager.get_current_profile_id();
                        if current_profile_id != Some(profile_id) {
                            #[cfg(debug_assertions)]
                            {
                                let profile_name = manager
                                    .get_profile_metadata_by_id(&profile_id)
                                    .map(|meta| meta.name.clone())
                                    .unwrap_or_else(|| "unknown".to_string());
                                let sub_name = manager
                                    .get_sub_profile_metadata_for_profile(&profile_id)
                                    .into_iter()
                                    .find(|meta| meta.id == sub_profile_id)
                                    .map(|meta| meta.name)
                                    .unwrap_or_else(|| "unknown".to_string());
                                warn!(
                                    "[HOTKEY] Ignored sub-profile hotkey '{}' :: '{}' (inactive)",
                                    profile_name, sub_name
                                );
                            }
                            return;
                        }
                        manager.switch_profile(&profile_id, &sub_profile_id)
                    } else {
                        Err(crate::profile::ProfileError::NoProfileLoaded)
                    }
                };

                match result {
                    Ok(_) => {
                        update_systems_after_profile_switch();
                        notify_sub_profile_switch(profile_id, sub_profile_id);

                        #[cfg(debug_assertions)]
                        {
                            let switch_time = switch_start.elapsed();
                            let manager_guard = PROFILE_MANAGER.lock().unwrap();
                            let (profile_name, sub_name) = manager_guard
                                .as_ref()
                                .map(|mgr| {
                                    let profile = mgr
                                        .get_profile_metadata_by_id(&profile_id)
                                        .map(|meta| meta.name.clone())
                                        .unwrap_or_else(|| "unknown".to_string());
                                    let sub = mgr
                                        .get_sub_profile_metadata_for_profile(&profile_id)
                                        .into_iter()
                                        .find(|meta| meta.id == sub_profile_id)
                                        .map(|meta| meta.name)
                                        .unwrap_or_else(|| "unknown".to_string());
                                    (profile, sub)
                                })
                                .unwrap_or_else(|| ("unknown".to_string(), "unknown".to_string()));
                            warn!(
                                "[HOTKEY-CALLBACK] INSTANT switch '{}' :: '{}' completed in {:?}",
                                profile_name, sub_name, switch_time
                            );
                        }
                    }
                    Err(e) => {
                        warn!("[HOTKEY-CALLBACK] Switch failed: {}", e);
                    }
                }
            },
        );

        let cycle_callback: ProfileCycleCallback = Arc::new(move |profile_id| {
            #[cfg(debug_assertions)]
            let switch_start = Instant::now();

            let result = {
                let mut manager_guard = PROFILE_MANAGER.lock().unwrap();
                if let Some(ref mut manager) = *manager_guard {
                    let current_profile_id = manager.get_current_profile_id();
                    if current_profile_id != Some(profile_id) {
                        #[cfg(debug_assertions)]
                        {
                            let profile_name = manager
                                .get_profile_metadata_by_id(&profile_id)
                                .map(|meta| meta.name.clone())
                                .unwrap_or_else(|| "unknown".to_string());
                            warn!(
                                "[HOTKEY] Ignored cycle hotkey for profile '{}' (inactive)",
                                profile_name
                            );
                        }
                        return;
                    }
                    manager.cycle_sub_profile(&profile_id)
                } else {
                    Err(ProfileError::NoProfileLoaded)
                }
            };

            match result {
                Ok((next_sub_profile_id, sub_profile_name)) => {
                    update_systems_after_profile_switch();
                    notify_sub_profile_switch(profile_id, next_sub_profile_id);

                    #[cfg(debug_assertions)]
                    {
                        let switch_time = switch_start.elapsed();
                        let manager_guard = PROFILE_MANAGER.lock().unwrap();
                        let profile_name = manager_guard
                            .as_ref()
                            .and_then(|mgr| mgr.get_profile_metadata_by_id(&profile_id))
                            .map(|meta| meta.name.clone())
                            .unwrap_or_else(|| "unknown".to_string());
                        warn!(
                            "[HOTKEY-CALLBACK] Cycle '{}' completed in {:?} -> '{}'",
                            profile_name, switch_time, sub_profile_name
                        );
                    }
                }
                Err(e) => {
                    warn!("[HOTKEY-CALLBACK] Cycle failed: {}", e);
                }
            }
        });

        Self {
            switch_callback,
            cycle_callback,
        }
    }

    /// Register all hotkeys defined in profile metadata with the event manager.
    /// Returns the number of successfully registered hotkeys.
    pub fn register_from_metadata(
        &self,
        profile_manager: &ProfileManager,
        event_manager: &mut EventInputManager,
    ) -> usize {
        event_manager.clear_hotkeys();
        self.fill_missing_hotkeys(profile_manager, event_manager)
    }

    fn register_profile_hotkeys(
        &self,
        event_manager: &mut EventInputManager,
        profile_meta: &ProfileMetadata,
        sub_metas: &[SubProfileMetadata],
    ) -> usize {
        let mut registered = 0usize;

        for sub_meta in sub_metas {
            if let Some(hotkey_str) = sub_meta.hotkey.as_deref() {
                if let Some(hotkey) = metadata_hotkey_to_struct(hotkey_str) {
                    if let Ok(true) = event_manager.register_switch_hotkey(
                        hotkey,
                        profile_meta.id,
                        profile_meta.name.clone(),
                        sub_meta.id,
                        sub_meta.name.clone(),
                        Arc::clone(&self.switch_callback),
                    ) {
                        registered += 1;
                    }
                }
            }
        }

        if !sub_metas.is_empty() {
            if let Some(hotkey_str) = profile_meta.hotkey.as_deref() {
                if let Some(hotkey) = metadata_hotkey_to_struct(hotkey_str) {
                    if let Ok(true) = event_manager.register_cycle_hotkey(
                        hotkey,
                        profile_meta.id,
                        Arc::clone(&self.cycle_callback),
                    ) {
                        registered += 1;
                    }
                }
            }
        }

        registered
    }

    fn sync_profile_from_metadata(
        &self,
        profile_manager: &ProfileManager,
        event_manager: &mut EventInputManager,
        profile_id: &Uuid,
    ) -> usize {
        event_manager.remove_hotkeys_for_profile(*profile_id);

        let Some(profile_meta) = profile_manager
            .get_profile_metadata_by_id(profile_id)
            .cloned()
        else {
            return 0;
        };

        let sub_metas = profile_manager.get_sub_profile_metadata_for_profile(profile_id);
        self.register_profile_hotkeys(event_manager, &profile_meta, &sub_metas)
    }

    fn fill_missing_hotkeys(
        &self,
        profile_manager: &ProfileManager,
        event_manager: &mut EventInputManager,
    ) -> usize {
        let profile_count = profile_manager.get_profile_metadata_count();
        let mut registered = 0usize;
        for index in 0..profile_count {
            if let Some(profile_meta) = profile_manager.get_profile_metadata(index) {
                let sub_metas =
                    profile_manager.get_sub_profile_metadata_for_profile(&profile_meta.id);
                registered +=
                    self.register_profile_hotkeys(event_manager, profile_meta, &sub_metas);
            }
        }
        registered
    }
}

/// Rebuild hotkey registrations from metadata using global state.
pub fn rebuild_hotkeys_from_metadata() {
    let hotkey_manager = HotkeyManager::new();

    let manager_guard = PROFILE_MANAGER.lock().unwrap();
    if let Some(ref manager) = *manager_guard {
        let mut event_guard = EVENT_INPUT_MANAGER.lock().unwrap();
        if let Some(ref mut event_manager) = *event_guard {
            let registered = hotkey_manager.register_from_metadata(manager, event_manager);
            #[cfg(debug_assertions)]
            warn!(
                "[HOTKEY] Rebuilt hotkeys from metadata ({} entries)",
                registered
            );
        }
    }
}

pub fn sync_hotkeys_for_profile(profile_id: &Uuid) {
    let hotkey_manager = HotkeyManager::new();
    let manager_guard = PROFILE_MANAGER.lock().unwrap();
    if let Some(ref manager) = *manager_guard {
        let mut event_guard = EVENT_INPUT_MANAGER.lock().unwrap();
        if let Some(ref mut event_manager) = *event_guard {
            hotkey_manager.sync_profile_from_metadata(manager, event_manager, profile_id);
            hotkey_manager.fill_missing_hotkeys(manager, event_manager);
        }
    }
}

pub fn remove_hotkeys_for_profile(profile_id: &Uuid) {
    let hotkey_manager = HotkeyManager::new();

    {
        let mut event_guard = EVENT_INPUT_MANAGER.lock().unwrap();
        if let Some(ref mut event_manager) = *event_guard {
            event_manager.remove_hotkeys_for_profile(*profile_id);
        } else {
            return;
        }
    }

    let manager_guard = PROFILE_MANAGER.lock().unwrap();
    if let Some(ref manager) = *manager_guard {
        let mut event_guard = EVENT_INPUT_MANAGER.lock().unwrap();
        if let Some(ref mut event_manager) = *event_guard {
            hotkey_manager.fill_missing_hotkeys(manager, event_manager);
        }
    }
}
