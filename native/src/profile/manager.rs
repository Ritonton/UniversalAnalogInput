use crate::conversions::{hotkey_to_metadata_string, metadata_hotkey_to_struct};
use crate::profile::profiles::*;
use log::{info, warn};
use serde_json;
use std::collections::HashMap;
use std::fs;
use std::path::PathBuf;
use std::sync::Arc;
use std::time::SystemTime;
use thiserror::Error;
use uuid::Uuid;

#[derive(Error, Debug)]
pub enum ProfileError {
    #[error("Profile not found: {0}")]
    ProfileNotFound(String),
    #[error("Sub-profile not found: {0}")]
    SubProfileNotFound(String),
    #[error("No profile loaded")]
    NoProfileLoaded,
    #[error("No sub-profile active")]
    NoSubProfileActive,
    #[error("IO error: {0}")]
    IoError(#[from] std::io::Error),
    #[error("JSON error: {0}")]
    JsonError(#[from] serde_json::Error),
    #[error("Config directory not accessible")]
    ConfigDirError,
    #[error("Profile '{0}' has no sub-profiles")]
    EmptyProfile(String),
}

/// Outcome of a sub-profile delete operation.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SubProfileDeletionOutcome {
    /// A sub-profile was removed but the parent profile still exists.
    SubProfileRemoved,
    /// The parent profile was deleted because it no longer contained sub-profiles.
    ProfileRemoved,
}

/// Lightweight metadata for profiles (loaded at startup only)
#[derive(Debug, Clone)]
pub struct ProfileMetadata {
    pub id: Uuid,
    pub name: String,
    pub description: String,
    pub file_path: PathBuf,
    pub sub_profile_count: usize,
    pub modified_time: SystemTime,
    pub created_at: u64,  // Creation timestamp (Unix)
    pub modified_at: u64, // Modification timestamp (Unix)
    pub hotkey: Option<String>,
}

/// Lightweight metadata for sub-profiles (loaded at startup only)
#[derive(Debug, Clone)]
pub struct SubProfileMetadata {
    pub id: Uuid,
    pub parent_profile_id: Uuid,
    pub name: String,
    pub description: String,
    pub hotkey: Option<String>,
    pub created_at: u64,  // Creation timestamp (Unix)
    pub modified_at: u64, // Modification timestamp (Unix)
}

/// Profile manager that keeps one profile loaded in memory at a time.
pub struct ProfileManager {
    config_dir: PathBuf,

    // Metadata loaded at startup (stable until restart).
    profile_metadata: HashMap<Uuid, ProfileMetadata>,
    sub_profile_metadata: HashMap<Uuid, SubProfileMetadata>,

    // One profile loaded at a time.
    current_profile: Option<GameProfile>,
    // All sub-profiles of the current profile compiled for instant switching.
    compiled_sub_profiles: HashMap<Uuid, Arc<CompiledProfile>>,

    // Current active sub-profile for the mapping engine.
    current_sub_profile_id: Option<Uuid>,
}

impl ProfileManager {
    pub fn new() -> Result<Self, ProfileError> {
        let config_dir = get_config_directory()?;
        fs::create_dir_all(&config_dir)?;

        let mut manager = Self {
            config_dir,
            profile_metadata: HashMap::new(),
            sub_profile_metadata: HashMap::new(),
            current_profile: None,
            compiled_sub_profiles: HashMap::new(),
            current_sub_profile_id: None,
        };

        // Load metadata only for faster startup.
        manager.load_metadata()?;

        // Create default profile if none exist
        if manager.profile_metadata.is_empty() {
            manager.create_default_profile()?;
        }

        Ok(manager)
    }

    /// Load only metadata from all profile files to keep startup quick.
    fn load_metadata(&mut self) -> Result<(), ProfileError> {
        let profiles_dir = self.config_dir.join("profiles");
        if !profiles_dir.exists() {
            fs::create_dir_all(&profiles_dir)?;
            return Ok(());
        }

        for entry in fs::read_dir(&profiles_dir)? {
            let entry = entry?;
            let path = entry.path();

            if path.extension().and_then(|s| s.to_str()) == Some("json") {
                if let Ok(content) = fs::read_to_string(&path) {
                    if let Ok(mut profile) = serde_json::from_str::<GameProfile>(&content) {
                        ensure_profile_ids(&mut profile);

                        if profile.sub_profiles.is_empty() {
                            warn!(
                                "[METADATA] Removing profile '{}' ({}) - no sub-profiles present",
                                profile.name, profile.id
                            );
                            if let Err(err) = fs::remove_file(&path) {
                                warn!(
                                    "[METADATA] Failed to remove empty profile file {:?}: {}",
                                    path, err
                                );
                            }
                            continue;
                        }

                        // Extract metadata only
                        let profile_meta = ProfileMetadata {
                            id: profile.id,
                            name: profile.name.clone(),
                            description: profile.description.clone(),
                            file_path: path.clone(),
                            sub_profile_count: profile.sub_profiles.len(),
                            modified_time: entry
                                .metadata()?
                                .modified()
                                .unwrap_or(SystemTime::now()),
                            created_at: profile.created_at,
                            modified_at: profile.modified_at,
                            hotkey: profile
                                .hotkey
                                .as_ref()
                                .map(|hk| hotkey_to_metadata_string(hk)),
                        };

                        // Extract sub-profile metadata
                        for sub_profile in &profile.sub_profiles {
                            let sub_meta = SubProfileMetadata {
                                id: sub_profile.id,
                                parent_profile_id: profile.id,
                                name: sub_profile.name.clone(),
                                description: sub_profile.description.clone(),
                                hotkey: sub_profile
                                    .hotkey
                                    .as_ref()
                                    .map(|hk| hotkey_to_metadata_string(hk)),
                                created_at: sub_profile.created_at,
                                modified_at: sub_profile.modified_at,
                            };
                            self.sub_profile_metadata.insert(sub_profile.id, sub_meta);
                        }

                        self.profile_metadata.insert(profile.id, profile_meta);
                    }
                }
            }
        }

        info!(
            "[METADATA] Loaded {} profiles metadata",
            self.profile_metadata.len()
        );
        Ok(())
    }

