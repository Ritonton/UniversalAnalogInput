use crate::icons;
use crate::message::Message;
use crate::theme;
use iced::widget::{button, column, container, row, text};
use iced::{Alignment, Element, Length};
use std::collections::{HashMap, HashSet};

pub const KEY_SIZE: f32 = 52.0;
pub const KEY_GAP:  f32 = 4.0;

pub fn key<'a>(
    label: &'a str,
    hid: u16,
    span: u16,
    analog: f32,
    held: bool,
    mouse_sym: &'a str,
    popup_open: bool,
    mouse_mode: bool,
) -> Element<'a, Message> {
    let w = KEY_SIZE * span as f32 + KEY_GAP * (span as f32 - 1.0);

    if label.is_empty() || hid == 0 {
        return container(row![])
            .width(Length::Fixed(w))
            .height(Length::Fixed(KEY_SIZE))
            .into();
    }

    let pct: String = if analog > 0.001 {
        format!("{:.0}%", analog * 100.0)
    } else {
        String::new()
    };

    let mut key_col = column![
        text(label).size(11).color(theme::TEXT_PRIMARY).align_x(Alignment::Center),
    ]
    .spacing(1)
    .align_x(Alignment::Center);

    if !mouse_sym.is_empty() {
        key_col = key_col.push(
            text(mouse_sym).font(icons::FONT).size(9).color(theme::ACCENT).align_x(Alignment::Center),
        );
    }

    if !pct.is_empty() {
        key_col = key_col.push(
            text(pct).size(9).color(theme::TEXT_SECONDARY).align_x(Alignment::Center),
        );
    }

    let is_mouse_bound = !mouse_sym.is_empty();
    button(key_col)
        .width(Length::Fixed(w))
        .height(Length::Fixed(KEY_SIZE))
        .padding(4)
        .style(move |_t: &iced::Theme, _s: button::Status| {
            if popup_open {
                theme::key_popup_open()
            } else if held || analog > 0.001 {
                theme::key_held(analog)
            } else if is_mouse_bound && mouse_mode {
                theme::key_held(0.0)
            } else if is_mouse_bound {
                theme::key_mouse_bound()
            } else {
                theme::key_normal()
            }
        })
        .on_press(Message::KeyClicked(hid))
        .into()
}

pub fn keyboard_row<'a>(
    keys: &'a [(String, u16, u16)],
    analogs: &HashMap<u16, f32>,
    held: &HashSet<u16>,
    mouse_labels: &'a HashMap<u16, String>,
    popup_hid: Option<u16>,
    mouse_mode: bool,
) -> Element<'a, Message> {
    let mut r = row![].spacing(KEY_GAP);
    for (label, hid, span) in keys {
        let analog     = analogs.get(hid).copied().unwrap_or(0.0);
        let is_held    = *hid != 0 && held.contains(hid);
        let mouse_sym  = mouse_labels.get(hid).map(|s| s.as_str()).unwrap_or("");
        let popup_open = popup_hid == Some(*hid);
        r = r.push(key(label.as_str(), *hid, *span, analog, is_held, mouse_sym, popup_open, mouse_mode));
    }
    r.into()
}
