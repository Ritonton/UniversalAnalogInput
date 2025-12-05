pub mod analog_sdk;

// Export only analog input system (digital inputs now handled by EventInputManager)
pub use analog_sdk::*;