    fn create_default_profile(&mut self) -> Result<(), ProfileError> {
        let mut profile = GameProfile::new("Default Game".to_string());
        profile.description = "Default gaming profile with WASD movement".to_string();
        profile.hotkey = Some(HotKey {
            key_name: "F2".to_string(),
            modifiers: 0,
        });

        let mut sub_profile = SubProfile::default();
        sub_profile.name = "Movement".to_string();
        sub_profile.description = "Basic WASD movement controls".to_string();
        sub_profile.hotkey = Some(HotKey {
            key_name: "F1".to_string(),
            modifiers: 0,
        });

        // Add WASD mappings
        let base_ts = crate::profile::profiles::now_timestamp();
        sub_profile.mappings = vec![
            KeyMapping {
                key_name: "W".to_string(),
                gamepad_control: GamepadControl::LeftStickUp,
                response_curve: ResponseCurve::Linear,
                dead_zone_inner: 0.05,
                dead_zone_outer: 0.95,
                curve_params: CurveParams::default(),
                created_at: base_ts,
                modified_at: base_ts,
            },
            KeyMapping {
                key_name: "A".to_string(),
                gamepad_control: GamepadControl::LeftStickLeft,
                response_curve: ResponseCurve::Linear,
                dead_zone_inner: 0.05,
                dead_zone_outer: 0.95,
                curve_params: CurveParams::default(),
                created_at: base_ts + 1,
                modified_at: base_ts + 1,
            },
            KeyMapping {
                key_name: "S".to_string(),
                gamepad_control: GamepadControl::LeftStickDown,
                response_curve: ResponseCurve::Linear,
                dead_zone_inner: 0.05,
                dead_zone_outer: 0.95,
                curve_params: CurveParams::default(),
                created_at: base_ts + 2,
                modified_at: base_ts + 2,
            },
            KeyMapping {
                key_name: "D".to_string(),
                gamepad_control: GamepadControl::LeftStickRight,
                response_curve: ResponseCurve::Linear,
                dead_zone_inner: 0.05,
                dead_zone_outer: 0.95,
                curve_params: CurveParams::default(),
                created_at: base_ts + 3,
                modified_at: base_ts + 3,
            },
        ];

        profile.sub_profiles = vec![sub_profile];
        ensure_profile_ids(&mut profile);

        self.save_profile(&profile)?;
        self.add_profile_to_metadata(&profile)?;

        Ok(())
    }

    fn save_profile(&self, profile: &GameProfile) -> Result<(), ProfileError> {
        let profiles_dir = self.config_dir.join("profiles");
        fs::create_dir_all(&profiles_dir)?;

        let filename = sanitize_filename(&profile.name) + ".json";
        let path = profiles_dir.join(filename);

        let json = serde_json::to_string_pretty(profile)?;
        fs::write(path, json)?;

        Ok(())
    }

    fn add_profile_to_metadata(&mut self, profile: &GameProfile) -> Result<(), ProfileError> {
        let profiles_dir = self.config_dir.join("profiles");
        let filename = sanitize_filename(&profile.name) + ".json";
        let file_path = profiles_dir.join(filename);

        let profile_meta = ProfileMetadata {
            id: profile.id,
            name: profile.name.clone(),
            description: profile.description.clone(),
            file_path,
            sub_profile_count: profile.sub_profiles.len(),
            modified_time: SystemTime::now(),
            created_at: profile.created_at,
            modified_at: profile.modified_at,
            hotkey: profile
                .hotkey
                .as_ref()
                .map(|hk| hotkey_to_metadata_string(hk)),
        };

        for sub_profile in &profile.sub_profiles {
            let sub_meta = SubProfileMetadata {
                id: sub_profile.id,
                parent_profile_id: profile.id,
                name: sub_profile.name.clone(),
                description: sub_profile.description.clone(),
                hotkey: sub_profile
                    .hotkey
                    .as_ref()
                    .map(|hk| hotkey_to_metadata_string(hk)),
                created_at: sub_profile.created_at,
                modified_at: sub_profile.modified_at,
            };
            self.sub_profile_metadata.insert(sub_profile.id, sub_meta);
        }

        self.profile_metadata.insert(profile.id, profile_meta);
        Ok(())
    }

    /// Switch to a specific profile and sub-profile.
    /// Unloads current profile, loads new one, compiles ALL sub-profiles.
    pub fn switch_profile(
        &mut self,
        profile_id: &Uuid,
        sub_profile_id: &Uuid,
    ) -> Result<Arc<CompiledProfile>, ProfileError> {
        // Check if switching to different profile
        let need_profile_change = self
            .current_profile
            .as_ref()
            .map_or(true, |loaded| loaded.id != *profile_id);

        if need_profile_change {
            info!("[SWITCH] Loading new profile: {}", profile_id);

            // Unload current profile
            self.current_profile = None;
            self.compiled_sub_profiles.clear();
            self.current_sub_profile_id = None;

            // Load new profile from disk
            let profile_meta = self
                .profile_metadata
                .get(profile_id)
                .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?;

            let content = fs::read_to_string(&profile_meta.file_path)?;
            let mut profile: GameProfile = serde_json::from_str(&content)?;
            ensure_profile_ids(&mut profile);

            // Compile ALL sub-profiles.
            for sub_profile in &profile.sub_profiles {
                let compiled = profile
                    .compile_profile(&sub_profile.name)
                    .ok_or_else(|| ProfileError::SubProfileNotFound(sub_profile.id.to_string()))?;
                self.compiled_sub_profiles
                    .insert(sub_profile.id, Arc::new(compiled));
            }

            self.current_profile = Some(profile);
            info!(
                "[SWITCH] Compiled {} sub-profiles",
                self.compiled_sub_profiles.len()
            );
        }

        // Switch to specific sub-profile.
        self.current_sub_profile_id = Some(*sub_profile_id);

        let compiled = self
            .compiled_sub_profiles
            .get(sub_profile_id)
            .ok_or_else(|| ProfileError::SubProfileNotFound(sub_profile_id.to_string()))?
            .clone();

        Ok(compiled)
    }

