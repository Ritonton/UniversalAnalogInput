use serde::{Deserialize, Serialize};

/// High-level analog input value used by the Rust API.
#[derive(Debug, Clone)]
pub struct AnalogInput {
    pub key_code: i32,
    pub analog_value: f64,
}

/// Aggregate performance metrics returned by the system API.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PerformanceMetrics {
    pub system: SystemMetrics,
    pub components: ComponentStatus,
    pub cache: CacheMetrics,
}

/// System-level performance data.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SystemMetrics {
    pub mapping_fps: f64,
    pub hotkey_detection_hz: f64,
    pub profile_switch_time_us: u32,
    pub ultra_performance_mode: bool,
}

/// Component availability snapshot.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ComponentStatus {
    pub wooting_sdk: ComponentState,
    pub vigem_client: ComponentState,
    pub mapping_thread: bool,
    pub hotkey_manager: bool,
}

/// Detailed component initialization state
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ComponentState {
    pub status: InitStatus,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub error: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "snake_case")]
pub enum InitStatus {
    Ok,
    Missing,
    NotInitialized,
}

impl ComponentState {
    pub fn ok() -> Self {
        Self {
            status: InitStatus::Ok,
            error: None,
        }
    }

    pub fn missing(error: String) -> Self {
        Self {
            status: InitStatus::Missing,
            error: Some(error),
        }
    }

    pub fn not_initialized() -> Self {
        Self {
            status: InitStatus::NotInitialized,
            error: None,
        }
    }

    pub fn is_healthy(&self) -> bool {
        self.status == InitStatus::Ok
    }
}

/// Cache metrics for quick diagnostics.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CacheMetrics {
    pub total_profiles: u32,
    pub total_sub_profiles: u32,
    pub current_active: bool,
    pub memory_usage_kb: u32,
    pub switch_method: String,
}

/// UI-facing profile metadata used for IPC.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ProfileMetadataDto {
    pub id: [u8; 16],
    pub name: String,
    pub description: String,
    pub sub_profile_count: u32,
    pub created_at: u64,
    pub modified_at: u64,
    pub hotkey: Option<String>,
}

/// UI-facing sub-profile metadata used for IPC.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SubProfileMetadataDto {
    pub id: [u8; 16],
    pub parent_profile_id: [u8; 16],
    pub name: String,
    pub description: String,
    pub hotkey: Option<String>,
    pub created_at: u64,
    pub modified_at: u64,
}

/// UI-facing mapping information.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MappingDto {
    pub key_name: String,
    pub gamepad_control: String,
    pub response_curve: String,
    pub dead_zone_inner: f32,
    pub dead_zone_outer: f32,
    pub use_smooth_curve: bool,
    pub custom_point_count: u32,
    pub custom_points: Vec<(f32, f32)>,
    pub created_at: u64,
}
