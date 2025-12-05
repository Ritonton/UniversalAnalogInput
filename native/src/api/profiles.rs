use crate::api::types::{MappingDto, ProfileMetadataDto, SubProfileMetadataDto};
use crate::conversions::{
    gamepad_control_to_name, get_all_gamepad_control_names, get_all_supported_key_names,
    name_to_gamepad_control, name_to_response_curve, response_curve_to_name,
};
use crate::input::{remove_hotkeys_for_profile, sync_hotkeys_for_profile};
use crate::profile::profiles::{CurveParams, KeyMapping};
use crate::profile::{
    update_systems_after_profile_switch, ProfileManager, SubProfileDeletionOutcome,
};
use crate::PROFILE_MANAGER;
use std::sync::MutexGuard;
use uuid::Uuid;

const MANAGER_NOT_INITIALIZED: &str = "Profile manager not initialized";

fn lock_manager() -> Result<MutexGuard<'static, Option<ProfileManager>>, String> {
    PROFILE_MANAGER
        .lock()
        .map_err(|e| format!("Profile manager lock poisoned: {}", e))
}

fn manager_unavailable() -> String {
    MANAGER_NOT_INITIALIZED.to_string()
}

/// Number of profiles available from metadata.
pub fn get_profile_metadata_count() -> usize {
    match PROFILE_MANAGER.lock() {
        Ok(guard) => guard
            .as_ref()
            .map(|manager| manager.get_profile_metadata_count())
            .unwrap_or(0),
        Err(_) => 0,
    }
}

/// Retrieve profile metadata by index.
pub fn get_profile_metadata(index: usize) -> Option<ProfileMetadataDto> {
    let guard = PROFILE_MANAGER.lock().ok()?;
    let manager = guard.as_ref()?;
    manager
        .get_profile_metadata(index)
        .map(|meta| ProfileMetadataDto {
            id: meta.id.to_bytes_le(),
            name: meta.name.clone(),
            description: meta.description.clone(),
            sub_profile_count: meta.sub_profile_count as u32,
            created_at: meta.created_at,
            modified_at: meta.modified_at,
            hotkey: meta.hotkey.clone(),
        })
}

/// Retrieve sub-profile metadata for a given profile/sub index.
pub fn get_sub_profile_metadata(
    profile_index: usize,
    sub_index: usize,
) -> Option<SubProfileMetadataDto> {
    let guard = PROFILE_MANAGER.lock().ok()?;
    let manager = guard.as_ref()?;
    manager
        .get_sub_profile_metadata(profile_index, sub_index)
        .map(|meta| SubProfileMetadataDto {
            id: meta.id.to_bytes_le(),
            parent_profile_id: meta.parent_profile_id.to_bytes_le(),
            name: meta.name.clone(),
            description: meta.description.clone(),
            hotkey: meta.hotkey.clone(),
            created_at: meta.created_at,
            modified_at: meta.modified_at,
        })
}

/// Switch currently active profile and sub-profile.
pub fn switch_profile(profile_id: &Uuid, sub_profile_id: &Uuid) -> Result<(), String> {
    {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .switch_profile(profile_id, sub_profile_id)
            .map_err(|e| e.to_string())?;
    }

    update_systems_after_profile_switch();
    Ok(())
}

/// Return the number of mappings in the active sub-profile.
pub fn get_current_mapping_count() -> usize {
    PROFILE_MANAGER
        .lock()
        .ok()
        .and_then(|guard| {
            guard
                .as_ref()
                .map(|manager| manager.get_current_mapping_count())
        })
        .unwrap_or(0)
}

