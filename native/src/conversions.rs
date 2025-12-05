//! Centralized conversion helpers for keys, gamepad controls, response curves, and hotkey metadata.

use crate::profile::profiles::{GamepadControl, HotKey, ResponseCurve};

/// Windows Virtual Key constants used throughout the project.
pub mod vk {
    pub const A: u16 = 0x41;
    pub const B: u16 = 0x42;
    pub const C: u16 = 0x43;
    pub const D: u16 = 0x44;
    pub const E: u16 = 0x45;
    pub const F: u16 = 0x46;
    pub const G: u16 = 0x47;
    pub const H: u16 = 0x48;
    pub const I: u16 = 0x49;
    pub const J: u16 = 0x4A;
    pub const K: u16 = 0x4B;
    pub const L: u16 = 0x4C;
    pub const M: u16 = 0x4D;
    pub const N: u16 = 0x4E;
    pub const O: u16 = 0x4F;
    pub const P: u16 = 0x50;
    pub const Q: u16 = 0x51;
    pub const R: u16 = 0x52;
    pub const S: u16 = 0x53;
    pub const T: u16 = 0x54;
    pub const U: u16 = 0x55;
    pub const V: u16 = 0x56;
    pub const W: u16 = 0x57;
    pub const X: u16 = 0x58;
    pub const Y: u16 = 0x59;
    pub const Z: u16 = 0x5A;

    pub const KEY_0: u16 = 0x30;
    pub const KEY_1: u16 = 0x31;
    pub const KEY_2: u16 = 0x32;
    pub const KEY_3: u16 = 0x33;
    pub const KEY_4: u16 = 0x34;
    pub const KEY_5: u16 = 0x35;
    pub const KEY_6: u16 = 0x36;
    pub const KEY_7: u16 = 0x37;
    pub const KEY_8: u16 = 0x38;
    pub const KEY_9: u16 = 0x39;

    pub const F1: u16 = 0x70;
    pub const F2: u16 = 0x71;
    pub const F3: u16 = 0x72;
    pub const F4: u16 = 0x73;
    pub const F5: u16 = 0x74;
    pub const F6: u16 = 0x75;
    pub const F7: u16 = 0x76;
    pub const F8: u16 = 0x77;
    pub const F9: u16 = 0x78;
    pub const F10: u16 = 0x79;
    pub const F11: u16 = 0x7A;
    pub const F12: u16 = 0x7B;

    pub const SPACE: u16 = 0x20;
    pub const RETURN: u16 = 0x0D;
    pub const ESCAPE: u16 = 0x1B;
    pub const TAB: u16 = 0x09;
    pub const BACK: u16 = 0x08;
    pub const DELETE: u16 = 0x2E;
    pub const INSERT: u16 = 0x2D;
    pub const HOME: u16 = 0x24;
    pub const END: u16 = 0x23;
    pub const PRIOR: u16 = 0x21;
    pub const NEXT: u16 = 0x22;

    pub const UP: u16 = 0x26;
    pub const DOWN: u16 = 0x28;
    pub const LEFT: u16 = 0x25;
    pub const RIGHT: u16 = 0x27;

    pub const SHIFT: u16 = 0x10;
    pub const LSHIFT: u16 = 0xA0;
    pub const RSHIFT: u16 = 0xA1;
    pub const CONTROL: u16 = 0x11;
    pub const LCONTROL: u16 = 0xA2;
    pub const RCONTROL: u16 = 0xA3;
    pub const MENU: u16 = 0x12;
    pub const LMENU: u16 = 0xA4;
    pub const RMENU: u16 = 0xA5;
    pub const LWIN: u16 = 0x5B;
    pub const RWIN: u16 = 0x5C;

    pub const LBUTTON: u16 = 0x01;
    pub const RBUTTON: u16 = 0x02;
    pub const MBUTTON: u16 = 0x04;
}

