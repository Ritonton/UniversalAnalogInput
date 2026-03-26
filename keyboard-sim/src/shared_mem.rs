/// Interface with the Wooting Analog Test Plugin shared memory.
///
/// SharedState layout (offsets from the start of SharedState):
///   offset  0 : vendor_id             u16
///   offset  2 : product_id            u16
///   offset  4 : manufacturer_name     [u8; 20]
///   offset 24 : device_name           [u8; 20]
///   offset 44 : device_type           u32  (DeviceType #[repr(C)], Keyboard=1)
///   offset 48 : device_connected      bool
///   offset 49 : dirty_device_info     bool
///   offset 50 : analog_values         [u8; 0xFF]
///
/// The SharedState is preceded by a shared_memory 0.8 MetaDataHeader (meta_size = 60).

use std::env;
use std::ffi::CString;
use std::fs;
use windows::Win32::Foundation::{CloseHandle, BOOL, HANDLE};
use windows::Win32::System::Memory::{
    MapViewOfFile, OpenFileMappingA, UnmapViewOfFile, FILE_MAP, MEMORY_MAPPED_VIEW_ADDRESS,
};

const LINK_FILE: &str = "wooting-test-plugin.link";
const FILE_MAP_RW: u32 = 0x0002 | 0x0004; // FILE_MAP_READ | FILE_MAP_WRITE

#[repr(C)]
struct SharedState {
    vendor_id:         u16,
    product_id:        u16,
    manufacturer_name: [u8; 20],
    device_name:       [u8; 20],
    device_type:       u32,
    device_connected:  bool,
    dirty_device_info: bool,
    analog_values:     [u8; 0xFF],
}

#[repr(C)]
struct MetaDataHeader {
    meta_size:  u64,
    user_size:  u64,
    num_locks:  u64,
    num_events: u64,
}

pub struct WootingSharedMem {
    _mapping:  HANDLE,
    view_base: *mut u8,
    ptr:       *mut SharedState,
}

unsafe impl Send for WootingSharedMem {}

impl WootingSharedMem {
    pub fn open() -> Result<Self, String> {
        let link_path = env::temp_dir().join(LINK_FILE);

        let raw = fs::read_to_string(&link_path)
            .map_err(|e| format!("Cannot read {LINK_FILE}: {e}"))?;

        let name_str = raw.trim();
        if name_str.is_empty() {
            return Err(format!("{LINK_FILE} is empty or invalid"));
        }

        let name_c = CString::new(name_str)
            .map_err(|e| format!("Invalid mapping name: {e}"))?;

        let mapping = unsafe {
            OpenFileMappingA(
                FILE_MAP_RW,
                BOOL(0),
                windows::core::PCSTR(name_c.as_ptr() as *const u8),
            )
        }
        .map_err(|e| format!("Failed to open shared memory: {e}"))?;

        let mapped = unsafe { MapViewOfFile(mapping, FILE_MAP(FILE_MAP_RW), 0, 0, 0) };
        if mapped.Value.is_null() {
            let err = std::io::Error::last_os_error();
            unsafe { CloseHandle(mapping).ok() };
            return Err(format!("MapViewOfFile failed: {err}"));
        }

        let view_base = mapped.Value as *mut u8;

        let meta_size = unsafe {
            (*(view_base as *const MetaDataHeader)).meta_size as usize
        };

        if meta_size < std::mem::size_of::<MetaDataHeader>() || meta_size > 4096 {
            unsafe {
                UnmapViewOfFile(MEMORY_MAPPED_VIEW_ADDRESS { Value: view_base as *mut _ }).ok();
                CloseHandle(mapping).ok();
            }
            return Err(format!("Invalid meta_size ({meta_size})"));
        }

        let ptr = unsafe { view_base.add(meta_size) as *mut SharedState };
        log::info!("Wooting shared memory opened (SharedState @ {ptr:p})");

        Ok(Self { _mapping: mapping, view_base, ptr })
    }

    pub fn set_connected(&self, connected: bool) {
        unsafe { (*self.ptr).device_connected = connected; }
    }

    pub fn set_analog(&self, hid_code: usize, value: u8) {
        if hid_code < 0xFF {
            unsafe { (*self.ptr).analog_values[hid_code] = value; }
        }
    }

    pub fn clear_all(&self) {
        unsafe { (*self.ptr).analog_values.fill(0); }
    }
}

impl Drop for WootingSharedMem {
    fn drop(&mut self) {
        self.set_connected(false);
        self.clear_all();
        unsafe {
            UnmapViewOfFile(MEMORY_MAPPED_VIEW_ADDRESS { Value: self.view_base as *mut _ }).ok();
            CloseHandle(self._mapping).ok();
        }
    }
}