/// Retrieve a mapping from the active sub-profile.
pub fn get_current_mapping_info(index: usize) -> Option<MappingDto> {
    let guard = PROFILE_MANAGER.lock().ok()?;
    let manager = guard.as_ref()?;
    manager.get_current_mapping(index).map(|mapping| {
        let response_curve = response_curve_to_name(&mapping.response_curve).to_string();
        let gamepad_control = gamepad_control_to_name(&mapping.gamepad_control).to_string();
        let custom_points: Vec<(f32, f32)> = mapping
            .curve_params
            .custom_points
            .iter()
            .cloned()
            .take(16)
            .collect();
        let custom_point_count = custom_points.len() as u32;

        MappingDto {
            key_name: mapping.key_name.clone(),
            gamepad_control,
            response_curve,
            dead_zone_inner: mapping.dead_zone_inner,
            dead_zone_outer: mapping.dead_zone_outer,
            use_smooth_curve: mapping.curve_params.use_smooth_interpolation,
            custom_point_count,
            custom_points,
            created_at: mapping.created_at,
        }
    })
}

/// Update or insert a mapping in the active sub-profile.
pub fn set_mapping(mapping: MappingDto) -> Result<(), String> {
    let gamepad_control = name_to_gamepad_control(&mapping.gamepad_control)
        .ok_or_else(|| format!("Invalid gamepad control: {}", mapping.gamepad_control))?;
    let response_curve = name_to_response_curve(&mapping.response_curve);

    let points_available = mapping.custom_points.len() as u32;
    let point_count = mapping.custom_point_count.min(points_available).min(16);
    let custom_points: Vec<(f32, f32)> = mapping
        .custom_points
        .iter()
        .take(point_count as usize)
        .cloned()
        .collect();

    let now = crate::profile::profiles::now_timestamp();
    let created_at = if mapping.created_at == 0 {
        now
    } else {
        mapping.created_at
    };

    let key_mapping = KeyMapping {
        key_name: mapping.key_name.clone(),
        gamepad_control,
        response_curve,
        dead_zone_inner: mapping.dead_zone_inner,
        dead_zone_outer: mapping.dead_zone_outer,
        curve_params: CurveParams {
            use_smooth_interpolation: mapping.use_smooth_curve,
            custom_points,
        },
        created_at,
        modified_at: now,
    };

    {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .set_current_mapping(key_mapping)
            .map_err(|e| e.to_string())?;
    }

    update_systems_after_profile_switch();
    Ok(())
}

/// Remove a mapping by key name from the active sub-profile.
pub fn remove_mapping(key_name: &str) -> Result<bool, String> {
    let removed = {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .remove_current_mapping(key_name)
            .map_err(|e| e.to_string())?
    };

    if removed {
        update_systems_after_profile_switch();
    }

    Ok(removed)
}

/// Permanently delete a profile by UUID.
pub fn delete_profile(profile_id: &Uuid) -> Result<(), String> {
    {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .delete_profile(profile_id)
            .map_err(|e| e.to_string())?;
    }

    remove_hotkeys_for_profile(profile_id);
    Ok(())
}

/// Rename a profile and refresh hotkey registrations.
pub fn rename_profile(profile_id: &Uuid, new_name: &str) -> Result<(), String> {
    {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .rename_profile(profile_id, new_name)
            .map_err(|e| e.to_string())?;
    }

    sync_hotkeys_for_profile(profile_id);
    Ok(())
}

/// Update profile description.
pub fn update_profile_description(profile_id: &Uuid, description: &str) -> Result<(), String> {
    let mut guard = lock_manager()?;
    let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
    manager
        .update_profile_description(profile_id, description)
        .map_err(|e| e.to_string())
}

fn optional_slice(input: &str) -> Option<&str> {
    let trimmed = input.trim();
    if trimmed.is_empty() {
        None
    } else {
        Some(trimmed)
    }
}

/// Add a new sub-profile to an existing profile.
pub fn add_sub_profile(
    profile_id: &Uuid,
    name: &str,
    description: &str,
    hotkey: &str,
) -> Result<(), String> {
    {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .add_sub_profile(
                profile_id,
                name,
                optional_slice(description),
                optional_slice(hotkey),
            )
            .map_err(|e| e.to_string())?;
    }

    sync_hotkeys_for_profile(profile_id);
    Ok(())
}

