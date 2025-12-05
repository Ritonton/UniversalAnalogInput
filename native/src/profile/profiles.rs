use crate::curves::{CurveProcessor, UnifiedCurve};
use log::debug;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::time::{SystemTime, UNIX_EPOCH};
use uuid::Uuid;

fn generate_uuid() -> Uuid {
    Uuid::new_v4()
}

pub fn now_timestamp() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs()
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct GameProfile {
    #[serde(default = "generate_uuid")]
    pub id: Uuid,
    pub name: String,
    pub description: String,
    pub game_path: Option<String>,
    pub sub_profiles: Vec<SubProfile>,
    #[serde(default = "now_timestamp")]
    pub created_at: u64,
    #[serde(default = "now_timestamp")]
    pub modified_at: u64,
    #[serde(default)]
    pub hotkey: Option<HotKey>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SubProfile {
    #[serde(default = "generate_uuid")]
    pub id: Uuid,
    pub name: String,
    pub description: String,
    pub hotkey: Option<HotKey>,
    pub mappings: Vec<KeyMapping>,
    #[serde(default = "now_timestamp")]
    pub created_at: u64,
    #[serde(default = "now_timestamp")]
    pub modified_at: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct KeyMapping {
    pub key_name: String, // Display key name ("W", "Space", "F1")
    pub gamepad_control: GamepadControl,
    pub response_curve: ResponseCurve,
    pub dead_zone_inner: f32, // Inner dead zone (0.0 - 1.0)
    pub dead_zone_outer: f32, // Outer dead zone (0.0 - 1.0)
    pub curve_params: CurveParams,
    #[serde(default = "now_timestamp")]
    pub created_at: u64,
    #[serde(default = "now_timestamp")]
    pub modified_at: u64,
}

impl KeyMapping {
    /// Get VK code for internal use (EventInputManager, WootingSDK)
    pub fn get_vk_code(&self) -> u16 {
        crate::conversions::key_name_to_vk(&self.key_name)
    }
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize)]
pub enum GamepadControl {
    LeftStickUp,
    LeftStickDown,
    LeftStickLeft,
    LeftStickRight,
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight,
    LeftTrigger,
    RightTrigger,
    ButtonA,
    ButtonB,
    ButtonX,
    ButtonY,
    LeftShoulder,
    RightShoulder,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize, PartialEq, Eq)]
pub enum ResponseCurve {
    Linear,
    Custom,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CurveParams {
    pub use_smooth_interpolation: bool, // For custom curves: true=smooth, false=linear
    pub custom_points: Vec<(f32, f32)>, // Custom curve points
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq, Hash)]
#[serde(rename_all = "camelCase")]
pub struct HotKey {
    pub key_name: String, // Key name ("F1", "F2", etc.)
    pub modifiers: u8,    // Ctrl=1, Alt=2, Shift=4, Win=8
}

impl HotKey {
    /// Get VK code for internal use (EventInputManager)
    pub fn get_vk_code(&self) -> u16 {
        crate::conversions::key_name_to_vk(&self.key_name)
    }
}

#[derive(Debug, Clone)]
pub struct CompiledProfile {
    pub mappings: HashMap<u16, CompiledMapping>,
    pub hotkey: Option<HotKey>,
}

#[derive(Debug, Clone)]
pub struct CompiledMapping {
    pub gamepad_control: GamepadControl,
    pub curve: UnifiedCurve,
}
pub type CurveFunction = fn(f32) -> f32;

impl Default for CurveParams {
    fn default() -> Self {
        Self {
            use_smooth_interpolation: false,
            custom_points: Vec::new(),
        }
    }
}

impl Default for KeyMapping {
    fn default() -> Self {
        let now = now_timestamp();
        Self {
            key_name: "Unknown".to_string(),
            gamepad_control: GamepadControl::LeftStickUp,
            response_curve: ResponseCurve::Linear,
            dead_zone_inner: 0.05,
            dead_zone_outer: 0.95,
            curve_params: CurveParams::default(),
            created_at: now,
            modified_at: now,
        }
    }
}

impl GameProfile {
    pub fn new(name: String) -> Self {
        let now = now_timestamp();
        Self {
            id: generate_uuid(),
            name,
            description: String::new(),
            game_path: None,
            sub_profiles: vec![SubProfile::default()],
            created_at: now,
            modified_at: now,
            hotkey: None,
        }
    }

    pub fn compile_profile(&self, sub_profile_name: &str) -> Option<CompiledProfile> {
        let sub_profile = self
            .sub_profiles
            .iter()
            .find(|sp| sp.name == sub_profile_name)?;

        let mut mappings = HashMap::new();

        for mapping in &sub_profile.mappings {
            debug!(
                "[PROFILE] Compiling mapping '{}': curve={:?}, {} custom points, smooth={}",
                mapping.key_name,
                mapping.response_curve,
                mapping.curve_params.custom_points.len(),
                mapping.curve_params.use_smooth_interpolation
            );

            let compiled = CompiledMapping {
                gamepad_control: mapping.gamepad_control,
                curve: UnifiedCurve::new(
                    mapping.response_curve,
                    mapping.curve_params.clone(),
                    mapping.dead_zone_inner,
                    mapping.dead_zone_outer,
                ),
            };
            mappings.insert(mapping.get_vk_code(), compiled);
        }

        Some(CompiledProfile {
            mappings,
            hotkey: sub_profile.hotkey.clone(),
        })
    }
}

impl SubProfile {
    pub fn new(
        name: String,
        description: String,
        hotkey: Option<HotKey>,
        mappings: Vec<KeyMapping>,
    ) -> Self {
        let now = now_timestamp();
        Self {
            id: generate_uuid(),
            name,
            description,
            hotkey,
            mappings,
            created_at: now,
            modified_at: now,
        }
    }
}

impl Default for SubProfile {
    fn default() -> Self {
        Self::new(
            "Movement".to_string(),
            "Basic WASD movement controls".to_string(),
            Some(HotKey {
                key_name: "F1".to_string(),
                modifiers: 0,
            }),
            Vec::new(),
        )
    }
}

impl CompiledMapping {
    /// Apply dead zones and curve transformation to input value.
    #[inline(always)]
    pub fn process_input(&self, raw_value: f32) -> f32 {
        self.curve.process_input(raw_value)
    }
}
