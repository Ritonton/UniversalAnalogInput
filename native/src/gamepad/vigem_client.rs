use std::sync::atomic::{AtomicU64, Ordering};
use vigem_client::{Client, TargetId, Xbox360Wired};

pub struct ViGEmClient {
    client: Option<Client>,
    controller: Option<Xbox360Wired<Client>>,
    initialized: bool,
    errors: AtomicU64,
}

impl ViGEmClient {
    pub fn new() -> Self {
        Self {
            client: None,
            controller: None,
            initialized: false,
            errors: AtomicU64::new(0),
        }
    }

    pub fn initialize(&mut self) -> Result<(), String> {
        if self.initialized {
            return Ok(());
        }

        // Connect to ViGEm Bus
        let client = Client::connect().map_err(|e| {
            format!("Failed to connect to ViGEm Bus. Make sure ViGEm Bus Driver is installed. Error: {}", e)
        })?;

        // Create Xbox 360 controller
        let target_id = TargetId::XBOX360_WIRED;
        let mut controller = Xbox360Wired::new(
            client
                .try_clone()
                .map_err(|e| format!("Failed to clone client: {}", e))?,
            target_id,
        );

        // Plugin the virtual controller
        controller
            .plugin()
            .map_err(|e| format!("Failed to plugin virtual controller: {}", e))?;

        // Wait for controller to be ready
        controller
            .wait_ready()
            .map_err(|e| format!("Virtual controller failed to become ready: {}", e))?;

        self.client = Some(client);
        self.controller = Some(controller);
        self.initialized = true;

        Ok(())
    }

    pub fn is_initialized(&self) -> bool {
        self.initialized && self.controller.is_some()
    }

    /// Update gamepad with pre-built ViGEm XGamepad report
    /// Used by atomic gamepad state for maximum performance
    pub fn update_from_vigem_gamepad(
        &mut self,
        vigem_gamepad: &vigem_client::XGamepad,
    ) -> Result<(), String> {
        if !self.is_initialized() {
            return Err("ViGEm client not initialized".to_string());
        }

        let controller = self.controller.as_mut().unwrap();
        if let Err(e) = controller.update(vigem_gamepad) {
            self.errors.fetch_add(1, Ordering::Relaxed);
            return Err(format!("Failed to update virtual controller: {}", e));
        }

        Ok(())
    }

    pub fn get_error_count(&self) -> u64 {
        self.errors.load(Ordering::Relaxed)
    }

    pub fn cleanup(&mut self) {
        if let Some(mut controller) = self.controller.take() {
            let _ = controller.unplug();
        }
        self.client = None;
        self.initialized = false;
    }
}

impl Drop for ViGEmClient {
    fn drop(&mut self) {
        self.cleanup();
    }
}

// Xbox 360 button mapping
#[repr(u16)]
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum XboxButton {
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Start = 0x0010,
    Back = 0x0020,
    LeftThumb = 0x0040,
    RightThumb = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,
    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}