/// Rename an existing sub-profile.
pub fn rename_sub_profile(
    profile_id: &Uuid,
    sub_profile_id: &Uuid,
    new_name: &str,
) -> Result<(), String> {
    {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .rename_sub_profile(profile_id, sub_profile_id, new_name)
            .map_err(|e| e.to_string())?;
    }

    sync_hotkeys_for_profile(profile_id);
    Ok(())
}

/// Delete a sub-profile. Returns whether the parent profile was also removed.
pub fn delete_sub_profile(
    profile_id: &Uuid,
    sub_profile_id: &Uuid,
) -> Result<SubProfileDeletionOutcome, String> {
    let outcome = {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .delete_sub_profile(profile_id, sub_profile_id)
            .map_err(|e| e.to_string())?
    };

    match outcome {
        SubProfileDeletionOutcome::SubProfileRemoved => {
            sync_hotkeys_for_profile(profile_id);
        }
        SubProfileDeletionOutcome::ProfileRemoved => {
            remove_hotkeys_for_profile(profile_id);
            update_systems_after_profile_switch();
        }
    }

    Ok(outcome)
}

/// Create a new profile.
pub fn create_profile(name: &str, description: &str) -> Result<Uuid, String> {
    let profile_id = {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .create_profile(name, description)
            .map_err(|e| e.to_string())?
    };

    sync_hotkeys_for_profile(&profile_id);
    Ok(profile_id)
}

/// Update the profile cycling hotkey.
pub fn update_profile_hotkey(profile_id: &Uuid, hotkey: &str) -> Result<(), String> {
    {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .set_profile_hotkey(profile_id, optional_slice(hotkey))
            .map_err(|e| e.to_string())?;
    }

    sync_hotkeys_for_profile(profile_id);
    Ok(())
}

/// Update a sub-profile hotkey.
pub fn update_sub_profile_hotkey(
    profile_id: &Uuid,
    sub_profile_id: &Uuid,
    hotkey: &str,
) -> Result<(), String> {
    {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .set_sub_profile_hotkey(profile_id, sub_profile_id, optional_slice(hotkey))
            .map_err(|e| e.to_string())?;
    }

    sync_hotkeys_for_profile(profile_id);
    Ok(())
}

/// Save profile to a file path.
pub fn save_profile_to_file(profile_id: &Uuid, file_path: &str) -> Result<(), String> {
    let guard = lock_manager()?;
    let manager = guard.as_ref().ok_or_else(manager_unavailable)?;
    manager
        .save_profile_to_file(profile_id, file_path)
        .map_err(|e| e.to_string())
}

/// Import a profile from a file path.
pub fn load_profile_from_file(file_path: &str) -> Result<Uuid, String> {
    let profile_id = {
        let mut guard = lock_manager()?;
        let manager = guard.as_mut().ok_or_else(manager_unavailable)?;
        manager
            .load_profile_from_file(file_path)
            .map_err(|e| e.to_string())?
    };

    sync_hotkeys_for_profile(&profile_id);
    Ok(profile_id)
}

/// Enumerate all supported key names.
pub fn get_supported_keys() -> Vec<String> {
    get_all_supported_key_names()
        .into_iter()
        .map(|name| name.to_string())
        .collect()
}

/// Enumerate all supported gamepad control names.
pub fn get_gamepad_controls() -> Vec<String> {
    get_all_gamepad_control_names()
        .into_iter()
        .map(|name| name.to_string())
        .collect()
}

/// Get a single supported key name by index.
pub fn get_supported_key_name(index: usize) -> Option<String> {
    get_all_supported_key_names()
        .get(index)
        .map(|name| name.to_string())
}

/// Get a single supported gamepad control name by index.
pub fn get_gamepad_control_name(index: usize) -> Option<String> {
    get_all_gamepad_control_names()
        .get(index)
        .map(|name| name.to_string())
}