/// Convert a VK code to a display name.
pub fn vk_to_key_name(vk_code: u16) -> &'static str {
    match vk_code {
        vk::A => "A",
        vk::B => "B",
        vk::C => "C",
        vk::D => "D",
        vk::E => "E",
        vk::F => "F",
        vk::G => "G",
        vk::H => "H",
        vk::I => "I",
        vk::J => "J",
        vk::K => "K",
        vk::L => "L",
        vk::M => "M",
        vk::N => "N",
        vk::O => "O",
        vk::P => "P",
        vk::Q => "Q",
        vk::R => "R",
        vk::S => "S",
        vk::T => "T",
        vk::U => "U",
        vk::V => "V",
        vk::W => "W",
        vk::X => "X",
        vk::Y => "Y",
        vk::Z => "Z",

        vk::KEY_1 => "1",
        vk::KEY_2 => "2",
        vk::KEY_3 => "3",
        vk::KEY_4 => "4",
        vk::KEY_5 => "5",
        vk::KEY_6 => "6",
        vk::KEY_7 => "7",
        vk::KEY_8 => "8",
        vk::KEY_9 => "9",
        vk::KEY_0 => "0",

        vk::F1 => "F1",
        vk::F2 => "F2",
        vk::F3 => "F3",
        vk::F4 => "F4",
        vk::F5 => "F5",
        vk::F6 => "F6",
        vk::F7 => "F7",
        vk::F8 => "F8",
        vk::F9 => "F9",
        vk::F10 => "F10",
        vk::F11 => "F11",
        vk::F12 => "F12",

        vk::SPACE => "Space",
        vk::RETURN => "Enter",
        vk::ESCAPE => "Esc",
        vk::TAB => "Tab",
        vk::BACK => "Backspace",
        vk::DELETE => "Delete",
        vk::INSERT => "Insert",
        vk::HOME => "Home",
        vk::END => "End",
        vk::PRIOR => "Page Up",
        vk::NEXT => "Page Down",
        vk::UP => "Up",
        vk::DOWN => "Down",
        vk::LEFT => "Left",
        vk::RIGHT => "Right",

        vk::LSHIFT | vk::RSHIFT => "Shift",
        vk::LCONTROL | vk::RCONTROL => "Ctrl",
        vk::LMENU | vk::RMENU => "Alt",
        vk::LWIN | vk::RWIN => "Win",

        vk::LBUTTON => "Left Mouse",
        vk::RBUTTON => "Right Mouse",
        vk::MBUTTON => "Middle Mouse",

        _ => "Unknown",
    }
}

/// Convert a display name to a VK code. Returns 0 when unknown.
pub fn key_name_to_vk(key_name: &str) -> u16 {
    match key_name {
        "A" => vk::A,
        "B" => vk::B,
        "C" => vk::C,
        "D" => vk::D,
        "E" => vk::E,
        "F" => vk::F,
        "G" => vk::G,
        "H" => vk::H,
        "I" => vk::I,
        "J" => vk::J,
        "K" => vk::K,
        "L" => vk::L,
        "M" => vk::M,
        "N" => vk::N,
        "O" => vk::O,
        "P" => vk::P,
        "Q" => vk::Q,
        "R" => vk::R,
        "S" => vk::S,
        "T" => vk::T,
        "U" => vk::U,
        "V" => vk::V,
        "W" => vk::W,
        "X" => vk::X,
        "Y" => vk::Y,
        "Z" => vk::Z,

        "1" => vk::KEY_1,
        "2" => vk::KEY_2,
        "3" => vk::KEY_3,
        "4" => vk::KEY_4,
        "5" => vk::KEY_5,
        "6" => vk::KEY_6,
        "7" => vk::KEY_7,
        "8" => vk::KEY_8,
        "9" => vk::KEY_9,
        "0" => vk::KEY_0,

        "F1" => vk::F1,
        "F2" => vk::F2,
        "F3" => vk::F3,
        "F4" => vk::F4,
        "F5" => vk::F5,
        "F6" => vk::F6,
        "F7" => vk::F7,
        "F8" => vk::F8,
        "F9" => vk::F9,
        "F10" => vk::F10,
        "F11" => vk::F11,
        "F12" => vk::F12,

        "Space" => vk::SPACE,
        "Enter" => vk::RETURN,
        "Esc" => vk::ESCAPE,
        "Tab" => vk::TAB,
        "Backspace" => vk::BACK,
        "Delete" => vk::DELETE,
        "Insert" => vk::INSERT,
        "Home" => vk::HOME,
        "End" => vk::END,
        "Page Up" => vk::PRIOR,
        "Page Down" => vk::NEXT,
        "Up" => vk::UP,
        "Down" => vk::DOWN,
        "Left" => vk::LEFT,
        "Right" => vk::RIGHT,

        "Shift" => vk::SHIFT,
        "Ctrl" => vk::CONTROL,
        "Alt" => vk::MENU,
        "Win" => vk::LWIN,

        "Left Mouse" => vk::LBUTTON,
        "Right Mouse" => vk::RBUTTON,
        "Middle Mouse" => vk::MBUTTON,

        _ => 0, // Unknown key
    }
}