    /// Get current active compiled profile for mapping engine
    pub fn get_current_compiled_profile(&self) -> Option<Arc<CompiledProfile>> {
        if let Some(sub_id) = self.current_sub_profile_id {
            self.compiled_sub_profiles.get(&sub_id).cloned()
        } else {
            None
        }
    }

    // Metadata accessors for API consumers.
    pub fn get_profile_metadata_count(&self) -> usize {
        self.profile_metadata.len()
    }

    pub fn get_profile_metadata(&self, index: usize) -> Option<&ProfileMetadata> {
        self.profile_metadata.values().nth(index)
    }

    pub fn get_profile_metadata_by_id(&self, profile_id: &Uuid) -> Option<&ProfileMetadata> {
        self.profile_metadata.get(profile_id)
    }

    pub fn get_sub_profile_metadata(
        &self,
        profile_index: usize,
        sub_index: usize,
    ) -> Option<&SubProfileMetadata> {
        if let Some(profile_meta) = self.get_profile_metadata(profile_index) {
            self.sub_profile_metadata
                .values()
                .filter(|sm| sm.parent_profile_id == profile_meta.id)
                .nth(sub_index)
        } else {
            None
        }
    }

    pub fn get_sub_profile_metadata_for_profile(
        &self,
        profile_id: &Uuid,
    ) -> Vec<SubProfileMetadata> {
        self.sub_profile_metadata
            .values()
            .filter(|sm| sm.parent_profile_id == *profile_id)
            .cloned()
            .collect()
    }

    pub fn get_current_profile_id(&self) -> Option<Uuid> {
        self.current_profile.as_ref().map(|profile| profile.id)
    }

    pub fn get_current_sub_profile_id(&self) -> Option<Uuid> {
        self.current_sub_profile_id
    }

    // Current mappings exposed to the API layer.
    pub fn get_current_mapping_count(&self) -> usize {
        if let Some(profile) = &self.current_profile {
            if let Some(sub_id) = self.current_sub_profile_id {
                if let Some(sub_profile) = profile.sub_profiles.iter().find(|sp| sp.id == sub_id) {
                    return sub_profile.mappings.len();
                }
            }
        }
        0
    }

    pub fn get_current_mapping(&self, index: usize) -> Option<&KeyMapping> {
        if let Some(profile) = &self.current_profile {
            if let Some(sub_id) = self.current_sub_profile_id {
                if let Some(sub_profile) = profile.sub_profiles.iter().find(|sp| sp.id == sub_id) {
                    return sub_profile.mappings.get(index);
                }
            }
        }
        None
    }

    /// Set/update a mapping in the current active sub-profile.
    pub fn set_current_mapping(&mut self, mapping: KeyMapping) -> Result<(), ProfileError> {
        let sub_profile_id = self
            .current_sub_profile_id
            .ok_or(ProfileError::NoSubProfileActive)?;
        let sub_profile_name: String;

        // Update or add the mapping.
        {
            let profile = self
                .current_profile
                .as_mut()
                .ok_or(ProfileError::NoProfileLoaded)?;
            let sub_profile = profile
                .sub_profiles
                .iter_mut()
                .find(|sp| sp.id == sub_profile_id)
                .ok_or(ProfileError::SubProfileNotFound(sub_profile_id.to_string()))?;

            // Save sub-profile name for later compilation.
            sub_profile_name = sub_profile.name.clone();

            let now = crate::profile::profiles::now_timestamp();
            let mut mapping = mapping;
            if mapping.created_at == 0 {
                mapping.created_at = now;
            }
            mapping.modified_at = now;

            // Update or add the mapping.
            if let Some(existing) = sub_profile
                .mappings
                .iter_mut()
                .find(|m| m.key_name == mapping.key_name)
            {
                mapping.created_at = existing.created_at;
                *existing = mapping;
            } else {
                sub_profile.mappings.push(mapping);
                sub_profile.mappings.sort_by_key(|m| m.created_at);
            }

            sub_profile.modified_at = now;
            profile.modified_at = now;
        }

        // Recompile and save.
        {
            let profile = self
                .current_profile
                .as_ref()
                .ok_or(ProfileError::NoProfileLoaded)?;
            let compiled = profile
                .compile_profile(&sub_profile_name)
                .ok_or_else(|| ProfileError::SubProfileNotFound(sub_profile_name.clone()))?;
            self.compiled_sub_profiles
                .insert(sub_profile_id, Arc::new(compiled));

            // Clone profile for saving.
            let profile_clone = profile.clone();
            self.save_profile(&profile_clone)?;
        }

        Ok(())
    }

