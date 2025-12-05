use crate::api::types::AnalogInput;
use log::warn;
use std::os::raw::{c_float, c_int, c_uint, c_ushort};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Mutex;
use wooting_analog_wrapper::ffi::*;
use wooting_analog_wrapper::{DeviceEventType, DeviceInfo_FFI, KeycodeType, WootingAnalogResult};

static KEYBOARD_STATUS_CALLBACK: Mutex<Option<fn(bool)>> = Mutex::new(None);
static DEVICE_WAS_CONNECTED: AtomicBool = AtomicBool::new(true);

/// Register a callback to be notified when keyboard connection status changes.
pub fn set_keyboard_status_callback(callback: fn(bool)) {
    let mut cb = KEYBOARD_STATUS_CALLBACK.lock().unwrap();
    *cb = Some(callback);
}

/// Resume disconnect polling after the SDK reports a reconnection.
pub fn resume_disconnect_polling() {
    DEVICE_WAS_CONNECTED.store(true, Ordering::Relaxed);

    warn!("[WOOTING_SDK] Resuming disconnect polling");
}

pub struct WootingSDK {
    initialized: bool,
}

impl WootingSDK {
    pub fn new() -> Self {
        Self { initialized: false }
    }

    pub fn initialize(&mut self) -> Result<(), String> {
        let result = unsafe { wooting_analog_initialise() };

        if result >= 0 {
            self.initialized = true;
            Ok(())
        } else {
            if result == WootingAnalogResult::NoPlugins as i32 {
                Err("No Wooting plugins found. Make sure the Wooting Analog SDK is properly installed.".to_string())
            } else if result == WootingAnalogResult::DLLNotFound as i32 {
                Err("Wooting Analog SDK not found. Please install the SDK on Github.".to_string())
            } else if result == WootingAnalogResult::IncompatibleVersion as i32 {
                Err("Incompatible Wooting SDK version.".to_string())
            } else {
                Err(format!(
                    "Failed to initialize Wooting SDK: error code {}",
                    result
                ))
            }
        }
    }

    pub fn is_initialized(&self) -> bool {
        self.initialized && unsafe { wooting_analog_is_initialised() }
    }

    pub fn get_analog_inputs(&self) -> Result<Vec<AnalogInput>, String> {
        if !self.is_initialized() {
            return Err("SDK not initialized".to_string());
        }

        const MAX_KEYS: usize = 256;
        let mut code_buffer: [c_ushort; MAX_KEYS] = [0; MAX_KEYS];
        let mut analog_buffer: [c_float; MAX_KEYS] = [0.0; MAX_KEYS];

        let key_count = unsafe {
            wooting_analog_read_full_buffer(
                code_buffer.as_mut_ptr(),
                analog_buffer.as_mut_ptr(),
                MAX_KEYS as c_uint,
            )
        };

        if key_count < 0 {
            if key_count == WootingAnalogResult::UnInitialized as i32 {
                return Err("SDK not initialized".to_string());
            } else if key_count == WootingAnalogResult::NoDevices as i32 {
                return Ok(vec![]);
            } else {
                return Err(format!(
                    "Failed to read analog inputs: error code {}",
                    key_count
                ));
            }
        }

        let mut inputs = Vec::with_capacity(key_count as usize);
        for i in 0..(key_count as usize) {
            inputs.push(AnalogInput {
                key_code: code_buffer[i] as c_int,
                analog_value: analog_buffer[i] as f64,
            });
        }

        Ok(inputs)
    }

    /// Fill an existing buffer to avoid allocation in the hot path.
    pub fn fill_analog_inputs(&self, inputs: &mut Vec<AnalogInput>) -> Result<(), &'static str> {
        if !self.is_initialized() {
            return Err("SDK not initialized");
        }

        inputs.clear();

        const MAX_KEYS: usize = 256;
        let mut code_buffer: [c_ushort; MAX_KEYS] = [0; MAX_KEYS];
        let mut analog_buffer: [c_float; MAX_KEYS] = [0.0; MAX_KEYS];

        let key_count = unsafe {
            wooting_analog_read_full_buffer(
                code_buffer.as_mut_ptr(),
                analog_buffer.as_mut_ptr(),
                MAX_KEYS as c_uint,
            )
        };

        if key_count < 0 {
            if key_count == WootingAnalogResult::UnInitialized as i32 {
                return Err("SDK not initialized");
            } else if key_count == WootingAnalogResult::NoDevices as i32 {
                return Err("No devices connected");
            } else if key_count == WootingAnalogResult::DeviceDisconnected as i32 {
                return Err("Device disconnected");
            } else {
                return Err("Failed to read analog inputs");
            }
        }

