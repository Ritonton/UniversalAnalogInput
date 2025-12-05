// IPC message protocol using JSON serialization
use serde::{Deserialize, Serialize};

/// Wrapper for IPC commands with correlation ID
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct IpcCommand {
    /// Message ID for request/response correlation (optional)
    #[serde(skip_serializing_if = "Option::is_none")]
    pub message_id: Option<u32>,

    /// The actual command (flattened into the same JSON object)
    #[serde(flatten)]
    pub command: IpcCommandType,
}

/// Commands that can be sent from UI to tray app
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum IpcCommandType {
    // Mapping operations
    StartMapping,
    StopMapping,
    IsMappingActive,

    // Profile operations
    GetProfileMetadataCount,
    GetProfileMetadata {
        index: u32,
    },
    GetSubProfileMetadata {
        profile_idx: u32,
        sub_idx: u32,
    },
    SwitchProfile {
        profile_id: [u8; 16],
        sub_profile_id: [u8; 16],
    },
    GetCurrentMappingCount,
    GetCurrentMappingInfo {
        index: u32,
    },

    // Mapping CRUD
    SetMapping {
        profile_id: [u8; 16],
        sub_profile_id: [u8; 16],
        mapping: MappingInfo,
    },
    RemoveMapping {
        profile_id: [u8; 16],
        sub_profile_id: [u8; 16],
        key_name: String,
    },

    // Profile CRUD
    CreateProfile {
        name: String,
        description: String,
    },
    RenameProfile {
        profile_id: [u8; 16],
        new_name: String,
    },
    UpdateProfileDescription {
        profile_id: [u8; 16],
        description: String,
    },
    DeleteProfile {
        profile_id: [u8; 16],
    },

    // Sub-profile CRUD
    AddSubProfile {
        profile_id: [u8; 16],
        name: String,
        description: String,
        hotkey: String,
    },
    RenameSubProfile {
        profile_id: [u8; 16],
        sub_id: [u8; 16],
        new_name: String,
    },
    DeleteSubProfile {
        profile_id: [u8; 16],
        sub_id: [u8; 16],
    },
    UpdateProfileHotkey {
        profile_id: [u8; 16],
        hotkey: String,
    },
    UpdateSubProfileHotkey {
        profile_id: [u8; 16],
        sub_id: [u8; 16],
        hotkey: String,
    },

    // Import/Export
    SaveProfileToFile {
        profile_id: [u8; 16],
        file_path: String,
    },
    LoadProfileFromFile {
        file_path: String,
    },

    // Enumeration
    GetSupportedKeyCount,
    GetSupportedKeyName {
        index: u32,
    },
    GetGamepadControlCount,
    GetGamepadControlName {
        index: u32,
    },

    // System
    GetVersion,
    GetPerformanceMetrics, // Get detailed system metrics including dependency status
    ShowUI,                // Request tray to launch the WinUI 3 app
    Shutdown,

    // Hotkey Control (suspend when dialogs open)
    SuspendHotkeys,
    ResumeHotkeys,
}

use crate::api::types::{MappingDto, ProfileMetadataDto, SubProfileMetadataDto};

/// Wrapper for IPC responses with correlation ID
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct IpcResponse {
    /// Message ID for request/response correlation
    /// If present, this is a response to a specific request
    /// If None, this is an unsolicited notification
    pub message_id: Option<u32>,

    /// The actual response (flattened into the same JSON object)
    #[serde(flatten)]
    pub response: IpcResponseType,
}

/// Response types sent from tray app to UI
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type")]
pub enum IpcResponseType {
    Success,
    Error {
        message: String,
    },
    IntValue {
        value: i32,
    },
    UintValue {
        value: u32,
    },
    StringValue {
        value: String,
    },
    ProfileMetadata {
        data: ProfileMetadata,
    },
    SubProfileMetadata {
        data: SubProfileMetadata,
    },
    MappingInfo {
        data: MappingInfo,
    },
    PerformanceMetrics {
        data: crate::api::types::PerformanceMetrics,
    },
    UiEvent {
        data: Option<UiEventData>,
    }, // None if no events pending
    Shutdown, // Notification from tray to UI: tray is closing, UI should exit
    KeyboardStatus {
        connected: bool,
    }, // Notification: keyboard connection status changed
    BringToFront, // Notification: bring UI window to foreground
}

/// Profile metadata structure for IPC
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ProfileMetadata {
    pub id: [u8; 16],
    pub name: String,
    pub description: String,
    pub sub_profile_count: u32,
    pub created_at: i64,
    pub modified_at: i64,
    pub hotkey: String,
}

