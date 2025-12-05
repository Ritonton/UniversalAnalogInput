// IPC command handler invoked by the server event loop.

use log::{info, warn};
use std::path::PathBuf;
use std::sync::OnceLock;
use universal_analog_input::api;
use universal_analog_input::api::types::MappingDto;
use universal_analog_input::ipc::protocol::{IpcCommandType, IpcResponseType};
use universal_analog_input::ipc::{
    IpcCommand, IpcResponse, MappingInfo, ProfileMetadata, SubProfileMetadata,
};
use uuid::Uuid;

/// Cached UI executable path for quick launch.
static CACHED_UI_PATH: OnceLock<PathBuf> = OnceLock::new();

pub struct CommandHandler;

impl CommandHandler {
    /// Handle a single IPC command.
    pub fn handle_command(command: IpcCommand) -> IpcResponse {
        // Response correlation id.
        let message_id = command.message_id.unwrap_or(0);

        match command.command {
            IpcCommandType::StartMapping => match api::start_mapping() {
                Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                Err(e) => IpcResponse::response(message_id, IpcResponseType::Error { message: e }),
            },

            IpcCommandType::StopMapping => match api::stop_mapping() {
                Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                Err(e) => IpcResponse::response(message_id, IpcResponseType::Error { message: e }),
            },

            IpcCommandType::IsMappingActive => {
                let active = api::is_mapping_active();
                IpcResponse::response(
                    message_id,
                    IpcResponseType::IntValue {
                        value: if active { 1 } else { 0 },
                    },
                )
            }

            IpcCommandType::GetProfileMetadataCount => {
                let count = api::get_profile_metadata_count();
                IpcResponse::response(
                    message_id,
                    IpcResponseType::UintValue {
                        value: count as u32,
                    },
                )
            }

            IpcCommandType::GetProfileMetadata { index } => {
                match api::get_profile_metadata(index as usize) {
                    Some(metadata) => IpcResponse::response(
                        message_id,
                        IpcResponseType::ProfileMetadata {
                            data: ProfileMetadata::from(metadata),
                        },
                    ),
                    None => IpcResponse::response(
                        message_id,
                        IpcResponseType::Error {
                            message: "Profile metadata not found".to_string(),
                        },
                    ),
                }
            }

            IpcCommandType::GetSubProfileMetadata {
                profile_idx,
                sub_idx,
            } => match api::get_sub_profile_metadata(profile_idx as usize, sub_idx as usize) {
                Some(metadata) => IpcResponse::response(
                    message_id,
                    IpcResponseType::SubProfileMetadata {
                        data: SubProfileMetadata::from(metadata),
                    },
                ),
                None => IpcResponse::response(
                    message_id,
                    IpcResponseType::Error {
                        message: "Sub-profile metadata not found".to_string(),
                    },
                ),
            },

            IpcCommandType::SwitchProfile {
                profile_id,
                sub_profile_id,
            } => {
                let pid = bytes_to_uuid(&profile_id);
                let sid = bytes_to_uuid(&sub_profile_id);

                match api::switch_profile(&pid, &sid) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::GetCurrentMappingCount => {
                let count = api::get_current_mapping_count();
                IpcResponse::response(
                    message_id,
                    IpcResponseType::UintValue {
                        value: count as u32,
                    },
                )
            }

            IpcCommandType::GetCurrentMappingInfo { index } => {
                match api::get_current_mapping_info(index as usize) {
                    Some(mapping) => IpcResponse::response(
                        message_id,
                        IpcResponseType::MappingInfo {
                            data: MappingInfo::from(mapping),
                        },
                    ),
                    None => IpcResponse::response(
                        message_id,
                        IpcResponseType::Error {
                            message: "Mapping not found or no active profile".to_string(),
                        },
                    ),
                }
            }

            IpcCommandType::SetMapping {
                profile_id: _,
                sub_profile_id: _,
                mapping,
            } => match api::set_mapping(MappingDto::from(mapping)) {
                Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                Err(e) => IpcResponse::response(message_id, IpcResponseType::Error { message: e }),
            },

            IpcCommandType::RemoveMapping {
                profile_id: _,
                sub_profile_id: _,
                key_name,
            } => match api::remove_mapping(&key_name) {
                Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                Err(e) => IpcResponse::response(message_id, IpcResponseType::Error { message: e }),
            },

            IpcCommandType::CreateProfile { name, description } => {
                match api::create_profile(&name, &description) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::DeleteProfile { profile_id } => {
                let pid = bytes_to_uuid(&profile_id);
                match api::delete_profile(&pid) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::RenameProfile {
                profile_id,
                new_name,
            } => {
                let pid = bytes_to_uuid(&profile_id);
                match api::rename_profile(&pid, &new_name) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::UpdateProfileDescription {
                profile_id,
                description,
            } => {
                let pid = bytes_to_uuid(&profile_id);
                match api::update_profile_description(&pid, &description) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::AddSubProfile {
                profile_id,
                name,
                description,
                hotkey,
            } => {
                let pid = bytes_to_uuid(&profile_id);
                match api::add_sub_profile(&pid, &name, &description, &hotkey) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::RenameSubProfile {
                profile_id,
                sub_id,
                new_name,
            } => {
                let pid = bytes_to_uuid(&profile_id);
                let sid = bytes_to_uuid(&sub_id);
                match api::rename_sub_profile(&pid, &sid, &new_name) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::DeleteSubProfile { profile_id, sub_id } => {
                let pid = bytes_to_uuid(&profile_id);
                let sid = bytes_to_uuid(&sub_id);
                match api::delete_sub_profile(&pid, &sid) {
                    Ok(outcome) => {
                        use universal_analog_input::profile::SubProfileDeletionOutcome;
                        let code = match outcome {
                            SubProfileDeletionOutcome::ProfileRemoved => 1,
                            SubProfileDeletionOutcome::SubProfileRemoved => 0,
                        };
                        IpcResponse::response(message_id, IpcResponseType::IntValue { value: code })
                    }
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::UpdateProfileHotkey { profile_id, hotkey } => {
                let pid = bytes_to_uuid(&profile_id);
                match api::update_profile_hotkey(&pid, &hotkey) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::UpdateSubProfileHotkey {
                profile_id,
                sub_id,
                hotkey,
            } => {
                let pid = bytes_to_uuid(&profile_id);
                let sid = bytes_to_uuid(&sub_id);
                match api::update_sub_profile_hotkey(&pid, &sid, &hotkey) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::SaveProfileToFile {
                profile_id,
                file_path,
            } => {
                let pid = bytes_to_uuid(&profile_id);
                match api::save_profile_to_file(&pid, &file_path) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::LoadProfileFromFile { file_path } => {
                match api::load_profile_from_file(&file_path) {
                    Ok(_) => IpcResponse::response(message_id, IpcResponseType::Success),
                    Err(e) => {
                        IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                    }
                }
            }

            IpcCommandType::GetSupportedKeyCount => {
                let keys = api::get_supported_keys();
                IpcResponse::response(
                    message_id,
                    IpcResponseType::UintValue {
                        value: keys.len() as u32,
                    },
                )
            }

            IpcCommandType::GetSupportedKeyName { index } => {
                match api::get_supported_key_name(index as usize) {
                    Some(name) => IpcResponse::response(
                        message_id,
                        IpcResponseType::StringValue { value: name },
                    ),
                    None => IpcResponse::response(
                        message_id,
                        IpcResponseType::Error {
                            message: "Index out of bounds".to_string(),
                        },
                    ),
                }
            }

            IpcCommandType::GetGamepadControlCount => {
                let controls = api::get_gamepad_controls();
                IpcResponse::response(
                    message_id,
                    IpcResponseType::UintValue {
                        value: controls.len() as u32,
                    },
                )
            }

            IpcCommandType::GetGamepadControlName { index } => {
                match api::get_gamepad_control_name(index as usize) {
                    Some(name) => IpcResponse::response(
                        message_id,
                        IpcResponseType::StringValue { value: name },
                    ),
                    None => IpcResponse::response(
                        message_id,
                        IpcResponseType::Error {
                            message: "Index out of bounds".to_string(),
                        },
                    ),
                }
            }

            IpcCommandType::GetVersion => IpcResponse::response(
                message_id,
                IpcResponseType::StringValue {
                    value: env!("CARGO_PKG_VERSION").to_string(),
                },
            ),

            IpcCommandType::GetPerformanceMetrics => {
                let metrics = universal_analog_input::api::system::get_performance_metrics();
                IpcResponse::response(
                    message_id,
                    IpcResponseType::PerformanceMetrics { data: metrics },
                )
            }

            IpcCommandType::ShowUI => {
                if let Err(e) = launch_ui_or_bring_to_front() {
                    IpcResponse::response(message_id, IpcResponseType::Error { message: e })
                } else {
                    IpcResponse::response(message_id, IpcResponseType::Success)
                }
            }

            IpcCommandType::Shutdown => {
                // Acknowledge shutdown request
                IpcResponse::response(message_id, IpcResponseType::Success)
            }

            IpcCommandType::SuspendHotkeys => {
                api::suspend_hotkeys();
                IpcResponse::response(message_id, IpcResponseType::Success)
            }

            IpcCommandType::ResumeHotkeys => {
                api::resume_hotkeys();
                IpcResponse::response(message_id, IpcResponseType::Success)
            }
        }
    }
}

/// Convert a 16-byte array to a UUID.
fn bytes_to_uuid(bytes: &[u8; 16]) -> Uuid {
    // IPC GUIDs are little-endian to match Guid.ToByteArray / Uuid::to_bytes_le.
    Uuid::from_bytes_le(*bytes)
}

/// Cache the UI executable path at startup for reuse.
pub fn cache_ui_path() {
    let _ = CACHED_UI_PATH.get_or_init(|| match find_ui_executable_path() {
        Ok(path) => {
            info!("[CACHE] UI path cached: {:?}", path);
            path
        }
        Err(e) => {
            warn!("[CACHE] Warning: Failed to find UI path: {}", e);
            PathBuf::from("")
        }
    });
}

/// Find UI executable path.
/// Only called ONCE at startup.
fn find_ui_executable_path() -> std::result::Result<PathBuf, String> {
    let exe_path =
        std::env::current_exe().map_err(|e| format!("Failed to get current exe path: {}", e))?;

    let exe_dir = exe_path.parent().ok_or("Failed to get exe directory")?;

    let is_release_build = !cfg!(debug_assertions);

    // Try development or production locations
    let ui_exe_candidates = if is_release_build {
        vec![
            // Production: UI in ui/ subdirectory
            exe_dir.join("ui/UniversalAnalogInputUI.exe"),
            // Development Release build
            exe_dir.parent()
                .and_then(|p| p.parent())
                .map(|project_root| {
                    project_root.join("ui/UniversalAnalogInputUI/bin/Release/net9.0-windows10.0.19041.0/win-x64/UniversalAnalogInputUI.exe")
                })
                .unwrap_or_else(|| PathBuf::from("")),
            // Fallback to Debug if Release not found
            exe_dir.parent()
                .and_then(|p| p.parent())
                .map(|project_root| {
                    project_root.join("ui/UniversalAnalogInputUI/bin/Debug/net9.0-windows10.0.19041.0/win-x64/UniversalAnalogInputUI.exe")
                })
                .unwrap_or_else(|| PathBuf::from("")),
        ]
    } else {
        vec![
            // Development Debug build
            exe_dir.parent()
                .and_then(|p| p.parent())
                .map(|project_root| {
                    project_root.join("ui/UniversalAnalogInputUI/bin/Debug/net9.0-windows10.0.19041.0/win-x64/UniversalAnalogInputUI.exe")
                })
                .unwrap_or_else(|| PathBuf::from("")),
            // Fallback to Release if Debug not found
            exe_dir.parent()
                .and_then(|p| p.parent())
                .map(|project_root| {
                    project_root.join("ui/UniversalAnalogInputUI/bin/Release/net9.0-windows10.0.19041.0/win-x64/UniversalAnalogInputUI.exe")
                })
                .unwrap_or_else(|| PathBuf::from("")),
            // Production: UI in ui/ subdirectory
            exe_dir.join("ui/UniversalAnalogInputUI.exe"),
        ]
    };

    // Find first existing UI exe
    ui_exe_candidates
        .iter()
        .find(|path| path.exists())
        .ok_or_else(|| {
            format!(
                "UI executable not found in any expected location. Searched:\n\
                 1. {}/UniversalAnalogInputUI.exe\n\
                 2. <project_root>/ui/UniversalAnalogInputUI/bin/{}/net9.0-windows10.0.19041.0/win-x64/UniversalAnalogInputUI.exe\n\
                 3. <project_root>/ui/UniversalAnalogInputUI/bin/{}/net9.0-windows10.0.19041.0/win-x64/UniversalAnalogInputUI.exe",
                exe_dir.display(),
                if is_release_build { "Release" } else { "Debug" },
                if is_release_build { "Debug" } else { "Release" }
            )
        })
        .cloned()
}

/// Check if UI is running and bring to front, or launch if not running.
pub fn launch_ui_or_bring_to_front() -> std::result::Result<(), String> {
    use windows::core::w;
    use windows::Win32::System::Threading::{OpenMutexW, SYNCHRONIZATION_SYNCHRONIZE};

    // Check if UI mutex exists
    unsafe {
        match OpenMutexW(
            SYNCHRONIZATION_SYNCHRONIZE,
            false,
            w!("Global\\UniversalAnalogInput_UI"),
        ) {
            Ok(mutex_handle) => {
                info!("[IPC] UI already running - sending BringToFront notification");

                let _ = windows::Win32::Foundation::CloseHandle(mutex_handle);

                universal_analog_input::ui_notifier::send_bring_to_front_notification();

                Ok(())
            }
            Err(_) => {
                info!("[IPC] UI not running - launching new instance");
                launch_ui()
            }
        }
    }
}

/// Launch the UI using the cached path.
fn launch_ui() -> std::result::Result<(), String> {
    use std::process::Command;

    let ui_exe = CACHED_UI_PATH
        .get()
        .ok_or("UI path not cached - call cache_ui_path() first")?;

    if ui_exe.as_os_str().is_empty() {
        return Err("UI executable path is empty - was not found during startup".to_string());
    }

    info!("[IPC] Launching UI from cached path: {:?}", ui_exe);

    // Spawn process with DETACHED flag for instant return
    // CREATE_NO_WINDOW + DETACHED_PROCESS = UI starts in its own process tree
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        const CREATE_NO_WINDOW: u32 = 0x08000000;
        const DETACHED_PROCESS: u32 = 0x00000008;

        Command::new(ui_exe)
            .creation_flags(CREATE_NO_WINDOW | DETACHED_PROCESS)
            .spawn()
            .map_err(|e| format!("Failed to spawn UI process: {}", e))?;
    }

    #[cfg(not(windows))]
    {
        Command::new(ui_exe)
            .spawn()
            .map_err(|e| format!("Failed to spawn UI process: {}", e))?;
    }

    Ok(())
}