/// Convert a gamepad control enum to its display name.
pub fn gamepad_control_to_name(control: &GamepadControl) -> &'static str {
    match control {
        GamepadControl::LeftStickUp => "Left Stick Up",
        GamepadControl::LeftStickDown => "Left Stick Down",
        GamepadControl::LeftStickLeft => "Left Stick Left",
        GamepadControl::LeftStickRight => "Left Stick Right",
        GamepadControl::RightStickUp => "Right Stick Up",
        GamepadControl::RightStickDown => "Right Stick Down",
        GamepadControl::RightStickLeft => "Right Stick Left",
        GamepadControl::RightStickRight => "Right Stick Right",
        GamepadControl::LeftTrigger => "Left Trigger",
        GamepadControl::RightTrigger => "Right Trigger",
        GamepadControl::ButtonA => "Button A",
        GamepadControl::ButtonB => "Button B",
        GamepadControl::ButtonX => "Button X",
        GamepadControl::ButtonY => "Button Y",
        GamepadControl::LeftShoulder => "Left Shoulder",
        GamepadControl::RightShoulder => "Right Shoulder",
        GamepadControl::DPadUp => "D-Pad Up",
        GamepadControl::DPadDown => "D-Pad Down",
        GamepadControl::DPadLeft => "D-Pad Left",
        GamepadControl::DPadRight => "D-Pad Right",
    }
}

/// Convert a display name to a gamepad control enum.
pub fn name_to_gamepad_control(name: &str) -> Option<GamepadControl> {
    match name {
        "Left Stick Up" => Some(GamepadControl::LeftStickUp),
        "Left Stick Down" => Some(GamepadControl::LeftStickDown),
        "Left Stick Left" => Some(GamepadControl::LeftStickLeft),
        "Left Stick Right" => Some(GamepadControl::LeftStickRight),
        "Right Stick Up" => Some(GamepadControl::RightStickUp),
        "Right Stick Down" => Some(GamepadControl::RightStickDown),
        "Right Stick Left" => Some(GamepadControl::RightStickLeft),
        "Right Stick Right" => Some(GamepadControl::RightStickRight),
        "Left Trigger" => Some(GamepadControl::LeftTrigger),
        "Right Trigger" => Some(GamepadControl::RightTrigger),
        "Button A" => Some(GamepadControl::ButtonA),
        "Button B" => Some(GamepadControl::ButtonB),
        "Button X" => Some(GamepadControl::ButtonX),
        "Button Y" => Some(GamepadControl::ButtonY),
        "Left Shoulder" => Some(GamepadControl::LeftShoulder),
        "Right Shoulder" => Some(GamepadControl::RightShoulder),
        "D-Pad Up" => Some(GamepadControl::DPadUp),
        "D-Pad Down" => Some(GamepadControl::DPadDown),
        "D-Pad Left" => Some(GamepadControl::DPadLeft),
        "D-Pad Right" => Some(GamepadControl::DPadRight),
        _ => None,
    }
}

/// Convert a response curve enum to its display name.
pub fn response_curve_to_name(curve: &ResponseCurve) -> &'static str {
    match curve {
        ResponseCurve::Linear => "Linear",
        ResponseCurve::Custom => "Custom",
    }
}

/// Convert a display name to a response curve enum.
pub fn name_to_response_curve(name: &str) -> ResponseCurve {
    match name {
        "Linear" => ResponseCurve::Linear,
        "Custom" => ResponseCurve::Custom,
        _ => ResponseCurve::Linear, // Default fallback
    }
}

pub fn metadata_hotkey_to_struct(raw: &str) -> Option<HotKey> {
    let trimmed = raw.trim();
    if trimmed.is_empty() || trimmed.eq_ignore_ascii_case("none") {
        return None;
    }

    let mut modifiers: u8 = 0;
    let mut key_name: Option<String> = None;

    for token in trimmed.split('+') {
        let token = token.trim();
        if token.is_empty() {
            continue;
        }

        match token.to_ascii_lowercase().as_str() {
            "ctrl" | "control" => modifiers |= 0b0001,
            "alt" => modifiers |= 0b0010,
            "shift" => modifiers |= 0b0100,
            "win" | "windows" | "super" => modifiers |= 0b1000,
            _ => key_name = Some(token.to_string()),
        }
    }

    key_name.map(|name| HotKey {
        key_name: name,
        modifiers,
    })
}

pub fn hotkey_to_metadata_string(hotkey: &HotKey) -> String {
    let mut parts: Vec<String> = Vec::new();

    if hotkey.modifiers & 0b0001 != 0 {
        parts.push("Ctrl".to_string());
    }
    if hotkey.modifiers & 0b0010 != 0 {
        parts.push("Alt".to_string());
    }
    if hotkey.modifiers & 0b0100 != 0 {
        parts.push("Shift".to_string());
    }
    if hotkey.modifiers & 0b1000 != 0 {
        parts.push("Win".to_string());
    }

    parts.push(hotkey.key_name.clone());
    parts.join(" + ")
}

