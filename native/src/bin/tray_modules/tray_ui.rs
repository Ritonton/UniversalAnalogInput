// Native Win32 system tray UI
use log::{error, info};
use std::ffi::OsStr;
use std::iter::once;
use std::mem::{size_of, MaybeUninit};
use std::os::windows::ffi::OsStrExt;
use std::ptr::null_mut;
use windows::core::*;
use windows::Win32::Foundation::*;
use windows::Win32::Graphics::Gdi::*;
use windows::Win32::System::LibraryLoader::*;
use windows::Win32::System::Registry::*;
use windows::Win32::UI::Shell::*;
use windows::Win32::UI::WindowsAndMessaging::*;

// Window messages
const WM_TRAYICON: u32 = WM_APP + 1;

// Menu IDs
const IDM_SHOW_UI: u16 = 1001;
const IDM_SEPARATOR1: u16 = 1002;
const IDM_TOGGLE_MAPPING: u16 = 1003;
const IDM_SEPARATOR2: u16 = 1004;
const IDM_EXIT: u16 = 1005;

// Global menu handle
static mut G_MENU: HMENU = HMENU(null_mut());

// Global window handle for tray operations
static mut G_HWND: HWND = HWND(null_mut());

// Global tray state
static mut G_KEYBOARD_CONNECTED: bool = true;
static mut G_MAPPING_ACTIVE: bool = false;
static mut G_UI_OPEN: bool = false;

pub struct TrayApp;

impl TrayApp {
    pub fn run() {
        unsafe {
            // Enable dark mode if Windows is in dark mode
            enable_dark_mode();

            // Get module handle
            let hmodule = GetModuleHandleW(None).expect("GetModuleHandleW failed");
            let hinstance: HINSTANCE = hmodule.into();

            // Register window class
            let class_name = w!("UAI_TrayClass");
            let wc = WNDCLASSW {
                style: CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc: Some(wndproc),
                hInstance: hinstance,
                hIcon: LoadIconW(None, IDI_APPLICATION).unwrap(),
                hCursor: LoadCursorW(None, IDC_ARROW).unwrap(),
                hbrBackground: GetSysColorBrush(COLOR_WINDOW),
                lpszClassName: class_name,
                ..Default::default()
            };
            RegisterClassW(&wc);

            // Create invisible window (for message handling)
            let hwnd = CreateWindowExW(
                WINDOW_EX_STYLE::default(),
                class_name,
                w!("Universal Analog Input"),
                WINDOW_STYLE::default(),
                0,
                0,
                0,
                0,
                None,
                None,
                Some(hinstance),
                None,
            )
            .expect("CreateWindowExW failed");

            // Store window handle globally for tray icon updates
            G_HWND = hwnd;

            // Message loop
            let mut msg = MaybeUninit::<MSG>::uninit();
            loop {
                let ret = GetMessageW(msg.as_mut_ptr(), None, 0, 0);
                if ret.0 == 0 || ret.0 == -1 {
                    break;
                }
                let msg = msg.assume_init();
                let _ = TranslateMessage(&msg).as_bool();
                DispatchMessageW(&msg);
            }
        }
    }
}

