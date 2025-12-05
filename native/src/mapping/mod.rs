pub mod engine;

pub use engine::*;

use std::sync::Mutex;

// Global mapping engine instance
pub static MAPPING_ENGINE: Mutex<Option<MappingEngine>> = Mutex::new(None);
