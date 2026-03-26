use crate::mouse::MouseDir;

#[derive(Debug, Clone)]
pub enum Message {
    KeyDown(u16),
    KeyUp(u16),
    ToggleConnected,
    ToggleMouseMode,
    SensitivityChanged(u8),
    /// Opens or closes the direction-binding flyout for a key (HID code).
    KeyClicked(u16),
    /// Toggles a mouse direction binding on a key.
    ToggleMouseDir { hid: u16, dir: MouseDir },
    Tick,
    ToggleAbout,
    OpenUrl(&'static str),
}