/// Window procedure that handles all messages
extern "system" fn wndproc(hwnd: HWND, msg: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    unsafe {
        match msg {
            WM_CREATE => {
                // Load icon from exe resources (embedded via build.rs)
                // Icon ID 1 is set in build.rs via winres
                // Use LoadImageW for better size control (16x16 for tray)
                let hmodule = GetModuleHandleW(None).expect("GetModuleHandleW failed");

                // Try to load custom icon from resources
                let hicon = match LoadImageW(
                    Some(hmodule.into()),
                    PCWSTR(1 as *const u16),
                    IMAGE_ICON,
                    GetSystemMetrics(SM_CXSMICON), // Small icon width (16px)
                    GetSystemMetrics(SM_CYSMICON), // Small icon height (16px)
                    LR_DEFAULTCOLOR | LR_SHARED,
                ) {
                    Ok(handle) => HICON(handle.0),
                    Err(_) => {
                        // Fallback to default application icon
                        LoadIconW(None, IDI_APPLICATION).unwrap()
                    }
                };

                // Create tray icon
                let mut nid = NOTIFYICONDATAW {
                    cbSize: size_of::<NOTIFYICONDATAW>() as u32,
                    hWnd: hwnd,
                    uID: 1,
                    uFlags: NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
                    uCallbackMessage: WM_TRAYICON,
                    hIcon: hicon,
                    ..Default::default()
                };

                let tip = to_wide("Universal Analog Input");
                for (i, c) in tip.iter().enumerate().take(nid.szTip.len()) {
                    nid.szTip[i] = *c;
                }

                let _ = Shell_NotifyIconW(NIM_ADD, &mut nid);

                // Set tray icon version
                nid.Anonymous.uVersion = NOTIFYICON_VERSION_4 as u32;
                let _ = Shell_NotifyIconW(NIM_SETVERSION, &mut nid);

                // Create context menu
                G_MENU = CreatePopupMenu().expect("CreatePopupMenu failed");

                let _ = AppendMenuW(G_MENU, MF_STRING, IDM_SHOW_UI as usize, w!("Show UI"));
                let _ = AppendMenuW(G_MENU, MF_SEPARATOR, IDM_SEPARATOR1 as usize, None);

                // Add toggle mapping item with initial text based on current state
                let initial_mapping_active =
                    universal_analog_input::api::mappings::is_mapping_active();
                let initial_text = if initial_mapping_active {
                    w!("Stop Mapping")
                } else {
                    w!("Start Mapping")
                };
                let _ = AppendMenuW(G_MENU, MF_STRING, IDM_TOGGLE_MAPPING as usize, initial_text);

                let _ = AppendMenuW(G_MENU, MF_SEPARATOR, IDM_SEPARATOR2 as usize, None);
                let _ = AppendMenuW(G_MENU, MF_STRING, IDM_EXIT as usize, w!("Exit"));

                LRESULT(0)
            }

            WM_TRAYICON => {
                // With NOTIFYICON_VERSION_4, event is in LOWORD of lparam
                let event = loword(lparam.0 as u32) as u32;
                match event {
                    WM_RBUTTONUP | WM_CONTEXTMENU => {
                        // Show context menu
                        let x = get_x_lparam(wparam.0);
                        let y = get_y_lparam(wparam.0);

                        let _ = SetForegroundWindow(hwnd);

                        let _ = TrackPopupMenu(
                            G_MENU,
                            TPM_RIGHTBUTTON | TPM_BOTTOMALIGN,
                            x,
                            y,
                            None,
                            hwnd,
                            None,
                        );

                        let _ = PostMessageW(Some(hwnd), WM_NULL, WPARAM(0), LPARAM(0));
                        LRESULT(0)
                    }
                    WM_LBUTTONUP => {
                        // Left click: Show UI
                        SendMessageW(
                            hwnd,
                            WM_COMMAND,
                            Some(WPARAM(IDM_SHOW_UI as usize)),
                            Some(LPARAM(0)),
                        );
                        LRESULT(0)
                    }
                    _ => DefWindowProcW(hwnd, msg, wparam, lparam),
                }
            }

            WM_COMMAND => {
                let id = (wparam.0 & 0xFFFF) as u16;
                match id {
                    IDM_SHOW_UI => {
                        if G_UI_OPEN {
                            // UI is open, so close it
                            info!("[TRAY] Close UI requested");
                            send_shutdown_to_ui();
                        } else {
                            // UI is closed, so open it
                            info!("[TRAY] Show UI requested");
                            crate::request_ui_launch();
                        }
                        LRESULT(0)
                    }
                    IDM_TOGGLE_MAPPING => {
                        // Use G_MAPPING_ACTIVE as source of truth to minimize race condition window
                        if G_MAPPING_ACTIVE {
                            // Currently active, so stop it
                            info!("[TRAY] Stop mapping requested");
                            match universal_analog_input::api::mappings::stop_mapping() {
                                Ok(_) => {
                                    info!("[TRAY] Mapping stopped successfully");
                                    update_menu_text(false);
                                }
                                Err(e) => {
                                    error!("[TRAY] Failed to stop mapping: {}", e);
                                    MessageBoxW(
                                        Some(hwnd),
                                        PCWSTR(
                                            to_wide(&format!("Failed to stop mapping:\n{}", e))
                                                .as_ptr(),
                                        ),
                                        w!("Error"),
                                        MB_OK | MB_ICONERROR,
                                    );
                                }
                            }
                        } else {
                            // Currently inactive, so start it
                            info!("[TRAY] Start mapping requested");
                            match universal_analog_input::api::mappings::start_mapping() {
                                Ok(_) => {
                                    info!("[TRAY] Mapping started successfully");
                                    update_menu_text(true);
                                }
                                Err(e) => {
                                    error!("[TRAY] Failed to start mapping: {}", e);
                                    MessageBoxW(
                                        Some(hwnd),
                                        PCWSTR(
                                            to_wide(&format!("Failed to start mapping:\n{}", e))
                                                .as_ptr(),
                                        ),
                                        w!("Error"),
                                        MB_OK | MB_ICONERROR,
                                    );
                                }
                            }
                        }
                        LRESULT(0)
                    }
                    IDM_EXIT => {
                        let _ = DestroyWindow(hwnd);
                        LRESULT(0)
                    }
                    _ => DefWindowProcW(hwnd, msg, wparam, lparam),
                }
            }

            WM_DESTROY => {
                // Remove tray icon
                let mut nid = NOTIFYICONDATAW {
                    cbSize: size_of::<NOTIFYICONDATAW>() as u32,
                    hWnd: hwnd,
                    uID: 1,
                    ..Default::default()
                };
                let _ = Shell_NotifyIconW(NIM_DELETE, &mut nid);

                // Destroy menu
                if !G_MENU.0.is_null() {
                    let _ = DestroyMenu(G_MENU);
                    G_MENU = HMENU(null_mut());
                }

                // Shutdown will be handled by PostQuitMessage below
                // which exits the message loop and returns to main()

                PostQuitMessage(0);
                LRESULT(0)
            }

            _ => DefWindowProcW(hwnd, msg, wparam, lparam),
        }
    }
}