    /// Remove a mapping from the current active sub-profile by key name.
    pub fn remove_current_mapping(&mut self, key_name: &str) -> Result<bool, ProfileError> {
        let sub_profile_id = self
            .current_sub_profile_id
            .ok_or(ProfileError::NoSubProfileActive)?;
        let sub_profile_name: String;
        let removed: bool;

        // Remove the mapping.
        {
            let profile = self
                .current_profile
                .as_mut()
                .ok_or(ProfileError::NoProfileLoaded)?;
            let sub_profile = profile
                .sub_profiles
                .iter_mut()
                .find(|sp| sp.id == sub_profile_id)
                .ok_or(ProfileError::SubProfileNotFound(sub_profile_id.to_string()))?;

            // Save sub-profile name for later compilation.
            sub_profile_name = sub_profile.name.clone();

            // Remove the mapping.
            let initial_len = sub_profile.mappings.len();
            sub_profile.mappings.retain(|m| m.key_name != key_name);
            removed = sub_profile.mappings.len() != initial_len;

            if removed {
                sub_profile.modified_at = crate::profile::profiles::now_timestamp();
                profile.modified_at = crate::profile::profiles::now_timestamp();
            }
        }

        // Recompile and save if something was removed
        if removed {
            let profile = self
                .current_profile
                .as_ref()
                .ok_or(ProfileError::NoProfileLoaded)?;
            let compiled = profile
                .compile_profile(&sub_profile_name)
                .ok_or_else(|| ProfileError::SubProfileNotFound(sub_profile_name.clone()))?;
            self.compiled_sub_profiles
                .insert(sub_profile_id, Arc::new(compiled));

            // Clone profile for saving
            let profile_clone = profile.clone();
            self.save_profile(&profile_clone)?;
        }

        Ok(removed)
    }

    /// Delete a profile by UUID (removes from disk and metadata).
    pub fn delete_profile(&mut self, profile_id: &Uuid) -> Result<(), ProfileError> {
        // Get profile metadata to find file path.
        let profile_meta = self
            .profile_metadata
            .get(profile_id)
            .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?
            .clone(); // Clone to avoid borrowing issues.

        // If this is the currently loaded profile, unload it.
        if let Some(current) = &self.current_profile {
            if current.id == *profile_id {
                self.current_profile = None;
                self.current_sub_profile_id = None;
                self.compiled_sub_profiles.clear();
            }
        }

        // Remove file from disk.
        if profile_meta.file_path.exists() {
            std::fs::remove_file(&profile_meta.file_path)?;
            warn!(
                "[MANAGER] Deleted profile file: {:?}",
                profile_meta.file_path
            );
        }

        // Remove from metadata.
        self.profile_metadata.remove(profile_id);

        // Remove associated sub-profile metadata.
        self.sub_profile_metadata
            .retain(|_, sub_meta| sub_meta.parent_profile_id != *profile_id);

        warn!(
            "[MANAGER] Profile '{}' deleted successfully",
            profile_meta.name
        );
        Ok(())
    }

    /// Rename a profile by UUID (updates file and metadata).
    pub fn rename_profile(
        &mut self,
        profile_id: &Uuid,
        new_name: &str,
    ) -> Result<(), ProfileError> {
        let name_conflict = self
            .profile_metadata
            .iter()
            .any(|(id, meta)| *id != *profile_id && meta.name == new_name);
        if name_conflict {
            return Err(ProfileError::IoError(std::io::Error::new(
                std::io::ErrorKind::AlreadyExists,
                format!("Profile '{}' already exists", new_name),
            )));
        }

        let mut profile_meta = self
            .profile_metadata
            .get(profile_id)
            .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?
            .clone();

        if profile_meta.name == new_name {
            return Ok(());
        }

        let old_name = profile_meta.name.clone();
        let old_path = profile_meta.file_path.clone();
        let profiles_dir = self.config_dir.join("profiles");
        let new_filename = sanitize_filename(new_name) + ".json";
        let new_path = profiles_dir.join(&new_filename);

        let path_conflict = self
            .profile_metadata
            .iter()
            .any(|(id, meta)| *id != *profile_id && meta.file_path == new_path)
            || new_path.exists();
        if path_conflict {
            return Err(ProfileError::IoError(std::io::Error::new(
                std::io::ErrorKind::AlreadyExists,
                format!("Profile file '{}' already exists", new_filename),
            )));
        }

        let now_timestamp = crate::profile::profiles::now_timestamp();
        let now_system = SystemTime::now();

        let mut profile_to_persist: Option<GameProfile> = None;
        if let Some(current) = &mut self.current_profile {
            if current.id == *profile_id {
                current.name = new_name.to_string();
                current.modified_at = now_timestamp;
                profile_to_persist = Some(current.clone());
            }
        }

        if profile_to_persist.is_none() {
            let source_path = if old_path.exists() {
                old_path.clone()
            } else {
                return Err(ProfileError::IoError(std::io::Error::new(
                    std::io::ErrorKind::NotFound,
                    format!("Profile file not found for '{}'.", old_name),
                )));
            };

            let content = fs::read_to_string(&source_path)?;
            let mut profile: GameProfile = serde_json::from_str(&content)?;
            profile.name = new_name.to_string();
            profile.modified_at = now_timestamp;
            profile_to_persist = Some(profile);
        }

        let profile_to_persist = profile_to_persist
            .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?;

        let write_path = if old_path.exists() {
            old_path.clone()
        } else {
            new_path.clone()
        };
        let json = serde_json::to_string_pretty(&profile_to_persist)?;
        fs::write(&write_path, json)?;

        if write_path == old_path && old_path != new_path {
            fs::rename(&old_path, &new_path)?;
        }

        profile_meta.name = new_name.to_string();
        profile_meta.file_path = new_path;
        profile_meta.modified_time = now_system;
        profile_meta.modified_at = now_timestamp;
        self.profile_metadata.insert(*profile_id, profile_meta);

        warn!(
            "[MANAGER] Profile renamed from '{}' to '{}'",
            old_name, new_name
        );
        Ok(())
    }

