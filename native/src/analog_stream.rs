use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Mutex;

use once_cell::sync::Lazy;
use windows::core::PCWSTR;
use windows::Win32::Foundation::{CloseHandle, HANDLE, INVALID_HANDLE_VALUE};
use windows::Win32::System::Memory::{
    CreateFileMappingW, MapViewOfFile, UnmapViewOfFile, FILE_MAP_WRITE, PAGE_READWRITE,
    MEMORY_MAPPED_VIEW_ADDRESS,
};

use crate::api::types::AnalogInput;
use crate::vk_to_key_name;

pub const MAX_ENTRIES: usize = 32;
const MMF_NAME_STR: &str = "Local\\UAI_AnalogStream\0";

#[repr(C)]
pub struct AnalogEntry {
    pub analog_value: f32,
    pub key_code: u16,
    pub name_len: u8,
    pub _pad: u8,
    pub name: [u8; 16],
}

const _ASSERT_ENTRY_SIZE: () = assert!(std::mem::size_of::<AnalogEntry>() == 24);

// Shared memory layout (784 bytes):
//   [0..8]  sequence:      u64  — odd = write in progress
//   [8]     key_count:     u8
//   [9]     stream_active: u8   — 1 = running, 0 = stopped
//   [10..16] _pad
//   [16..]  entries[32] × 24 bytes
#[repr(C)]
pub struct AnalogStreamData {
    pub sequence:      u64,
    pub key_count:     u8,
    pub stream_active: u8,
    pub _pad:          [u8; 6],
    pub entries:       [AnalogEntry; MAX_ENTRIES],
}

const MMF_SIZE: usize = std::mem::size_of::<AnalogStreamData>();
const _ASSERT_DATA_SIZE: () = assert!(MMF_SIZE == 784);

// Stagnant values below GHOST_CEILING for GHOST_TICKS frames are calibration noise.
const GHOST_CEILING: f32 = 0.02;
const GHOST_EPSILON: f32 = 0.005;
const GHOST_TICKS:   u32 = 120; // 1 s at 120 Hz

pub struct AnalogStreamWriter {
    handle: HANDLE,
    view: *mut AnalogStreamData,
    ghost_tracker: HashMap<u16, (f32, u32)>, // (last_value, stale_ticks) per VK
}

// Safety: accessed only behind the global Mutex.
unsafe impl Send for AnalogStreamWriter {}
unsafe impl Sync for AnalogStreamWriter {}

impl AnalogStreamWriter {
    pub fn create() -> Result<Self, String> {
        let name_wide: Vec<u16> = MMF_NAME_STR.encode_utf16().collect();

        let handle = unsafe {
            CreateFileMappingW(
                INVALID_HANDLE_VALUE,
                None,
                PAGE_READWRITE,
                0,
                MMF_SIZE as u32,
                PCWSTR(name_wide.as_ptr()),
            )
        }
        .map_err(|e| format!("CreateFileMappingW failed: {e}"))?;

        let view = unsafe { MapViewOfFile(handle, FILE_MAP_WRITE, 0, 0, MMF_SIZE) };

        if view.Value.is_null() {
            unsafe { let _ = CloseHandle(handle); }
            return Err("MapViewOfFile returned null".to_string());
        }

        unsafe { std::ptr::write_bytes(view.Value as *mut u8, 0, MMF_SIZE) };

        Ok(Self {
            handle,
            view: view.Value as *mut AnalogStreamData,
            ghost_tracker: HashMap::new(),
        })
    }

