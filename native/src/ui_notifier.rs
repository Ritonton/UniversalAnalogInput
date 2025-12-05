// UI notification helpers built on IPC messages.

use log::{debug, warn};
use once_cell::sync::Lazy;
use std::sync::Mutex;
use uuid::Uuid;

use crate::ipc::{IpcResponse, UiEventData};

// Global IPC server callback for notification queueing.
static IPC_NOTIFICATION_CALLBACK: Lazy<Mutex<Option<Box<dyn Fn(IpcResponse) + Send + Sync>>>> =
    Lazy::new(|| Mutex::new(None));

// Global tray callback for keyboard status changes (independent of IPC).
static TRAY_KEYBOARD_STATUS_CALLBACK: Lazy<Mutex<Option<Box<dyn Fn(bool) + Send + Sync>>>> =
    Lazy::new(|| Mutex::new(None));

const UI_EVENT_SUB_PROFILE_SWITCH: u32 = 0;

/// Register a callback for queuing notifications to the IPC server.
pub fn register_notification_callback<F>(callback: F)
where
    F: Fn(IpcResponse) + Send + Sync + 'static,
{
    let mut cb = IPC_NOTIFICATION_CALLBACK.lock().unwrap();
    *cb = Some(Box::new(callback));
}

/// Register a callback for tray keyboard status updates (independent of IPC).
pub fn register_tray_keyboard_callback<F>(callback: F)
where
    F: Fn(bool) + Send + Sync + 'static,
{
    let mut cb = TRAY_KEYBOARD_STATUS_CALLBACK.lock().unwrap();
    *cb = Some(Box::new(callback));
}

/// Send a notification to the UI via IPC.
pub fn send_notification(notification: IpcResponse) {
    if let Some(ref callback) = *IPC_NOTIFICATION_CALLBACK.lock().unwrap() {
        callback(notification);
    } else {
        warn!("[UI_NOTIFIER] No IPC callback registered, notification dropped");
    }
}

/// Notify the UI of a sub-profile switch.
pub fn notify_sub_profile_switch(profile_id: Uuid, sub_profile_id: Uuid) {
    use crate::ipc::protocol::IpcResponseType;
    let notification = IpcResponse::notification(IpcResponseType::UiEvent {
        data: Some(UiEventData {
            event_type: UI_EVENT_SUB_PROFILE_SWITCH,
            profile_id: profile_id.to_bytes_le(),
            sub_profile_id: sub_profile_id.to_bytes_le(),
        }),
    });

    send_notification(notification);
}

/// Send the current keyboard status to the UI.
pub fn send_current_keyboard_status() {
    use crate::ipc::protocol::IpcResponseType;
    use crate::WOOTING_SDK;

    let connected = {
        let sdk_guard = WOOTING_SDK.lock().unwrap();
        if let Some(ref sdk) = *sdk_guard {
            sdk.has_devices()
        } else {
            false
        }
    };

    debug!(
        "[UI_NOTIFIER] Sending current keyboard status: {}",
        if connected {
            "CONNECTED"
        } else {
            "DISCONNECTED"
        }
    );

    let notification = IpcResponse::notification(IpcResponseType::KeyboardStatus { connected });
    send_notification(notification);
}

/// Notify the UI of a keyboard connection status change.
pub fn send_keyboard_status_notification(connected: bool) {
    use crate::ipc::protocol::IpcResponseType;

    if let Some(ref callback) = *TRAY_KEYBOARD_STATUS_CALLBACK.lock().unwrap() {
        callback(connected);
    }

    let notification = IpcResponse::notification(IpcResponseType::KeyboardStatus { connected });
    send_notification(notification);
}

/// Notify the UI to bring itself to the foreground.
pub fn send_bring_to_front_notification() {
    use crate::ipc::protocol::IpcResponseType;

    let notification = IpcResponse::notification(IpcResponseType::BringToFront);
    send_notification(notification);
}