impl From<ProfileMetadataDto> for ProfileMetadata {
    fn from(dto: ProfileMetadataDto) -> Self {
        Self {
            id: dto.id,
            name: dto.name,
            description: dto.description,
            sub_profile_count: dto.sub_profile_count,
            created_at: dto.created_at as i64,
            modified_at: dto.modified_at as i64,
            hotkey: dto.hotkey.unwrap_or_else(|| "None".to_string()),
        }
    }
}

/// Sub-profile metadata structure for IPC
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SubProfileMetadata {
    pub id: [u8; 16],
    pub parent_profile_id: [u8; 16],
    pub name: String,
    pub description: String,
    pub hotkey: String,
    pub created_at: i64,
    pub modified_at: i64,
}

impl From<SubProfileMetadataDto> for SubProfileMetadata {
    fn from(dto: SubProfileMetadataDto) -> Self {
        Self {
            id: dto.id,
            parent_profile_id: dto.parent_profile_id,
            name: dto.name,
            description: dto.description,
            hotkey: dto.hotkey.unwrap_or_else(|| "None".to_string()),
            created_at: dto.created_at as i64,
            modified_at: dto.modified_at as i64,
        }
    }
}

/// Mapping information structure for IPC
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MappingInfo {
    pub key_name: String,
    pub gamepad_control: String,
    pub response_curve: String,
    pub dead_zone_inner: f32,
    pub dead_zone_outer: f32,
    pub use_smooth_curve: bool,
    pub custom_point_count: u32,
    pub custom_points: Vec<(f32, f32)>, // Up to 16 points
    pub created_at: i64,
}

impl From<MappingDto> for MappingInfo {
    fn from(dto: MappingDto) -> Self {
        let custom_points = dto
            .custom_points
            .into_iter()
            .take(16)
            .collect::<Vec<(f32, f32)>>();
        let custom_point_count = custom_points.len() as u32;

        Self {
            key_name: dto.key_name,
            gamepad_control: dto.gamepad_control,
            response_curve: dto.response_curve,
            dead_zone_inner: dto.dead_zone_inner,
            dead_zone_outer: dto.dead_zone_outer,
            use_smooth_curve: dto.use_smooth_curve,
            custom_point_count,
            custom_points,
            created_at: dto.created_at as i64,
        }
    }
}

impl From<MappingInfo> for MappingDto {
    fn from(info: MappingInfo) -> Self {
        let custom_points = info
            .custom_points
            .into_iter()
            .take(16)
            .collect::<Vec<(f32, f32)>>();
        let custom_point_count = custom_points.len() as u32;

        Self {
            key_name: info.key_name,
            gamepad_control: info.gamepad_control,
            response_curve: info.response_curve,
            dead_zone_inner: info.dead_zone_inner,
            dead_zone_outer: info.dead_zone_outer,
            use_smooth_curve: info.use_smooth_curve,
            custom_point_count,
            custom_points,
            created_at: info.created_at as u64,
        }
    }
}

impl IpcCommand {
    /// Parse command from JSON string
    pub fn from_json(json: &str) -> Result<Self, serde_json::Error> {
        serde_json::from_str(json)
    }

    /// Serialize command to JSON string
    pub fn to_json(&self) -> Result<String, serde_json::Error> {
        serde_json::to_string(self)
    }
}

impl IpcCommand {
    /// Create a command for a request (with message_id)
    pub fn request(message_id: u32, command: IpcCommandType) -> Self {
        Self {
            message_id: Some(message_id),
            command,
        }
    }

    /// Create a command without correlation (for fire-and-forget)
    pub fn fire_and_forget(command: IpcCommandType) -> Self {
        Self {
            message_id: None,
            command,
        }
    }

    /// Parse command from bytes (for length-prefixed protocol)
    pub fn from_bytes(bytes: &[u8]) -> Result<Self, serde_json::Error> {
        serde_json::from_slice(bytes)
    }
}

impl IpcResponse {
    /// Create a response to a request (with message_id)
    pub fn response(message_id: u32, response: IpcResponseType) -> Self {
        Self {
            message_id: Some(message_id),
            response,
        }
    }

    /// Create a notification (no message_id)
    pub fn notification(response: IpcResponseType) -> Self {
        Self {
            message_id: None,
            response,
        }
    }

    /// Parse response from JSON string
    pub fn from_json(json: &str) -> Result<Self, serde_json::Error> {
        serde_json::from_str(json)
    }

    /// Serialize response to JSON string
    pub fn to_json(&self) -> Result<String, serde_json::Error> {
        serde_json::to_string(self)
    }

    /// Serialize response to bytes (for length-prefixed protocol)
    pub fn to_bytes(&self) -> Result<Vec<u8>, serde_json::Error> {
        serde_json::to_vec(self)
    }
}

/// UI Event data for IPC
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct UiEventData {
    pub event_type: u32,
    pub profile_id: [u8; 16],
    pub sub_profile_id: [u8; 16],
}
