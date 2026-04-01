// Persistent settings for the tray process.
// Owned by the tray binary; the UI reads/writes them via IPC (GetTraySettings, SetTrayHintEnabled).
use log::info;
use once_cell::sync::Lazy;
use serde::{Deserialize, Serialize};
use std::sync::Mutex;
use universal_analog_input::settings::SettingsStore;

const SETTINGS_FILE: &str = "tray_settings.json";
const SETTINGS_VERSION: u32 = 1;

/// All persistent settings owned by the tray process.
/// Each field needs `#[serde(default = "...")]` for forward compatibility.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TraySettings {
    pub version: u32,

    /// Show a Windows notification the first time the UI closes to tray in a session.
    #[serde(default = "default_true")]
    pub show_tray_hint_notification: bool,
}

fn default_true() -> bool {
    true
}

impl Default for TraySettings {
    fn default() -> Self {
        Self {
            version: SETTINGS_VERSION,
            show_tray_hint_notification: true,
        }
    }
}

static STORE: Lazy<SettingsStore> = Lazy::new(|| SettingsStore::new(SETTINGS_FILE));
static SETTINGS: Lazy<Mutex<TraySettings>> = Lazy::new(|| Mutex::new(STORE.load()));

/// Return a snapshot of the current tray settings.
pub fn get() -> TraySettings {
    SETTINGS.lock().unwrap().clone()
}

pub fn set_show_tray_hint_notification(enabled: bool) {
    let mut s = SETTINGS.lock().unwrap();
    if s.show_tray_hint_notification == enabled {
        return; // avoid unnecessary disk writes
    }
    s.show_tray_hint_notification = enabled;
    STORE.save(&*s);
    info!(
        "[SETTINGS] show_tray_hint_notification → {}",
        if enabled { "enabled" } else { "disabled" }
    );
}