    /// Cycle through sub-profiles of a profile based on creation timestamp order.
    pub fn cycle_sub_profile(&mut self, profile_id: &Uuid) -> Result<(Uuid, String), ProfileError> {
        let mut sub_metas: Vec<SubProfileMetadata> = self
            .sub_profile_metadata
            .values()
            .filter(|sm| sm.parent_profile_id == *profile_id)
            .cloned()
            .collect();

        if sub_metas.is_empty() {
            return Err(ProfileError::SubProfileNotFound(profile_id.to_string()));
        }

        sub_metas.sort_by(|a, b| {
            a.created_at
                .cmp(&b.created_at)
                .then_with(|| a.id.cmp(&b.id))
        });

        let current_is_profile = self
            .current_profile
            .as_ref()
            .map_or(false, |loaded| loaded.id == *profile_id);

        let next_meta = if current_is_profile {
            if let Some(current_sub_id) = self.current_sub_profile_id {
                if let Some(index) = sub_metas.iter().position(|meta| meta.id == current_sub_id) {
                    sub_metas[(index + 1) % sub_metas.len()].clone()
                } else {
                    sub_metas[0].clone()
                }
            } else {
                sub_metas[0].clone()
            }
        } else {
            sub_metas[0].clone()
        };

        self.switch_profile(profile_id, &next_meta.id)?;

        warn!(
            "[PROFILE] Cycling profile {} -> sub-profile '{}'",
            profile_id, next_meta.name
        );

        Ok((next_meta.id, next_meta.name))
    }

    /// Add a new sub-profile to an existing profile.
    pub fn add_sub_profile(
        &mut self,
        profile_id: &Uuid,
        name: &str,
        description: Option<&str>,
        hotkey: Option<&str>,
    ) -> Result<(), ProfileError> {
        // Check if profile exists in metadata.
        if !self.profile_metadata.contains_key(profile_id) {
            return Err(ProfileError::ProfileNotFound(profile_id.to_string()));
        }

        let parsed_hotkey = hotkey.and_then(metadata_hotkey_to_struct);

        let sub_profile = crate::profile::profiles::SubProfile::new(
            name.to_string(),
            description.unwrap_or("").to_string(),
            parsed_hotkey.clone(),
            Vec::new(),
        );

        // If this is the currently loaded profile, add directly.
        if let Some(current) = &mut self.current_profile {
            if current.id == *profile_id {
                current.sub_profiles.push(sub_profile.clone());
                current.modified_at = crate::profile::profiles::now_timestamp();

                // Compile the new sub-profile.
                let compiled = current
                    .compile_profile(name)
                    .ok_or_else(|| ProfileError::SubProfileNotFound(name.to_string()))?;
                self.compiled_sub_profiles
                    .insert(sub_profile.id, Arc::new(compiled));

                // Save to disk.
                let profile_clone = current.clone();
                let new_count = current.sub_profiles.len();
                self.save_profile(&profile_clone)?;

                // Add to sub-profile metadata.
                let hotkey_string = parsed_hotkey
                    .as_ref()
                    .map(|hk| hotkey_to_metadata_string(hk));

                let sub_meta = SubProfileMetadata {
                    id: sub_profile.id,
                    parent_profile_id: *profile_id,
                    name: sub_profile.name.clone(),
                    description: sub_profile.description.clone(),
                    hotkey: hotkey_string,
                    created_at: sub_profile.created_at,
                    modified_at: sub_profile.modified_at,
                };
                self.sub_profile_metadata.insert(sub_profile.id, sub_meta);

                // Update profile metadata count and timestamp
                if let Some(profile_meta) = self.profile_metadata.get_mut(profile_id) {
                    profile_meta.sub_profile_count = new_count;
                    profile_meta.modified_at = crate::profile::profiles::now_timestamp();
                }
            } else {
                // Different profile loaded, load the target profile temporarily.
                self.add_sub_profile_to_unloaded_profile(profile_id, name, description, hotkey)?;
            }
        } else {
            // No profile loaded, load the target profile temporarily.
            self.add_sub_profile_to_unloaded_profile(profile_id, name, description, hotkey)?;
        }

        warn!(
            "[MANAGER] Added sub-profile '{}' to profile {}",
            name, profile_id
        );
        Ok(())
    }

    /// Helper to add sub-profile to non-loaded profile.
    fn add_sub_profile_to_unloaded_profile(
        &mut self,
        profile_id: &Uuid,
        name: &str,
        description: Option<&str>,
        hotkey: Option<&str>,
    ) -> Result<(), ProfileError> {
        let profile_meta = self.profile_metadata.get(profile_id).unwrap();
        let content = std::fs::read_to_string(&profile_meta.file_path)?;
        let mut profile: GameProfile = serde_json::from_str(&content)?;

        let parsed_hotkey = hotkey.and_then(metadata_hotkey_to_struct);

        let sub_profile = crate::profile::profiles::SubProfile::new(
            name.to_string(),
            description.unwrap_or("").to_string(),
            parsed_hotkey.clone(),
            Vec::new(),
        );

        profile.sub_profiles.push(sub_profile.clone());
        profile.modified_at = crate::profile::profiles::now_timestamp();
        self.save_profile(&profile)?;

        // Add to sub-profile metadata.
        let hotkey_string = parsed_hotkey
            .as_ref()
            .map(|hk| hotkey_to_metadata_string(hk));

        let sub_meta = SubProfileMetadata {
            id: sub_profile.id,
            parent_profile_id: *profile_id,
            name: sub_profile.name.clone(),
            description: sub_profile.description.clone(),
            hotkey: hotkey_string,
            created_at: sub_profile.created_at,
            modified_at: sub_profile.modified_at,
        };
        self.sub_profile_metadata.insert(sub_profile.id, sub_meta);

        // Update profile metadata count and timestamp.
        if let Some(profile_meta) = self.profile_metadata.get_mut(profile_id) {
            profile_meta.sub_profile_count = profile.sub_profiles.len();
            profile_meta.modified_at = crate::profile::profiles::now_timestamp();
        }

        Ok(())
    }

