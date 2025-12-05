//! Atomic gamepad state shared between event callbacks and the mapping thread.

use crate::gamepad::vigem_client::XboxButton;
use std::sync::atomic::{AtomicI16, AtomicU16, AtomicU8, Ordering};

/// Atomic representation of the current gamepad state.
pub struct AtomicGamepadState {
    buttons: AtomicU16, // XButtons bitmask
    // Analog controls (updated by mapping thread)
    thumb_lx: AtomicI16, // -32768 to 32767
    thumb_ly: AtomicI16,
    thumb_rx: AtomicI16,
    thumb_ry: AtomicI16,
    left_trigger: AtomicU8, // 0 to 255
    right_trigger: AtomicU8,
}

impl AtomicGamepadState {
    /// Create a new atomic gamepad state with neutral positions.
    pub const fn new() -> Self {
        Self {
            buttons: AtomicU16::new(0),
            thumb_lx: AtomicI16::new(0),
            thumb_ly: AtomicI16::new(0),
            thumb_rx: AtomicI16::new(0),
            thumb_ry: AtomicI16::new(0),
            left_trigger: AtomicU8::new(0),
            right_trigger: AtomicU8::new(0),
        }
    }

    /// Set a single button state atomically.
    pub fn set_button(&self, button: XboxButton, pressed: bool) {
        let button_mask = button as u16;

        loop {
            let current = self.buttons.load(Ordering::Relaxed);
            let new_value = if pressed {
                current | button_mask // Set button bit
            } else {
                current & !button_mask // Clear button bit
            };

            if self
                .buttons
                .compare_exchange_weak(current, new_value, Ordering::Relaxed, Ordering::Relaxed)
                .is_ok()
            {
                break; // Atomic success
            }
        }
    }

    /// Replace the current button bitmask.
    pub fn set_buttons(&self, button_mask: u16) {
        self.buttons.store(button_mask, Ordering::Relaxed);
    }

    /// Update analog stick values atomically, clamped to the valid range.
    pub fn set_sticks(&self, left_x: f64, left_y: f64, right_x: f64, right_y: f64) {
        self.thumb_lx.store(
            (left_x.clamp(-1.0, 1.0) * 32767.0) as i16,
            Ordering::Relaxed,
        );
        self.thumb_ly.store(
            (left_y.clamp(-1.0, 1.0) * 32767.0) as i16,
            Ordering::Relaxed,
        );
        self.thumb_rx.store(
            (right_x.clamp(-1.0, 1.0) * 32767.0) as i16,
            Ordering::Relaxed,
        );
        self.thumb_ry.store(
            (right_y.clamp(-1.0, 1.0) * 32767.0) as i16,
            Ordering::Relaxed,
        );
    }

    /// Update trigger values atomically, clamped to the valid range.
    pub fn set_triggers(&self, left: f64, right: f64) {
        self.left_trigger
            .store((left.clamp(0.0, 1.0) * 255.0) as u8, Ordering::Relaxed);
        self.right_trigger
            .store((right.clamp(0.0, 1.0) * 255.0) as u8, Ordering::Relaxed);
    }

    /// Create a ViGEm `XGamepad` snapshot of the current state.
    pub fn to_vigem_gamepad(&self) -> vigem_client::XGamepad {
        let mut gamepad = vigem_client::XGamepad::default();
        gamepad.buttons.raw = self.buttons.load(Ordering::Relaxed);
        gamepad.thumb_lx = self.thumb_lx.load(Ordering::Relaxed);
        gamepad.thumb_ly = self.thumb_ly.load(Ordering::Relaxed);
        gamepad.thumb_rx = self.thumb_rx.load(Ordering::Relaxed);
        gamepad.thumb_ry = self.thumb_ry.load(Ordering::Relaxed);
        gamepad.left_trigger = self.left_trigger.load(Ordering::Relaxed);
        gamepad.right_trigger = self.right_trigger.load(Ordering::Relaxed);
        gamepad
    }

    /// Convert a gamepad control to an Xbox button for atomic operations.
    pub fn gamepad_control_to_xbox_button(
        control: &crate::profile::profiles::GamepadControl,
    ) -> Option<XboxButton> {
        use crate::profile::profiles::GamepadControl;

        match control {
            GamepadControl::ButtonA => Some(XboxButton::A),
            GamepadControl::ButtonB => Some(XboxButton::B),
            GamepadControl::ButtonX => Some(XboxButton::X),
            GamepadControl::ButtonY => Some(XboxButton::Y),
            GamepadControl::LeftShoulder => Some(XboxButton::LeftShoulder),
            GamepadControl::RightShoulder => Some(XboxButton::RightShoulder),
            GamepadControl::DPadUp => Some(XboxButton::DPadUp),
            GamepadControl::DPadDown => Some(XboxButton::DPadDown),
            GamepadControl::DPadLeft => Some(XboxButton::DPadLeft),
            GamepadControl::DPadRight => Some(XboxButton::DPadRight),
            _ => None, // Non-button controls (sticks, triggers)
        }
    }
}

impl Default for AtomicGamepadState {
    fn default() -> Self {
        Self::new()
    }
}

// AtomicGamepadState can be safely shared between threads
unsafe impl Send for AtomicGamepadState {}
unsafe impl Sync for AtomicGamepadState {}
