/// Low-level Windows hooks (WH_KEYBOARD_LL + WH_MOUSE_LL).
///
/// Captures KeyDown/KeyUp and mouse wheel events regardless of window focus.
/// Mouse movement is polled via GetCursorPos on a dedicated thread.

use std::sync::OnceLock;
use tokio::sync::mpsc;
use windows::Win32::Foundation::{LPARAM, LRESULT, POINT, WPARAM};
use windows::Win32::UI::WindowsAndMessaging::{
    CallNextHookEx, DispatchMessageW, GetCursorPos, GetMessageW, SetWindowsHookExW,
    TranslateMessage, KBDLLHOOKSTRUCT, MSLLHOOKSTRUCT,
    WH_KEYBOARD_LL, WH_MOUSE_LL,
    WM_KEYDOWN, WM_KEYUP, WM_MOUSEWHEEL, WM_SYSKEYDOWN, WM_SYSKEYUP,
};

#[derive(Debug, Clone)]
pub enum HookEvent {
    KeyDown(u16),
    KeyUp(u16),
    /// Scroll delta in detents (1.0 = one notch upward).
    WheelScrolled(f32),
    /// Relative mouse displacement in pixels.
    MouseMoved { dx: i32, dy: i32 },
}

static SENDER: OnceLock<mpsc::UnboundedSender<HookEvent>> = OnceLock::new();

pub fn start() -> mpsc::UnboundedReceiver<HookEvent> {
    let (tx, rx) = mpsc::unbounded_channel();
    SENDER.set(tx).ok();

    // Poll GetCursorPos to capture global mouse movement independently of focus.
    let poll_tx = SENDER.get().unwrap().clone();
    std::thread::spawn(move || {
        let mut last_x: i32 = i32::MIN;
        let mut last_y: i32 = i32::MIN;
        loop {
            unsafe {
                let mut pt = POINT { x: 0, y: 0 };
                if GetCursorPos(&mut pt).is_ok() {
                    if last_x != i32::MIN {
                        let dx = pt.x - last_x;
                        let dy = pt.y - last_y;
                        if dx != 0 || dy != 0 {
                            poll_tx.send(HookEvent::MouseMoved { dx, dy }).ok();
                        }
                    }
                    last_x = pt.x;
                    last_y = pt.y;
                }
            }
            std::thread::sleep(std::time::Duration::from_millis(8));
        }
    });

    std::thread::spawn(|| unsafe {
        let _kbd   = SetWindowsHookExW(WH_KEYBOARD_LL, Some(kbd_proc),   None, 0)
            .expect("SetWindowsHookExW(WH_KEYBOARD_LL) failed");
        let _mouse = SetWindowsHookExW(WH_MOUSE_LL,    Some(mouse_proc), None, 0)
            .expect("SetWindowsHookExW(WH_MOUSE_LL) failed");

        let mut msg = std::mem::zeroed();
        loop {
            let r = GetMessageW(&mut msg, None, 0, 0);
            if r.0 == 0 || r.0 == -1 { break; }
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
    });

    rx
}

unsafe extern "system" fn kbd_proc(code: i32, w: WPARAM, l: LPARAM) -> LRESULT {
    if code >= 0 {
        let kb = &*(l.0 as *const KBDLLHOOKSTRUCT);
        let ev = match w.0 as u32 {
            WM_KEYDOWN | WM_SYSKEYDOWN => Some(HookEvent::KeyDown(kb.vkCode as u16)),
            WM_KEYUP   | WM_SYSKEYUP   => Some(HookEvent::KeyUp(kb.vkCode as u16)),
            _ => None,
        };
        if let Some(ev) = ev {
            if let Some(tx) = SENDER.get() { tx.send(ev).ok(); }
        }
    }
    CallNextHookEx(None, code, w, l)
}

unsafe extern "system" fn mouse_proc(code: i32, w: WPARAM, l: LPARAM) -> LRESULT {
    if code >= 0 {
        let ms = &*(l.0 as *const MSLLHOOKSTRUCT);
        if w.0 as u32 == WM_MOUSEWHEEL {
            // HIWORD of mouseData = signed delta in WHEEL_DELTA units (120).
            let delta = ((ms.mouseData >> 16) as u16) as i16 as f32 / 120.0;
            if delta != 0.0 {
                if let Some(tx) = SENDER.get() {
                    tx.send(HookEvent::WheelScrolled(delta)).ok();
                }
            }
        }
    }
    CallNextHookEx(None, code, w, l)
}

/// Converts a Windows Virtual Key code to a USB HID Usage ID.
pub fn vk_to_hid(vk: u16) -> Option<u16> {
    use windows::Win32::UI::Input::KeyboardAndMouse::*;

    if (0x41..=0x5A).contains(&vk) { return Some(vk - 0x41 + 4); }
    if (0x31..=0x39).contains(&vk) { return Some(vk - 0x31 + 30); }

    Some(match VIRTUAL_KEY(vk) {
        VK_0          => 39,  VK_RETURN     => 40,  VK_ESCAPE  => 41,
        VK_BACK       => 42,  VK_TAB        => 43,  VK_SPACE   => 44,
        VK_OEM_MINUS  => 45,  VK_OEM_PLUS   => 46,  VK_OEM_4   => 47,
        VK_OEM_6      => 48,  VK_OEM_5      => 49,  VK_OEM_1   => 51,
        VK_OEM_7      => 52,  VK_OEM_3      => 53,  VK_OEM_COMMA  => 54,
        VK_OEM_PERIOD => 55,  VK_OEM_2      => 56,  VK_CAPITAL => 57,
        VK_F1  => 58, VK_F2  => 59, VK_F3  => 60, VK_F4  => 61,
        VK_F5  => 62, VK_F6  => 63, VK_F7  => 64, VK_F8  => 65,
        VK_F9  => 66, VK_F10 => 67, VK_F11 => 68, VK_F12 => 69,
        VK_SNAPSHOT => 70, VK_SCROLL => 71, VK_PAUSE => 72,
        VK_INSERT => 73, VK_HOME => 74, VK_PRIOR   => 75,
        VK_DELETE => 76, VK_END  => 77, VK_NEXT    => 78,
        VK_RIGHT => 79, VK_LEFT => 80, VK_DOWN    => 81, VK_UP => 82,
        VK_NUMLOCK   => 83, VK_DIVIDE    => 84, VK_MULTIPLY => 85,
        VK_SUBTRACT  => 86, VK_ADD       => 87,
        VK_NUMPAD1 => 89, VK_NUMPAD2 => 90, VK_NUMPAD3 => 91,
        VK_NUMPAD4 => 92, VK_NUMPAD5 => 93, VK_NUMPAD6 => 94,
        VK_NUMPAD7 => 95, VK_NUMPAD8 => 96, VK_NUMPAD9 => 97,
        VK_NUMPAD0 => 98, VK_DECIMAL => 99,
        VK_LCONTROL => 224, VK_LSHIFT => 225, VK_LMENU => 226, VK_LWIN => 227,
        VK_RCONTROL => 228, VK_RSHIFT => 229, VK_RMENU => 230, VK_RWIN => 231,
        _ => return None,
    })
}