        if key_count == 0 {
            use std::cell::Cell;
            thread_local! {
                static FRAMES_UNTIL_CHECK: Cell<u32> = Cell::new(120);
            }

            FRAMES_UNTIL_CHECK.with(|counter| {
                let remaining = counter.get();

                if remaining > 0 {
                    counter.set(remaining - 1);
                    return;
                }

                counter.set(120);
                if DEVICE_WAS_CONNECTED.load(Ordering::Relaxed) {
                    let has_devices = self.has_devices();

                    if !has_devices {
                        DEVICE_WAS_CONNECTED.store(false, Ordering::Relaxed);

                        warn!("[WOOTING_SDK] Keyboard DISCONNECTED (stopping polling, waiting for SDK callback)");

                        if let Ok(cb_guard) = KEYBOARD_STATUS_CALLBACK.lock() {
                            if let Some(callback) = *cb_guard {
                                callback(false);
                            }
                        }
                    }
                }
            });
        }

        if inputs.capacity() < key_count as usize {
            inputs.reserve(key_count as usize - inputs.capacity());
        }

        for i in 0..(key_count as usize) {
            inputs.push(AnalogInput {
                key_code: code_buffer[i] as c_int,
                analog_value: analog_buffer[i] as f64,
            });
        }

        Ok(())
    }

    pub fn get_single_key_analog(&self, keycode: u16) -> Result<f32, String> {
        if !self.is_initialized() {
            return Err("SDK not initialized".to_string());
        }

        let value = unsafe { wooting_analog_read_analog(keycode) };

        if value < 0.0 {
            let error_code = value as i32;
            if error_code == WootingAnalogResult::NoMapping as i32 {
                Ok(0.0)
            } else if error_code == WootingAnalogResult::UnInitialized as i32 {
                Err("SDK not initialized".to_string())
            } else if error_code == WootingAnalogResult::NoDevices as i32 {
                Ok(0.0)
            } else {
                Err(format!(
                    "Failed to read key {}: error code {}",
                    keycode, error_code
                ))
            }
        } else {
            Ok(value)
        }
    }

    pub fn set_keycode_mode(&self, mode: KeycodeType) -> Result<(), String> {
        if !self.is_initialized() {
            return Err("SDK not initialized".to_string());
        }

        let result = unsafe { wooting_analog_set_keycode_mode(mode) };

        if result == WootingAnalogResult::Ok {
            Ok(())
        } else if result == WootingAnalogResult::InvalidArgument {
            Err("Invalid keycode mode".to_string())
        } else if result == WootingAnalogResult::NotAvailable {
            Err("Keycode mode not available on this platform".to_string())
        } else if result == WootingAnalogResult::UnInitialized {
            Err("SDK not initialized".to_string())
        } else {
            Err(format!("Failed to set keycode mode: {:?}", result))
        }
    }

    /// Set device event callback for connect/disconnect notifications.
    pub fn set_device_event_callback(
        &self,
        callback: extern "C" fn(DeviceEventType, *mut DeviceInfo_FFI),
    ) -> Result<(), String> {
        if !self.is_initialized() {
            return Err("SDK not initialized".to_string());
        }

        let result = unsafe { wooting_analog_set_device_event_cb(callback) };

        if result == WootingAnalogResult::Ok {
            Ok(())
        } else if result == WootingAnalogResult::UnInitialized {
            Err("SDK not initialized".to_string())
        } else {
            Err(format!("Failed to set device event callback: {:?}", result))
        }
    }

    /// Clear the device event callback.
    pub fn clear_device_event_callback(&self) -> Result<(), String> {
        if !self.is_initialized() {
            return Err("SDK not initialized".to_string());
        }

        let result = unsafe { wooting_analog_clear_device_event_cb() };

        if result == WootingAnalogResult::Ok {
            Ok(())
        } else if result == WootingAnalogResult::UnInitialized {
            Err("SDK not initialized".to_string())
        } else {
            Err(format!(
                "Failed to clear device event callback: {:?}",
                result
            ))
        }
    }

    /// Check if any devices are currently connected.
    /// This is a one-time check; use the device event callback for real-time monitoring.
    pub fn has_devices(&self) -> bool {
        if !self.is_initialized() {
            return false;
        }

        unsafe {
            let mut device_buffer: [*mut DeviceInfo_FFI; 1] = [std::ptr::null_mut()];
            let result =
                wooting_analog_get_connected_devices_info(device_buffer.as_mut_ptr(), 1 as c_uint);

            result > 0
        }
    }

    pub fn cleanup(&mut self) {
        if self.initialized {
            unsafe {
                let _ = wooting_analog_clear_device_event_cb();
                let _ = wooting_analog_uninitialise();
            }
            self.initialized = false;
        }
    }
}

impl Drop for WootingSDK {
    fn drop(&mut self) {
        self.cleanup();
    }
}
