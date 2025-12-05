use crate::mapping::MAPPING_ENGINE;
use log::{debug, info, warn};

/// Start the mapping thread.
pub fn start_mapping() -> Result<(), String> {
    {
        use crate::{VIGEM_CLIENT, VIGEM_INIT_STATUS, WOOTING_INIT_STATUS, WOOTING_SDK};

        // Check Wooting SDK initialization status
        let wooting_status = WOOTING_INIT_STATUS
            .read()
            .map_err(|e| format!("Lock error: {}", e))?;
        if !wooting_status
            .as_ref()
            .map(|result| result.is_ok())
            .unwrap_or(false)
        {
            return Err("Wooting SDK not initialized or failed to initialize".to_string());
        }

        // Check ViGEm initialization status
        let vigem_status = VIGEM_INIT_STATUS
            .read()
            .map_err(|e| format!("Lock error: {}", e))?;
        if !vigem_status
            .as_ref()
            .map(|result| result.is_ok())
            .unwrap_or(false)
        {
            return Err("ViGEm Bus Driver not initialized or failed to initialize".to_string());
        }

        let wooting_guard = WOOTING_SDK
            .lock()
            .map_err(|e| format!("Lock error: {}", e))?;
        let vigem_guard = VIGEM_CLIENT
            .lock()
            .map_err(|e| format!("Lock error: {}", e))?;
        let engine_guard = MAPPING_ENGINE
            .lock()
            .map_err(|e| format!("Lock error: {}", e))?;

        if wooting_guard.is_none() || vigem_guard.is_none() || engine_guard.is_none() {
            return Err("Systems not initialized".to_string());
        }
    }

    {
        use crate::{VIGEM_CLIENT, WOOTING_SDK};
        let engine_guard = MAPPING_ENGINE
            .lock()
            .map_err(|e| format!("Lock error: {}", e))?;
        if let Some(ref engine) = *engine_guard {
            match engine.start_mapping(&WOOTING_SDK, &VIGEM_CLIENT) {
                Ok(_) => {
                    info!("[MAPPING] Mapping loop started (120 FPS)");
                    Ok(())
                }
                Err(e) => Err(format!("Failed to start mapping: {}", e)),
            }
        } else {
            Err("Mapping engine not initialized".to_string())
        }
    }
}

/// Stop the mapping thread.
pub fn stop_mapping() -> Result<(), String> {
    let engine_guard = MAPPING_ENGINE
        .lock()
        .map_err(|e| format!("Lock error: {}", e))?;
    if let Some(ref engine) = *engine_guard {
        engine.stop_mapping();
        debug!("[STOP] Hotkey system remains active for profile management");
    } else {
        warn!("[WARN] Mapping engine not initialized, nothing to stop");
    }
    Ok(())
}

/// Check whether the mapping thread is running.
pub fn is_mapping_active() -> bool {
    let engine_guard = MAPPING_ENGINE.lock().unwrap_or_else(|e| e.into_inner());
    if let Some(ref engine) = *engine_guard {
        engine.is_active()
    } else {
        false
    }
}
