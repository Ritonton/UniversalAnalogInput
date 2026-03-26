use iced::Font;

pub const FONT: Font = Font::with_name("bootstrap-icons");

pub const MOUSE:       &str = "\u{F499}"; // bi-mouse
pub const ARROW_UP:    &str = "\u{F148}"; // bi-arrow-up
pub const ARROW_DOWN:  &str = "\u{F128}"; // bi-arrow-down
pub const ARROW_LEFT:  &str = "\u{F12F}"; // bi-arrow-left
pub const ARROW_RIGHT: &str = "\u{F138}"; // bi-arrow-right
pub const EXCLAMATION: &str = "\u{F33A}"; // bi-exclamation-triangle

pub fn icon(codepoint: &'static str) -> iced::widget::Text<'static> {
    iced::widget::text(codepoint).font(FONT)
}