    /// Delete a sub-profile by UUID.
    pub fn delete_sub_profile(
        &mut self,
        profile_id: &Uuid,
        sub_profile_id: &Uuid,
    ) -> Result<SubProfileDeletionOutcome, ProfileError> {
        let outcome = if let Some(current) = &mut self.current_profile {
            if current.id == *profile_id {
                if self.current_sub_profile_id == Some(*sub_profile_id) {
                    self.current_sub_profile_id = None;
                    self.compiled_sub_profiles.clear();
                }

                // Remove from profile and update timestamps.
                current.sub_profiles.retain(|sp| sp.id != *sub_profile_id);
                current.modified_at = crate::profile::profiles::now_timestamp();

                self.compiled_sub_profiles.remove(sub_profile_id);

                let new_count = current.sub_profiles.len();
                if new_count == 0 {
                    SubProfileDeletionOutcome::ProfileRemoved
                } else {
                    // Persist updated profile only when it still contains sub-profiles.
                    let profile_clone = current.clone();
                    self.save_profile(&profile_clone)?;

                    if let Some(profile_meta) = self.profile_metadata.get_mut(profile_id) {
                        profile_meta.sub_profile_count = new_count;
                        profile_meta.modified_at = crate::profile::profiles::now_timestamp();
                    }

                    SubProfileDeletionOutcome::SubProfileRemoved
                }
            } else {
                // Different profile loaded, handle unloaded profile.
                self.delete_sub_profile_from_unloaded_profile(profile_id, sub_profile_id)?
            }
        } else {
            // No profile loaded.
            self.delete_sub_profile_from_unloaded_profile(profile_id, sub_profile_id)?
        };

        // Remove from metadata cache.
        self.sub_profile_metadata.remove(sub_profile_id);

        if matches!(outcome, SubProfileDeletionOutcome::ProfileRemoved) {
            self.delete_profile(profile_id)?;
            warn!(
                "[MANAGER] Deleted sub-profile {} and removed empty profile {}",
                sub_profile_id, profile_id
            );
        } else {
            warn!(
                "[MANAGER] Deleted sub-profile {} from profile {}",
                sub_profile_id, profile_id
            );
        }

        Ok(outcome)
    }

    /// Helper to delete sub-profile from non-loaded profile.
    fn delete_sub_profile_from_unloaded_profile(
        &mut self,
        profile_id: &Uuid,
        sub_profile_id: &Uuid,
    ) -> Result<SubProfileDeletionOutcome, ProfileError> {
        let profile_meta = self
            .profile_metadata
            .get(profile_id)
            .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?
            .clone();
        let content = std::fs::read_to_string(&profile_meta.file_path)?;
        let mut profile: GameProfile = serde_json::from_str(&content)?;

        profile.sub_profiles.retain(|sp| sp.id != *sub_profile_id);
        profile.modified_at = crate::profile::profiles::now_timestamp();

        if profile.sub_profiles.is_empty() {
            if let Some(profile_meta) = self.profile_metadata.get_mut(profile_id) {
                profile_meta.sub_profile_count = 0;
                profile_meta.modified_at = profile.modified_at;
            }
            return Ok(SubProfileDeletionOutcome::ProfileRemoved);
        }

        self.save_profile(&profile)?;

        if let Some(profile_meta) = self.profile_metadata.get_mut(profile_id) {
            profile_meta.sub_profile_count = profile.sub_profiles.len();
            profile_meta.modified_at = profile.modified_at;
        }

        Ok(SubProfileDeletionOutcome::SubProfileRemoved)
    }

    /// Rename a sub-profile by UUID.
    pub fn rename_sub_profile(
        &mut self,
        profile_id: &Uuid,
        sub_profile_id: &Uuid,
        new_name: &str,
    ) -> Result<(), ProfileError> {
        // Update metadata.
        if let Some(sub_meta) = self.sub_profile_metadata.get_mut(sub_profile_id) {
            sub_meta.name = new_name.to_string();
            sub_meta.modified_at = crate::profile::profiles::now_timestamp();
        } else {
            return Err(ProfileError::SubProfileNotFound(sub_profile_id.to_string()));
        }

        // If this is the currently loaded profile, rename directly.
        if let Some(current) = &mut self.current_profile {
            if current.id == *profile_id {
                if let Some(sub_profile) = current
                    .sub_profiles
                    .iter_mut()
                    .find(|sp| sp.id == *sub_profile_id)
                {
                    sub_profile.name = new_name.to_string();
                    sub_profile.modified_at = crate::profile::profiles::now_timestamp();
                    current.modified_at = crate::profile::profiles::now_timestamp(); // Mettre à jour aussi le profil parent

                    // Recompile if needed.
                    let compiled = current
                        .compile_profile(new_name)
                        .ok_or_else(|| ProfileError::SubProfileNotFound(new_name.to_string()))?;
                    self.compiled_sub_profiles
                        .insert(*sub_profile_id, Arc::new(compiled));

                    // Save to disk.
                    let profile_clone = current.clone();
                    self.save_profile(&profile_clone)?;
                }
            } else {
                // Different profile loaded, handle unloaded profile.
                self.rename_sub_profile_in_unloaded_profile(profile_id, sub_profile_id, new_name)?;
            }
        } else {
            // No profile loaded.
            self.rename_sub_profile_in_unloaded_profile(profile_id, sub_profile_id, new_name)?;
        }

        warn!(
            "[MANAGER] Renamed sub-profile {} to '{}'",
            sub_profile_id, new_name
        );
        Ok(())
    }