    pub fn update(&mut self, inputs: &[AnalogInput]) {
        self.ghost_tracker.retain(|vk, _| {
            inputs.iter().any(|i| i.key_code as u16 == *vk)
        });

        let live: Vec<&AnalogInput> = inputs.iter().filter(|input| {
            let vk  = input.key_code as u16;
            let val = input.analog_value as f32;
            if val >= GHOST_CEILING {
                self.ghost_tracker.remove(&vk);
                return true;
            }
            let entry = self.ghost_tracker.entry(vk).or_insert((val, 0));
            if (val - entry.0).abs() < GHOST_EPSILON {
                entry.1 += 1;
            } else {
                entry.0 = val;
                entry.1 = 0;
            }
            entry.1 < GHOST_TICKS
        }).collect();

        let data = unsafe { &mut *self.view };

        data.sequence = data.sequence.wrapping_add(1);
        std::sync::atomic::fence(Ordering::Release);

        data.stream_active = 1;
        let count = live.len().min(MAX_ENTRIES);
        data.key_count = count as u8;

        for (i, input) in live[..count].iter().enumerate() {
            let entry = &mut data.entries[i];
            entry.analog_value = input.analog_value as f32;
            entry.key_code = input.key_code as u16;

            let name = vk_to_key_name(input.key_code as u16);
            let bytes = name.as_bytes();
            let len = bytes.len().min(15);
            entry.name_len = len as u8;
            entry.name = [0u8; 16];
            entry.name[..len].copy_from_slice(&bytes[..len]);
        }

        std::sync::atomic::fence(Ordering::Release);
        data.sequence = data.sequence.wrapping_add(1);
    }

    // Writes stream_active=0 so the UI detects the stop.
    pub fn write_stopped(&mut self) {
        let data = unsafe { &mut *self.view };
        data.sequence = data.sequence.wrapping_add(1);
        std::sync::atomic::fence(Ordering::Release);
        data.stream_active = 0;
        data.key_count = 0;
        std::sync::atomic::fence(Ordering::Release);
        data.sequence = data.sequence.wrapping_add(1);
    }
}

impl Drop for AnalogStreamWriter {
    fn drop(&mut self) {
        unsafe {
            let addr = MEMORY_MAPPED_VIEW_ADDRESS { Value: self.view as *mut _ };
            let _ = UnmapViewOfFile(addr);
            let _ = CloseHandle(self.handle);
        }
    }
}

pub static ANALOG_STREAM_ACTIVE: AtomicBool = AtomicBool::new(false);

// Separate from ANALOG_STREAM_ACTIVE: true while the UI has the page open.
// Lets start_mapping() re-enable the stream without activating it when UI is closed.
static ANALOG_STREAM_UI_REQUESTED: AtomicBool = AtomicBool::new(false);

// Kept alive across stop/start to preserve ghost_tracker state.
pub static ANALOG_STREAM_WRITER: Lazy<Mutex<Option<AnalogStreamWriter>>> =
    Lazy::new(|| Mutex::new(None));

pub fn start() -> Result<(), String> {
    let mut guard = ANALOG_STREAM_WRITER.lock().unwrap();
    if guard.is_none() {
        *guard = Some(AnalogStreamWriter::create()?);
    }
    ANALOG_STREAM_UI_REQUESTED.store(true, Ordering::Relaxed);
    ANALOG_STREAM_ACTIVE.store(true, Ordering::Relaxed);
    Ok(())
}

// Preserves UI_REQUESTED so resume_if_requested() works when mapping restarts.
pub fn pause() {
    ANALOG_STREAM_ACTIVE.store(false, Ordering::Relaxed);
    let mut guard = ANALOG_STREAM_WRITER.lock().unwrap();
    if let Some(ref mut writer) = *guard {
        writer.write_stopped();
    }
}

pub fn stop() {
    ANALOG_STREAM_UI_REQUESTED.store(false, Ordering::Relaxed);
    ANALOG_STREAM_ACTIVE.store(false, Ordering::Relaxed);
    let mut guard = ANALOG_STREAM_WRITER.lock().unwrap();
    if let Some(ref mut writer) = *guard {
        writer.write_stopped();
    }
}

pub fn resume_if_requested() {
    if ANALOG_STREAM_UI_REQUESTED.load(Ordering::Relaxed) {
        ANALOG_STREAM_ACTIVE.store(true, Ordering::Relaxed);
    }
}

pub fn cleanup() {
    ANALOG_STREAM_UI_REQUESTED.store(false, Ordering::Relaxed);
    ANALOG_STREAM_ACTIVE.store(false, Ordering::Relaxed);
    let mut guard = ANALOG_STREAM_WRITER.lock().unwrap();
    *guard = None;
}
