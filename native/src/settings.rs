// JSON settings persistence for UAI components.
// Each component owns its own store and settings struct, no shared state here.
use log::{info, warn};
use serde::de::DeserializeOwned;
use serde::Serialize;
use std::fs;
use std::path::PathBuf;

/// Handle to a JSON settings file under `%LOCALAPPDATA%\UniversalAnalogInput\<filename>`.
pub struct SettingsStore {
    path: Option<PathBuf>,
}

impl SettingsStore {
    /// Resolve the file path. Construction always succeeds; I/O errors surface in `load`/`save`.
    pub fn new(filename: &str) -> Self {
        let path = dirs::data_local_dir()
            .map(|dir| dir.join("UniversalAnalogInput").join(filename));

        if path.is_none() {
            warn!("[SETTINGS] Could not resolve data directory — settings will not persist");
        }

        Self { path }
    }

    /// Load settings from disk. Returns `T::default()` if the file is absent or malformed.
    /// Unknown fields are ignored, so adding new fields never breaks existing files.
    pub fn load<T>(&self) -> T
    where
        T: Default + DeserializeOwned,
    {
        let path = match &self.path {
            Some(p) => p,
            None => return T::default(),
        };

        match fs::read_to_string(path) {
            Ok(content) => match serde_json::from_str::<T>(&content) {
                Ok(settings) => {
                    info!("[SETTINGS] Loaded from {:?}", path);
                    settings
                }
                Err(e) => {
                    warn!("[SETTINGS] Parse error in {:?}: {} — using defaults", path, e);
                    T::default()
                }
            },
            Err(_) => {
                info!("[SETTINGS] No file at {:?} — using defaults", path);
                T::default()
            }
        }
    }

    /// Write settings to disk as pretty-printed JSON. Creates the directory if needed.
    /// Returns `true` on success.
    pub fn save<T: Serialize>(&self, settings: &T) -> bool {
        let path = match &self.path {
            Some(p) => p,
            None => return false,
        };

        if let Some(parent) = path.parent() {
            let _ = fs::create_dir_all(parent);
        }

        match serde_json::to_string_pretty(settings) {
            Ok(json) => match fs::write(path, json) {
                Ok(_) => {
                    info!("[SETTINGS] Saved to {:?}", path);
                    true
                }
                Err(e) => {
                    warn!("[SETTINGS] Write failed for {:?}: {}", path, e);
                    false
                }
            },
            Err(e) => {
                warn!("[SETTINGS] Serialization failed: {}", e);
                false
            }
        }
    }
}