    /// Helper to rename sub-profile in non-loaded profile.
    fn rename_sub_profile_in_unloaded_profile(
        &mut self,
        profile_id: &Uuid,
        sub_profile_id: &Uuid,
        new_name: &str,
    ) -> Result<(), ProfileError> {
        let profile_meta = self
            .profile_metadata
            .get(profile_id)
            .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?;
        let content = std::fs::read_to_string(&profile_meta.file_path)?;
        let mut profile: GameProfile = serde_json::from_str(&content)?;

        if let Some(sub_profile) = profile
            .sub_profiles
            .iter_mut()
            .find(|sp| sp.id == *sub_profile_id)
        {
            sub_profile.name = new_name.to_string();
            sub_profile.modified_at = crate::profile::profiles::now_timestamp();
            profile.modified_at = crate::profile::profiles::now_timestamp();
            self.save_profile(&profile)?;
        }

        Ok(())
    }

    /// Create a new profile with default sub-profile.
    pub fn create_profile(&mut self, name: &str, description: &str) -> Result<Uuid, ProfileError> {
        // Check if profile with this name already exists.
        if self.profile_metadata.values().any(|meta| meta.name == name) {
            return Err(ProfileError::IoError(std::io::Error::new(
                std::io::ErrorKind::AlreadyExists,
                format!("Profile '{}' already exists", name),
            )));
        }

        // Create new profile with a default sub-profile.
        let mut profile = crate::profile::profiles::GameProfile::new(name.to_string());
        profile.description = description.to_string();

        crate::profile::manager::ensure_profile_ids(&mut profile);

        // Persist profile and refresh metadata caches.
        self.save_profile(&profile)?;
        self.add_profile_to_metadata(&profile)?;

        warn!(
            "[MANAGER] Created profile '{}' with ID {}",
            name, profile.id
        );
        Ok(profile.id)
    }

    /// Update profile description by UUID.
    pub fn update_profile_description(
        &mut self,
        profile_id: &Uuid,
        new_description: &str,
    ) -> Result<(), ProfileError> {
        // Update metadata.
        if let Some(profile_meta) = self.profile_metadata.get_mut(profile_id) {
            profile_meta.description = new_description.to_string();
            profile_meta.modified_time = std::time::SystemTime::now();
            profile_meta.modified_at = crate::profile::profiles::now_timestamp();
        } else {
            return Err(ProfileError::ProfileNotFound(profile_id.to_string()));
        }

        // If this is the currently loaded profile, update it.
        if let Some(current) = &mut self.current_profile {
            if current.id == *profile_id {
                current.description = new_description.to_string();
                current.modified_at = crate::profile::profiles::now_timestamp();

                // Save the updated profile to disk.
                let profile_clone = current.clone();
                self.save_profile(&profile_clone)?;
            } else {
                // Different profile loaded, handle unloaded profile.
                self.update_description_in_unloaded_profile(profile_id, new_description)?;
            }
        } else {
            // No profile loaded.
            self.update_description_in_unloaded_profile(profile_id, new_description)?;
        }

        warn!("[MANAGER] Updated description for profile {}", profile_id);
        Ok(())
    }

    /// Helper to update description in non-loaded profile.
    fn update_description_in_unloaded_profile(
        &mut self,
        profile_id: &Uuid,
        new_description: &str,
    ) -> Result<(), ProfileError> {
        let profile_meta = self
            .profile_metadata
            .get(profile_id)
            .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?;
        let content = std::fs::read_to_string(&profile_meta.file_path)?;
        let mut profile: GameProfile = serde_json::from_str(&content)?;

        profile.description = new_description.to_string();
        profile.modified_at = crate::profile::profiles::now_timestamp();
        self.save_profile(&profile)?;

        Ok(())
    }

    /// Update profile cycle hotkey.
    pub fn set_profile_hotkey(
        &mut self,
        profile_id: &Uuid,
        hotkey: Option<&str>,
    ) -> Result<(), ProfileError> {
        let parsed_hotkey = hotkey.and_then(metadata_hotkey_to_struct);

        let now_timestamp = crate::profile::profiles::now_timestamp();
        let now_system = std::time::SystemTime::now();

        let metadata_entry = self
            .profile_metadata
            .get_mut(profile_id)
            .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?;

        metadata_entry.hotkey = parsed_hotkey
            .as_ref()
            .map(|hk| hotkey_to_metadata_string(hk));
        metadata_entry.modified_at = now_timestamp;
        metadata_entry.modified_time = now_system;

        if let Some(current) = &mut self.current_profile {
            if current.id == *profile_id {
                current.hotkey = parsed_hotkey.clone();
                current.modified_at = now_timestamp;

                let profile_clone = current.clone();
                self.save_profile(&profile_clone)?;
                return Ok(());
            }
        }

        let profile_meta = self.profile_metadata.get(profile_id).unwrap();
        let content = std::fs::read_to_string(&profile_meta.file_path)?;
        let mut profile: GameProfile = serde_json::from_str(&content)?;

        profile.hotkey = parsed_hotkey.clone();
        profile.modified_at = now_timestamp;
        self.save_profile(&profile)?;

        Ok(())
    }

