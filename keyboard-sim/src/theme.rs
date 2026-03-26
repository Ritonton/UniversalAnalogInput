use iced::widget::button;
use iced::{Border, Color, Shadow};

pub const BG:             Color = Color::from_rgb(0.125, 0.125, 0.125);
pub const SURFACE:        Color = Color::from_rgb(0.176, 0.176, 0.176);
pub const SURFACE_HIGH:   Color = Color::from_rgb(0.220, 0.220, 0.220);
pub const BORDER:         Color = Color::from_rgb(0.271, 0.271, 0.271);
pub const ACCENT:         Color = Color::from_rgb(0.133, 0.773, 0.369);
pub const TEXT_PRIMARY:   Color = Color::WHITE;
pub const TEXT_SECONDARY: Color = Color::from_rgb(0.671, 0.671, 0.671);
pub const SUCCESS:        Color = Color::from_rgb(0.365, 0.765, 0.392);
pub const DANGER:         Color = Color::from_rgb(0.937, 0.349, 0.349);
pub const WARNING:        Color = Color::from_rgb(1.0, 0.647, 0.0);

pub fn key_normal() -> button::Style {
    button::Style {
        background: Some(SURFACE.into()),
        text_color: TEXT_PRIMARY,
        border: Border { color: BORDER, width: 1.0, radius: 4.0.into() },
        shadow: Shadow::default(),
        snap: false,
    }
}

/// Color interpolated from SURFACE to ACCENT based on the analog value.
pub fn key_held(analog: f32) -> button::Style {
    let t = analog.clamp(0.0, 1.0);
    let bg = Color::from_rgb(
        SURFACE.r + t * (ACCENT.r - SURFACE.r),
        SURFACE.g + t * (ACCENT.g - SURFACE.g),
        SURFACE.b + t * (ACCENT.b - SURFACE.b),
    );
    button::Style {
        background: Some(bg.into()),
        text_color: TEXT_PRIMARY,
        border: Border { color: ACCENT, width: 1.5, radius: 4.0.into() },
        shadow: Shadow {
            color: Color { a: 0.4 * t, ..ACCENT },
            offset: iced::Vector::new(0.0, 0.0),
            blur_radius: 6.0 * t,
        },
        snap: false,
    }
}

/// Key bound to a mouse direction — distinct background, no accent.
pub fn key_mouse_bound() -> button::Style {
    button::Style {
        background: Some(SURFACE_HIGH.into()),
        text_color: TEXT_PRIMARY,
        border: Border { color: BORDER, width: 1.0, radius: 4.0.into() },
        shadow: Shadow::default(),
        snap: false,
    }
}

/// Direction button inside the binding flyout — accent only when selected.
pub fn mouse_dir_button(active: bool) -> button::Style {
    button::Style {
        background: Some(if active { ACCENT } else { SURFACE_HIGH }.into()),
        text_color: TEXT_PRIMARY,
        border: Border {
            color: if active { ACCENT } else { BORDER },
            width: 1.0,
            radius: 6.0.into(),
        },
        shadow: Shadow::default(),
        snap: false,
    }
}

/// Key whose flyout is open — elevated background, no accent.
pub fn key_popup_open() -> button::Style {
    button::Style {
        background: Some(SURFACE_HIGH.into()),
        text_color: TEXT_PRIMARY,
        border: Border { color: BORDER, width: 1.0, radius: 4.0.into() },
        shadow: Shadow::default(),
        snap: false,
    }
}

pub fn popup_container() -> iced::widget::container::Style {
    iced::widget::container::Style {
        background: Some(Color::from_rgb(0.11, 0.11, 0.11).into()),
        border: iced::Border { color: BORDER, width: 1.0, radius: 8.0.into() },
        shadow: iced::Shadow {
            color: Color { r: 0.0, g: 0.0, b: 0.0, a: 0.65 },
            offset: iced::Vector::new(0.0, 4.0),
            blur_radius: 14.0,
        },
        text_color: None,
        snap: false,
    }
}

pub fn control_button(active: bool) -> button::Style {
    let bg = if active { SUCCESS } else { SURFACE_HIGH };
    button::Style {
        background: Some(bg.into()),
        text_color: TEXT_PRIMARY,
        border: Border {
            color: if active { SUCCESS } else { BORDER },
            width: 1.0,
            radius: 6.0.into(),
        },
        shadow: Shadow::default(),
        snap: false,
    }
}