/// List all supported key names.
pub fn get_all_supported_key_names() -> Vec<&'static str> {
    let vk_codes = vec![
        // Letters
        vk::A,
        vk::B,
        vk::C,
        vk::D,
        vk::E,
        vk::F,
        vk::G,
        vk::H,
        vk::I,
        vk::J,
        vk::K,
        vk::L,
        vk::M,
        vk::N,
        vk::O,
        vk::P,
        vk::Q,
        vk::R,
        vk::S,
        vk::T,
        vk::U,
        vk::V,
        vk::W,
        vk::X,
        vk::Y,
        vk::Z,
        // Numbers
        vk::KEY_1,
        vk::KEY_2,
        vk::KEY_3,
        vk::KEY_4,
        vk::KEY_5,
        vk::KEY_6,
        vk::KEY_7,
        vk::KEY_8,
        vk::KEY_9,
        vk::KEY_0,
        // Special keys
        vk::SPACE,
        vk::TAB,
        vk::RETURN,
        vk::ESCAPE,
        vk::BACK,
        vk::DELETE,
        vk::INSERT,
        vk::HOME,
        vk::END,
        vk::PRIOR,
        vk::NEXT,
        // Modifiers
        vk::CONTROL,
        vk::SHIFT,
        vk::MENU,
        vk::LWIN,
        // Function keys
        vk::F1,
        vk::F2,
        vk::F3,
        vk::F4,
        vk::F5,
        vk::F6,
        vk::F7,
        vk::F8,
        vk::F9,
        vk::F10,
        vk::F11,
        vk::F12,
        // Arrow keys
        vk::UP,
        vk::DOWN,
        vk::LEFT,
        vk::RIGHT,
        // Mouse buttons
        vk::LBUTTON,
        vk::RBUTTON,
        vk::MBUTTON,
    ];

    // Convert VK codes to names using the safe conversion function
    vk_codes.into_iter().map(|vk| vk_to_key_name(vk)).collect()
}

/// List all available gamepad control names.
pub fn get_all_gamepad_control_names() -> Vec<&'static str> {
    let controls = vec![
        GamepadControl::LeftStickUp,
        GamepadControl::LeftStickDown,
        GamepadControl::LeftStickLeft,
        GamepadControl::LeftStickRight,
        GamepadControl::RightStickUp,
        GamepadControl::RightStickDown,
        GamepadControl::RightStickLeft,
        GamepadControl::RightStickRight,
        GamepadControl::LeftTrigger,
        GamepadControl::RightTrigger,
        GamepadControl::ButtonA,
        GamepadControl::ButtonB,
        GamepadControl::ButtonX,
        GamepadControl::ButtonY,
        GamepadControl::LeftShoulder,
        GamepadControl::RightShoulder,
        GamepadControl::DPadUp,
        GamepadControl::DPadDown,
        GamepadControl::DPadLeft,
        GamepadControl::DPadRight,
    ];

    // Convert enums to names using the safe conversion function
    controls
        .iter()
        .map(|control| gamepad_control_to_name(control))
        .collect()
}

#[cfg(test)]
mod tests {
    use super::{hotkey_to_metadata_string, metadata_hotkey_to_struct};

    #[test]
    fn parse_simple_hotkey() {
        let hotkey = metadata_hotkey_to_struct("F1").expect("Hotkey expected");
        assert_eq!(hotkey.key_name, "F1");
        assert_eq!(hotkey.modifiers, 0);
    }

    #[test]
    fn parse_combo_hotkey() {
        let hotkey = metadata_hotkey_to_struct("Ctrl + Alt + K").expect("Hotkey expected");
        assert_eq!(hotkey.key_name, "K");
        assert_eq!(hotkey.modifiers, 0b0001 | 0b0010);
    }

    #[test]
    fn parse_none_hotkey() {
        assert!(metadata_hotkey_to_struct("None").is_none());
        assert!(metadata_hotkey_to_struct("   ").is_none());
    }

    #[test]
    fn round_trip_hotkey() {
        let original = "Ctrl + Shift + F5";
        let parsed = metadata_hotkey_to_struct(original).expect("Hotkey expected");
        let serialized = hotkey_to_metadata_string(&parsed);
        assert_eq!(serialized, "Ctrl + Shift + F5");
    }
}