    /// Update sub-profile hotkey.
    pub fn set_sub_profile_hotkey(
        &mut self,
        profile_id: &Uuid,
        sub_profile_id: &Uuid,
        hotkey: Option<&str>,
    ) -> Result<(), ProfileError> {
        let parsed_hotkey = hotkey.and_then(metadata_hotkey_to_struct);
        let now_timestamp = crate::profile::profiles::now_timestamp();
        let now_system = SystemTime::now();

        let profile_meta = self
            .profile_metadata
            .get_mut(profile_id)
            .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?;

        if let Some(sub_meta) = self.sub_profile_metadata.get_mut(sub_profile_id) {
            sub_meta.hotkey = parsed_hotkey
                .as_ref()
                .map(|hk| hotkey_to_metadata_string(hk));
            sub_meta.modified_at = now_timestamp;
        } else {
            return Err(ProfileError::SubProfileNotFound(sub_profile_id.to_string()));
        }

        profile_meta.modified_at = now_timestamp;
        profile_meta.modified_time = now_system;

        if let Some(current) = &mut self.current_profile {
            if current.id == *profile_id {
                if let Some(sub_profile) = current
                    .sub_profiles
                    .iter_mut()
                    .find(|sp| sp.id == *sub_profile_id)
                {
                    sub_profile.hotkey = parsed_hotkey.clone();
                    sub_profile.modified_at = now_timestamp;
                    current.modified_at = now_timestamp;

                    let profile_clone = current.clone();
                    self.save_profile(&profile_clone)?;
                    return Ok(());
                } else {
                    return Err(ProfileError::SubProfileNotFound(sub_profile_id.to_string()));
                }
            }
        }

        let profile_meta_read = self.profile_metadata.get(profile_id).unwrap();
        let content = std::fs::read_to_string(&profile_meta_read.file_path)?;
        let mut profile: GameProfile = serde_json::from_str(&content)?;

        if let Some(sub_profile) = profile
            .sub_profiles
            .iter_mut()
            .find(|sp| sp.id == *sub_profile_id)
        {
            sub_profile.hotkey = parsed_hotkey.clone();
            sub_profile.modified_at = now_timestamp;
            profile.modified_at = now_timestamp;
            self.save_profile(&profile)?;
            Ok(())
        } else {
            Err(ProfileError::SubProfileNotFound(sub_profile_id.to_string()))
        }
    }

    /// Export profile to external file by UUID.
    pub fn save_profile_to_file(
        &self,
        profile_id: &Uuid,
        file_path: &str,
    ) -> Result<(), ProfileError> {
        let profile = if let Some(current) = &self.current_profile {
            if current.id == *profile_id {
                // Profile is currently loaded.
                current.clone()
            } else {
                // Profile not loaded, load it temporarily.
                let profile_meta = self
                    .profile_metadata
                    .get(profile_id)
                    .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?;
                let content = std::fs::read_to_string(&profile_meta.file_path)?;
                serde_json::from_str(&content)?
            }
        } else {
            // No profile loaded, load it temporarily.
            let profile_meta = self
                .profile_metadata
                .get(profile_id)
                .ok_or_else(|| ProfileError::ProfileNotFound(profile_id.to_string()))?;
            let content = std::fs::read_to_string(&profile_meta.file_path)?;
            serde_json::from_str(&content)?
        };

        // Export.
        let json = serde_json::to_string_pretty(&profile)?;
        std::fs::write(file_path, json)?;

        warn!(
            "[MANAGER] Exported profile '{}' to file: {}",
            profile.name, file_path
        );
        Ok(())
    }

    /// Import profile from external file.
    pub fn load_profile_from_file(&mut self, file_path: &str) -> Result<Uuid, ProfileError> {
        // Read and parse profile from file.
        let content = std::fs::read_to_string(file_path)?;
        let mut profile: GameProfile = serde_json::from_str(&content)?;

        if profile.sub_profiles.is_empty() {
            return Err(ProfileError::EmptyProfile(profile.name));
        }

        // Refresh identifiers and timestamps so the imported profile behaves like a new entry.
        let import_time = crate::profile::profiles::now_timestamp();

        profile.id = Uuid::new_v4();
        profile.created_at = import_time;
        profile.modified_at = import_time;

        for (index, sub_profile) in profile.sub_profiles.iter_mut().enumerate() {
            let timestamp = import_time + index as u64;
            sub_profile.id = Uuid::new_v4();
            sub_profile.created_at = timestamp;
            sub_profile.modified_at = timestamp;
        }

        // Check if profile with this name already exists, rename if needed.
        let mut profile_name = profile.name.clone();
        let mut counter = 1;
        while self
            .profile_metadata
            .values()
            .any(|meta| meta.name == profile_name)
        {
            profile_name = format!("{} ({})", profile.name, counter);
            counter += 1;
        }
        profile.name = profile_name;

        crate::profile::manager::ensure_profile_ids(&mut profile);

        // Save to config directory and refresh metadata caches.
        self.save_profile(&profile)?;
        self.add_profile_to_metadata(&profile)?;

        warn!(
            "[MANAGER] Imported profile '{}' from file: {}",
            profile.name, file_path
        );
        Ok(profile.id)
    }

    pub fn get_profile_names(&self) -> Vec<String> {
        self.profile_metadata
            .values()
            .map(|meta| meta.name.clone())
            .collect()
    }

    /// Only return if it's the currently loaded profile.
    pub fn get_profile(&self, name: &str) -> Option<&GameProfile> {
        if let Some(profile) = &self.current_profile {
            if profile.name == name {
                return Some(profile);
            }
        }
        None
    }

    pub fn get_current_profile(&self) -> Option<Arc<CompiledProfile>> {
        self.get_current_compiled_profile()
    }
}

fn get_config_directory() -> Result<PathBuf, ProfileError> {
    dirs::config_dir()
        .map(|dir| dir.join("UniversalAnalogInput"))
        .ok_or(ProfileError::ConfigDirError)
}

fn sanitize_filename(name: &str) -> String {
    name.chars()
        .map(|c| match c {
            '<' | '>' | ':' | '"' | '|' | '?' | '*' | '\\' | '/' => '_',
            c if c.is_control() || (c as u32) == 127 => '_',
            c => c,
        })
        .collect()
}

fn ensure_profile_ids(profile: &mut GameProfile) {
    if profile.id.is_nil() {
        profile.id = Uuid::new_v4();
    }
    for sub in &mut profile.sub_profiles {
        if sub.id.is_nil() {
            sub.id = Uuid::new_v4();
        }
    }
}
