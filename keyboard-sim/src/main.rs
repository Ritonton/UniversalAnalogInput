#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod app;
mod icons;
mod input;
mod keyboard;
mod message;
mod mouse;
mod shared_mem;
mod theme;

use app::{window_size, AppState};
use iced::{window, Size};

const APP_ICON:      &[u8] = include_bytes!("../assets/icon.ico");
const ICON_FONT_TTF: &[u8] = include_bytes!("../assets/bootstrap-icons.ttf");

fn load_icon() -> Option<window::icon::Icon> {
    use image::codecs::ico::IcoDecoder;
    use image::ImageDecoder;

    let cursor = std::io::Cursor::new(APP_ICON);
    let decoder = IcoDecoder::new(cursor).ok()?;
    let (w, h) = decoder.dimensions();
    let mut rgba = vec![0u8; (w * h * 4) as usize];
    decoder.read_image(&mut rgba).ok()?;

    if w == 32 && h == 32 {
        window::icon::from_rgba(rgba, w, h).ok()
    } else {
        let img = image::RgbaImage::from_raw(w, h, rgba)?;
        let resized = image::imageops::resize(&img, 32, 32, image::imageops::FilterType::Lanczos3);
        window::icon::from_rgba(resized.into_raw(), 32, 32).ok()
    }
}

fn main() -> iced::Result {
    #[cfg(debug_assertions)]
    env_logger::Builder::from_env(
        env_logger::Env::default().default_filter_or("keyboard_sim=debug"),
    )
    .init();

    let (w, h) = window_size();
    let icon = load_icon();

    iced::application(AppState::new, AppState::update, AppState::view)
        .font(ICON_FONT_TTF)
        .title("Keyboard Sim — Universal Analog Input")
        .theme(AppState::theme)
        .subscription(AppState::subscription)
        .window(window::Settings {
            size: Size::new(w, h),
            resizable: false,
            decorations: true,
            icon,
            ..Default::default()
        })
        .run()
}