// Helper functions

fn to_wide(s: &str) -> Vec<u16> {
    OsStr::new(s).encode_wide().chain(once(0)).collect()
}

fn loword(l: u32) -> u16 {
    (l & 0xFFFF) as u16
}

fn get_x_lparam(lp: usize) -> i32 {
    (lp & 0xFFFF) as i16 as i32
}

fn get_y_lparam(lp: usize) -> i32 {
    ((lp >> 16) & 0xFFFF) as i16 as i32
}

/// Check if Windows is in dark mode
fn is_dark_mode() -> bool {
    unsafe {
        let key_path = w!("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
        let value_name = w!("AppsUseLightTheme");

        let mut hkey = HKEY::default();
        if RegOpenKeyExW(HKEY_CURRENT_USER, key_path, Some(0), KEY_READ, &mut hkey).is_ok() {
            let mut data: u32 = 0;
            let mut data_size = size_of::<u32>() as u32;
            let mut reg_type = REG_NONE;

            if RegQueryValueExW(
                hkey,
                value_name,
                None,
                Some(&mut reg_type),
                Some(&mut data as *mut u32 as *mut u8),
                Some(&mut data_size),
            )
            .is_ok()
            {
                let _ = RegCloseKey(hkey);
                return data == 0; // 0 = dark mode, 1 = light mode
            }
            let _ = RegCloseKey(hkey);
        }
        false
    }
}

/// Enable dark mode for menus
fn enable_dark_mode() {
    unsafe {
        if let Ok(hmodule) = LoadLibraryW(w!("uxtheme.dll")) {
            // SetPreferredAppMode (ordinal 135) and FlushMenuThemes (ordinal 136)
            let set_fn = GetProcAddress(hmodule, PCSTR(135 as *const u8));
            let flush_fn = GetProcAddress(hmodule, PCSTR(136 as *const u8));

            if let (Some(set_fn), Some(flush_fn)) = (set_fn, flush_fn) {
                type SetPreferredAppModeFn = unsafe extern "system" fn(i32) -> i32;
                type FlushMenuThemesFn = unsafe extern "system" fn();

                let set_fn: SetPreferredAppModeFn = std::mem::transmute(set_fn);
                let flush_fn: FlushMenuThemesFn = std::mem::transmute(flush_fn);

                if is_dark_mode() {
                    set_fn(1); // 1 = AllowDark
                } else {
                    set_fn(0); // 0 = Default
                }
                flush_fn();
            }
        }
    }
}

/// Show error message box
#[allow(dead_code)]
fn show_error(message: &str) {
    unsafe {
        let title_wide = to_wide("Error");
        let message_wide = to_wide(message);

        MessageBoxW(
            None,
            PCWSTR(message_wide.as_ptr()),
            PCWSTR(title_wide.as_ptr()),
            MB_OK | MB_ICONERROR,
        );
    }
}

/// Build tooltip text based on current state
/// Format: "Universal Analog Input\nKeyboard: Connected\nMapping: Active"
fn build_tooltip_text() -> String {
    unsafe {
        let mut lines = vec!["Universal Analog Input".to_string()];

        // Keyboard status line
        let keyboard_status = if G_KEYBOARD_CONNECTED {
            "Keyboard: Connected"
        } else {
            "Keyboard: Disconnected"
        };
        lines.push(keyboard_status.to_string());

        // Mapping status line (only show if keyboard connected)
        if G_KEYBOARD_CONNECTED {
            let mapping_status = if G_MAPPING_ACTIVE {
                "Mapping: Active"
            } else {
                "Mapping: Inactive"
            };
            lines.push(mapping_status.to_string());
        }

        lines.join("\n")
    }
}

/// Update the tray icon and tooltip (internal, optimized)
/// This is the low-level function that actually modifies the system tray
fn update_tray_icon_internal() {
    unsafe {
        if G_HWND.0.is_null() {
            error!("[TRAY_UI] Cannot update tray icon - window not created yet");
            return;
        }

        // Load base icon from exe resources
        let hmodule = match GetModuleHandleW(None) {
            Ok(h) => h,
            Err(_) => return,
        };

        let base_icon = match LoadImageW(
            Some(hmodule.into()),
            PCWSTR(1 as *const u16),
            IMAGE_ICON,
            GetSystemMetrics(SM_CXSMICON),
            GetSystemMetrics(SM_CYSMICON),
            LR_DEFAULTCOLOR | LR_SHARED,
        ) {
            Ok(handle) => HICON(handle.0),
            Err(_) => {
                // Fallback to default application icon
                match LoadIconW(None, IDI_APPLICATION) {
                    Ok(icon) => icon,
                    Err(_) => return,
                }
            }
        };

        // If keyboard disconnected, add badge overlay
        let final_icon = if !G_KEYBOARD_CONNECTED {
            match create_icon_with_badge(base_icon) {
                Some(badged_icon) => badged_icon,
                None => base_icon, // Fallback to base icon if badge creation fails
            }
        } else {
            base_icon
        };

        // Build tooltip text from current state
        let tooltip_text = build_tooltip_text();
        let tip = to_wide(&tooltip_text);

        // Update tray icon
        let mut nid = NOTIFYICONDATAW {
            cbSize: size_of::<NOTIFYICONDATAW>() as u32,
            hWnd: G_HWND,
            uID: 1,
            uFlags: NIF_ICON | NIF_TIP | NIF_SHOWTIP,
            hIcon: final_icon,
            ..Default::default()
        };

        for (i, c) in tip.iter().enumerate().take(nid.szTip.len()) {
            nid.szTip[i] = *c;
        }

        let _ = Shell_NotifyIconW(NIM_MODIFY, &mut nid);

        info!(
            "[TRAY_UI] Tray icon updated - Keyboard: {}, Mapping: {}",
            if G_KEYBOARD_CONNECTED {
                "Connected"
            } else {
                "Disconnected"
            },
            if G_MAPPING_ACTIVE {
                "Active"
            } else {
                "Inactive"
            }
        );
    }
}

/// Update keyboard connection status and refresh tray icon
/// Called from tray.rs when keyboard status changes
pub fn update_keyboard_status(connected: bool) {
    unsafe {
        if G_KEYBOARD_CONNECTED == connected {
            return; // No change, skip update (optimization)
        }

        G_KEYBOARD_CONNECTED = connected;
        update_tray_icon_internal();
    }
}

/// Update tooltip only (optimized for mapping status changes that don't affect the icon)
fn update_tooltip_only() {
    unsafe {
        if G_HWND.0.is_null() {
            return;
        }

        // Build tooltip text from current state
        let tooltip_text = build_tooltip_text();
        let tip = to_wide(&tooltip_text);

        // Update only the tooltip (NIF_TIP flag only, no icon reload)
        let mut nid = NOTIFYICONDATAW {
            cbSize: size_of::<NOTIFYICONDATAW>() as u32,
            hWnd: G_HWND,
            uID: 1,
            uFlags: NIF_TIP | NIF_SHOWTIP,
            ..Default::default()
        };

        for (i, c) in tip.iter().enumerate().take(nid.szTip.len()) {
            nid.szTip[i] = *c;
        }

        let _ = Shell_NotifyIconW(NIM_MODIFY, &mut nid);

        info!(
            "[TRAY_UI] Tooltip updated - Mapping: {}",
            if G_MAPPING_ACTIVE {
                "Active"
            } else {
                "Inactive"
            }
        );
    }
}

/// Update mapping engine status and refresh tooltip only (no icon reload)
/// Called from tray.rs when mapping engine state changes
pub fn update_mapping_status(active: bool) {
    unsafe {
        if G_MAPPING_ACTIVE == active {
            return; // No change, skip update (optimization)
        }

        G_MAPPING_ACTIVE = active;
        update_tooltip_only(); // Only update tooltip, not the icon

        // Only update menu text if UI is closed (menu has mapping toggle)
        if !G_UI_OPEN {
            update_menu_text(active);
        }
    }
}

/// Notify that the UI has been opened
/// Called from tray.rs when UI connects
pub fn notify_ui_opened() {
    unsafe {
        if G_UI_OPEN {
            return; // Already open, skip update
        }

        G_UI_OPEN = true;
        rebuild_menu();

        info!("[TRAY_UI] UI opened - menu updated");
    }
}

/// Notify that the UI has been closed
/// Called from tray.rs when UI disconnects
pub fn notify_ui_closed() {
    unsafe {
        if !G_UI_OPEN {
            return; // Already closed, skip update
        }

        G_UI_OPEN = false;
        rebuild_menu();

        info!("[TRAY_UI] UI closed - menu updated");
    }
}

/// Update menu item text based on mapping engine status
/// When mapping is active, show "Stop Mapping"
/// When mapping is inactive, show "Start Mapping"
fn update_menu_text(mapping_active: bool) {
    unsafe {
        if G_MENU.0.is_null() {
            return;
        }

        let new_text = if mapping_active {
            w!("Stop Mapping")
        } else {
            w!("Start Mapping")
        };

        // Modify the menu item text using ModifyMenuW
        let _ = ModifyMenuW(
            G_MENU,
            IDM_TOGGLE_MAPPING as u32,
            MF_BYCOMMAND | MF_STRING,
            IDM_TOGGLE_MAPPING as usize,
            new_text,
        );

        info!(
            "[TRAY_UI] Menu text updated to: {}",
            if mapping_active {
                "Stop Mapping"
            } else {
                "Start Mapping"
            }
        );
    }
}

/// Send shutdown notification to UI
/// This asks the UI to close gracefully
fn send_shutdown_to_ui() {
    use universal_analog_input::ipc::protocol::IpcResponseType;
    use universal_analog_input::ipc::IpcResponse;

    let notification = IpcResponse::notification(IpcResponseType::Shutdown);
    universal_analog_input::ui_notifier::send_notification(notification);

    info!("[TRAY_UI] Shutdown notification sent to UI");
}

/// Rebuild the menu based on UI state
/// When UI is open: Show "Close UI" only (no mapping toggle)
/// When UI is closed: Show "Show UI" and "Toggle Mapping"
fn rebuild_menu() {
    unsafe {
        if G_MENU.0.is_null() {
            return;
        }

        // Clear all existing menu items
        while GetMenuItemCount(Some(G_MENU)) > 0 {
            let _ = DeleteMenu(G_MENU, 0, MF_BYPOSITION);
        }

        if G_UI_OPEN {
            // UI is open - show only "Close UI" and "Exit"
            let _ = AppendMenuW(G_MENU, MF_STRING, IDM_SHOW_UI as usize, w!("Close UI"));
            let _ = AppendMenuW(G_MENU, MF_SEPARATOR, IDM_SEPARATOR1 as usize, None);
            let _ = AppendMenuW(G_MENU, MF_STRING, IDM_EXIT as usize, w!("Exit"));
        } else {
            // UI is closed - show "Show UI", "Toggle Mapping", and "Exit"
            let _ = AppendMenuW(G_MENU, MF_STRING, IDM_SHOW_UI as usize, w!("Show UI"));
            let _ = AppendMenuW(G_MENU, MF_SEPARATOR, IDM_SEPARATOR1 as usize, None);

            // Add toggle mapping with appropriate text
            let mapping_text = if G_MAPPING_ACTIVE {
                w!("Stop Mapping")
            } else {
                w!("Start Mapping")
            };
            let _ = AppendMenuW(G_MENU, MF_STRING, IDM_TOGGLE_MAPPING as usize, mapping_text);

            let _ = AppendMenuW(G_MENU, MF_SEPARATOR, IDM_SEPARATOR2 as usize, None);
            let _ = AppendMenuW(G_MENU, MF_STRING, IDM_EXIT as usize, w!("Exit"));
        }

        info!(
            "[TRAY_UI] Menu rebuilt - UI: {}",
            if G_UI_OPEN { "OPEN" } else { "CLOSED" }
        );
    }
}

/// Create an icon with a red error badge overlay
/// Returns Some(icon) on success, None on failure
fn create_icon_with_badge(base_icon: HICON) -> Option<HICON> {
    unsafe {
        // Get system icon size for tray
        let icon_size = GetSystemMetrics(SM_CXSMICON);

        // Create device contexts
        let hdc_screen = GetDC(None);
        let hdc_mem = CreateCompatibleDC(Some(hdc_screen));
        let hdc_mask = CreateCompatibleDC(Some(hdc_screen));

        // Create bitmap for icon composition
        let hbitmap = CreateCompatibleBitmap(hdc_screen, icon_size, icon_size);
        let hbitmap_mask = CreateCompatibleBitmap(hdc_screen, icon_size, icon_size);

        let old_bitmap = SelectObject(hdc_mem, HGDIOBJ(hbitmap.0));
        let old_mask = SelectObject(hdc_mask, HGDIOBJ(hbitmap_mask.0));

        // Draw base icon
        let _ = DrawIconEx(
            hdc_mem, 0, 0, base_icon, icon_size, icon_size, 0, None, DI_NORMAL,
        );

        // Load red error icon from imageres.dll (index 93)
        let system_path = std::env::var("SystemRoot").unwrap_or_else(|_| "C:\\Windows".to_string());
        let imageres_path = format!("{}\\System32\\imageres.dll", system_path);
        let imageres_wide = to_wide(&imageres_path);

        let badge_icon = ExtractIconW(None, PCWSTR(imageres_wide.as_ptr()), 93);

        if !badge_icon.is_invalid() && badge_icon.0 as isize != 1 {
            // Draw badge overlay in top-right corner (60% size for better visibility)
            let badge_size = (icon_size as f32 * 0.6) as i32;
            // Position at top-right corner, fully inside the icon bounds
            let badge_x = icon_size - badge_size;
            let badge_y = 0;

            let _ = DrawIconEx(
                hdc_mem, badge_x, badge_y, badge_icon, badge_size, badge_size, 0, None, DI_NORMAL,
            );

            let _ = DestroyIcon(badge_icon);
        }

        // Create icon from bitmap
        let icon_info = ICONINFO {
            fIcon: true.into(),
            xHotspot: 0,
            yHotspot: 0,
            hbmMask: hbitmap_mask,
            hbmColor: hbitmap,
        };

        let result_icon = CreateIconIndirect(&icon_info);

        // Cleanup
        let _ = SelectObject(hdc_mem, old_bitmap);
        let _ = SelectObject(hdc_mask, old_mask);
        let _ = DeleteObject(HGDIOBJ(hbitmap.0));
        let _ = DeleteObject(HGDIOBJ(hbitmap_mask.0));
        let _ = DeleteDC(hdc_mem);
        let _ = DeleteDC(hdc_mask);
        let _ = ReleaseDC(None, hdc_screen);

        result_icon.ok()
    }
}
