use crate::icons;
use crate::input::{self, hook::{self, HookEvent}};
use crate::keyboard::{layout, widget};
use crate::message::Message;
use crate::mouse::MouseDir;
use crate::shared_mem::WootingSharedMem;
use crate::theme;
use iced::keyboard::{self as kbd, key::Physical};
use iced::widget::{button, column, container, row, text};
use iced::{event, Alignment, Element, Event, Length, Subscription, Task, Theme};
use std::collections::{HashMap, HashSet};
use tokio::sync::mpsc;

const SCROLL_STEP: f32 = 0.08;
const SENSITIVITY_DEFAULT: u8 = 50;
const SENSITIVITY_SCALE: f32 = 1500.0;
/// EMA coefficient for smoothing raw mouse deltas.
const MOUSE_EMA: f32 = 0.35;
/// Threshold below which a direction is considered inactive and starts decaying.
const MOUSE_ACTIVE: f32 = 0.06;
/// Slow decay factor per tick (~16 ms) — reaches zero in roughly one second.
const MOUSE_DECAY: f32 = 0.92;
const MOUSE_ZERO: f32 = 0.005;

pub struct AppState {
    held:           HashSet<u16>,
    analogs:        HashMap<u16, f32>,
    hook_rx:        Option<mpsc::UnboundedReceiver<HookEvent>>,
    rows:           Vec<Vec<(String, u16, u16)>>,
    connected:      bool,
    shmem:          Option<WootingSharedMem>,
    error:          Option<String>,
    mouse_mode:     bool,
    mouse_bindings: HashMap<MouseDir, HashSet<u16>>,
    mouse_labels:   HashMap<u16, String>,
    mouse_vel:      [f32; 4],
    mouse_vx:       f32,
    mouse_vy:       f32,
    sensitivity:    u8,
    /// HID code of the key whose binding flyout is open, if any.
    popup_hid:      Option<u16>,
    /// Tick counter used to throttle reconnection attempts.
    reconnect_tick: u32,
    show_about:     bool,
}

impl AppState {
    pub fn new() -> (Self, Task<Message>) {
        let hook_rx = hook::start();

        let rows = layout::rows()
            .into_iter()
            .map(|r| r.into_iter().map(|(lbl, hid, span, _)| (lbl.to_string(), hid, span)).collect())
            .collect();

        let (shmem, connected, error) = match WootingSharedMem::open() {
            Ok(m) => { m.set_connected(true); (Some(m), true, None) }
            Err(e) => {
                log::warn!("Failed to open shared memory: {e}");
                (None, false, Some("Open Universal Analog Input to continue".to_string()))
            }
        };

        let mut mouse_bindings: HashMap<MouseDir, HashSet<u16>> = HashMap::new();
        mouse_bindings.insert(MouseDir::Up,    HashSet::from([26u16])); // W
        mouse_bindings.insert(MouseDir::Down,  HashSet::from([22u16])); // S
        mouse_bindings.insert(MouseDir::Left,  HashSet::from([ 4u16])); // A
        mouse_bindings.insert(MouseDir::Right, HashSet::from([ 7u16])); // D

        let mouse_labels = Self::build_mouse_labels(&mouse_bindings);

        (Self {
            held: HashSet::new(),
            analogs: HashMap::new(),
            hook_rx: Some(hook_rx),
            rows,
            connected,
            shmem,
            error,
            mouse_mode:    false,
            mouse_bindings,
            mouse_labels,
            mouse_vel:     [0.0; 4],
            mouse_vx:      0.0,
            mouse_vy:      0.0,
            sensitivity:    SENSITIVITY_DEFAULT,
            popup_hid:      None,
            reconnect_tick: 0,
            show_about:     false,
        }, Task::none())
    }

    pub fn update(&mut self, msg: Message) -> Task<Message> {
        match msg {
            Message::KeyDown(hid)          => self.key_down(hid),
            Message::KeyUp(hid)            => self.key_up(hid),
            Message::ToggleConnected       => self.toggle_connected(),
            Message::SensitivityChanged(v) => self.sensitivity = v,
            Message::ToggleMouseMode => {
                self.mouse_mode = !self.mouse_mode;
                if !self.mouse_mode {
                    self.mouse_vel = [0.0; 4];
                    self.mouse_vx  = 0.0;
                    self.mouse_vy  = 0.0;
                    for hids in self.mouse_bindings.values() {
                        for &hid in hids {
                            self.analogs.insert(hid, 0.0);
                            if let Some(m) = &self.shmem { m.set_analog(hid as usize, 0); }
                        }
                    }
                }
            }
            Message::KeyClicked(hid) => {
                self.popup_hid = if self.popup_hid == Some(hid) { None } else { Some(hid) };
            }
            Message::ToggleMouseDir { hid, dir } => {
                let hids = self.mouse_bindings.entry(dir).or_default();
                if !hids.remove(&hid) {
                    hids.insert(hid);
                }
                self.mouse_labels = Self::build_mouse_labels(&self.mouse_bindings);
            }
            Message::Tick => self.drain_hook(),
            Message::ToggleAbout => self.show_about = !self.show_about,
            Message::OpenUrl(url) => {
                std::process::Command::new("cmd")
                    .args(["/c", "start", "", url])
                    .spawn()
                    .ok();
            }
        }
        Task::none()
    }

    pub fn view(&self) -> Element<'_, Message> {
        let connected    = self.connected;
        let mouse_active = self.mouse_mode;
        let sens         = self.sensitivity;

        let mut top_row = row![
            button(
                row![icons::icon(icons::MOUSE).size(12), text("Mouse").size(12)]
                    .spacing(6).align_y(Alignment::Center)
            )
            .style(move |_t, _s| theme::control_button(mouse_active))
            .padding([5, 12])
            .on_press(Message::ToggleMouseMode),
        ]
        .spacing(10)
        .align_y(Alignment::Center);

        for &dir in MouseDir::all().iter() {
            let keys = self.mouse_bindings.get(&dir)
                .filter(|s| !s.is_empty())
                .map(|hids| hids.iter().map(|&h| self.hid_label(h)).collect::<Vec<_>>().join("+"))
                .unwrap_or_else(|| "—".to_string());
            top_row = top_row.push(
                row![
                    icons::icon(dir.icon_str()).size(11).color(theme::TEXT_SECONDARY),
                    text(keys).size(11).color(theme::TEXT_SECONDARY),
                ]
                .spacing(3)
                .align_y(Alignment::Center)
            );
        }

        top_row = top_row.push(
            row![
                text("Sensitivity").size(11).color(theme::TEXT_SECONDARY),
                iced::widget::slider(1u8..=100u8, sens, Message::SensitivityChanged)
                    .width(Length::Fixed(120.0)),
                text(format!("{sens}")).size(11).color(theme::TEXT_PRIMARY),
            ]
            .spacing(6)
            .align_y(Alignment::Center)
        );

        if let Some(err) = &self.error {
            top_row = top_row.push(
                row![
                    icons::icon(icons::EXCLAMATION).size(11).color(theme::WARNING),
                    text(err.as_str()).size(11).color(theme::WARNING),
                ]
                .spacing(4)
                .align_y(Alignment::Center)
            );
        }

        top_row = top_row
            .push(iced::widget::Space::new().width(Length::Fill))
            .push(
                text(if connected { "● Connected" } else { "○ Disconnected" })
                    .size(12)
                    .color(if connected { theme::SUCCESS } else { theme::TEXT_SECONDARY })
            )
            .push(
                button(text(if connected { "Disconnect" } else { "Connect" }).size(12))
                    .style(move |_t, _s| theme::control_button(connected))
                    .padding([5, 12])
                    .on_press(Message::ToggleConnected)
            );

        let mut kb_col = column![].spacing(widget::KEY_GAP);
        for row_data in &self.rows {
            kb_col = kb_col.push(widget::keyboard_row(
                row_data, &self.analogs, &self.held, &self.mouse_labels, self.popup_hid, self.mouse_mode,
            ));
        }

        // Flyout overlay: render above or below the key depending on available space.
        let kb_element: Element<Message> = match self.popup_hid
            .and_then(|h| self.find_key_grid_pos(h).map(|pos| (h, pos)))
        {
            Some((hid, (px, py))) => {
                let popup = self.render_popup(hid);

                const POPUP_H: f32 = 133.0;
                const POPUP_W: f32 = 120.0;

                let kb_h = self.rows.len() as f32 * (widget::KEY_SIZE + widget::KEY_GAP)
                    - widget::KEY_GAP;
                let max_row_w = layout::max_row_span() as f32
                    * (widget::KEY_SIZE + widget::KEY_GAP);

                let clamped_px = px.min((max_row_w - POPUP_W).max(0.0));

                let space_below = kb_h - (py + widget::KEY_SIZE);
                let popup_y = if space_below >= POPUP_H {
                    py + widget::KEY_SIZE + 4.0
                } else {
                    (py - POPUP_H - 4.0).max(0.0)
                };

                let overlay = column![
                    iced::widget::Space::new().height(popup_y),
                    row![
                        iced::widget::Space::new().width(clamped_px),
                        popup,
                    ].spacing(0),
                ].spacing(0);

                iced::widget::stack(vec![kb_col.into(), overlay.into()]).into()
            }
            None => kb_col.into(),
        };

        let hint_str = if self.popup_hid.is_some() {
            "Click a direction to assign it — click the key again to close"
        } else if self.mouse_mode {
            "Mouse mode active — move the mouse to control analog values"
        } else {
            "Hold a key + scroll wheel to adjust · click a key to bind it to mouse movement"
        };
        let bottom = row![
            text(hint_str).size(11).color(theme::TEXT_SECONDARY),
            iced::widget::Space::new().width(Length::Fill),
            text(concat!("v", env!("CARGO_PKG_VERSION"))).size(11).color(theme::TEXT_SECONDARY),
            button(text("About").size(11))
                .style(|_t, _s| theme::key_normal())
                .padding([2, 8])
                .on_press(Message::ToggleAbout),
        ]
        .spacing(8)
        .align_y(Alignment::Center);

        let content = column![top_row, kb_element, bottom]
            .spacing(6)
            .padding([6, 12])
            .width(Length::Fill);

        let base = container(content)
            .width(Length::Fill)
            .height(Length::Fill)
            .style(|_| iced::widget::container::Style {
                background: Some(theme::BG.into()),
                ..Default::default()
            });

        if self.show_about {
            let about = self.render_about();
            let overlay = container(
                column![
                    iced::widget::Space::new().height(Length::Fill),
                    row![
                        iced::widget::Space::new().width(Length::Fill),
                        about,
                        iced::widget::Space::new().width(Length::Fill),
                    ],
                    iced::widget::Space::new().height(Length::Fill),
                ]
            )
            .width(Length::Fill)
            .height(Length::Fill)
            .style(|_| iced::widget::container::Style {
                background: Some(iced::Color { r: 0.0, g: 0.0, b: 0.0, a: 0.75 }.into()),
                ..Default::default()
            });
            iced::widget::stack(vec![base.into(), overlay.into()]).into()
        } else {
            base.into()
        }
    }

    fn render_popup(&self, hid: u16) -> Element<'_, Message> {
        let label = self.hid_label(hid);

        let dir_buttons: Vec<Element<Message>> = MouseDir::all()
            .iter()
            .map(|&dir| {
                let is_bound = self.mouse_bindings.get(&dir).map_or(false, |s| s.contains(&hid));
                let content = row![
                    icons::icon(dir.icon_str()).size(11),
                    text(dir.label()).size(11),
                ]
                .spacing(5)
                .align_y(Alignment::Center);
                button(content)
                    .style(move |_t, _s| theme::mouse_dir_button(is_bound))
                    .padding([4, 10])
                    .width(Length::Fixed(120.0))
                    .on_press(Message::ToggleMouseDir { hid, dir })
                    .into()
            })
            .collect();

        let content = column(
            std::iter::once(
                text(label).size(12).color(theme::TEXT_PRIMARY).into()
            ).chain(dir_buttons)
        ).spacing(3);

        container(content)
            .style(|_| theme::popup_container())
            .padding(8)
            .into()
    }

    fn render_about(&self) -> Element<'_, Message> {
        let link_btn = |label: &'static str, url: &'static str| -> Element<Message> {
            button(text(label).size(12).color(theme::ACCENT))
                .style(|_t, _s| iced::widget::button::Style {
                    background: None,
                    border: iced::Border::default(),
                    shadow: iced::Shadow::default(),
                    text_color: theme::ACCENT,
                    snap: false,
                })
                .padding([2, 0])
                .on_press(Message::OpenUrl(url))
                .into()
        };

        let content = column![
            text("Keyboard Sim").size(20).color(theme::TEXT_PRIMARY),
            text("Analog input simulator — part of Universal Analog Input")
                .size(12).color(theme::TEXT_SECONDARY),
            text(concat!("Version ", env!("CARGO_PKG_VERSION")))
                .size(12).color(theme::TEXT_SECONDARY),
            iced::widget::Space::new().height(10),
            text("© 2025-2026 Henri DELEMAZURE. All rights reserved.")
                .size(11).color(theme::TEXT_SECONDARY),
            iced::widget::Space::new().height(14),
            link_btn(
                "GitHub — github.com/Ritonton/UniversalAnalogInput",
                "https://github.com/Ritonton/UniversalAnalogInput",
            ),
            link_btn(
                "Report an issue",
                "https://github.com/Ritonton/UniversalAnalogInput/issues",
            ),
            iced::widget::Space::new().height(14),
            text("Dependencies").size(11).color(theme::TEXT_SECONDARY),
            link_btn("Wooting Analog SDK", "https://github.com/WootingKb/wooting-analog-sdk"),
            iced::widget::Space::new().height(14),
            link_btn("MIT License", "https://github.com/Ritonton/UniversalAnalogInput/blob/master/LICENSE"),
            iced::widget::Space::new().height(16),
            button(text("Close").size(12))
                .style(|_t, _s| theme::control_button(false))
                .padding([6, 20])
                .on_press(Message::ToggleAbout),
        ]
        .spacing(3)
        .padding(20);

        container(content)
            .style(|_| theme::popup_container())
            .width(Length::Fixed(420.0))
            .into()
    }

    pub fn subscription(&self) -> Subscription<Message> {
        let tick = iced::time::every(std::time::Duration::from_millis(16))
            .map(|_| Message::Tick);

        let kbd_events = event::listen_with(|ev, _status, _id| match ev {
            Event::Keyboard(kbd::Event::KeyPressed {
                physical_key: Physical::Code(code), ..
            }) => input::physical_to_hid(&code).map(Message::KeyDown),
            Event::Keyboard(kbd::Event::KeyReleased {
                physical_key: Physical::Code(code), ..
            }) => input::physical_to_hid(&code).map(Message::KeyUp),
            _ => None,
        });

        Subscription::batch([tick, kbd_events])
    }

    pub fn theme(&self) -> Theme {
        Theme::custom(
            "UAI Dark".to_string(),
            iced::theme::Palette {
                background: theme::BG,
                text:       theme::TEXT_PRIMARY,
                primary:    theme::ACCENT,
                success:    theme::SUCCESS,
                warning:    theme::WARNING,
                danger:     theme::DANGER,
            },
        )
    }

    fn key_down(&mut self, hid: u16) {
        self.held.insert(hid);
        self.analogs.entry(hid).or_insert(0.0);
    }

    fn key_up(&mut self, hid: u16) {
        self.held.remove(&hid);
        self.analogs.insert(hid, 0.0);
        if let Some(m) = &self.shmem { m.set_analog(hid as usize, 0); }
    }

    fn scroll(&mut self, delta: f32) {
        if self.held.is_empty() || !self.connected { return; }
        for &hid in &self.held {
            let v = self.analogs.entry(hid).or_insert(0.0);
            *v = (*v + delta).clamp(0.0, 1.0);
            if let Some(m) = &self.shmem { m.set_analog(hid as usize, (*v * 255.0) as u8); }
        }
    }

    fn apply_mouse_movement(&mut self, dx: i32, dy: i32) {
        if !self.mouse_mode || !self.connected { return; }

        self.mouse_vx = self.mouse_vx * (1.0 - MOUSE_EMA) + dx as f32 * MOUSE_EMA;
        self.mouse_vy = self.mouse_vy * (1.0 - MOUSE_EMA) + dy as f32 * MOUSE_EMA;

        let sens = self.sensitivity as f32 / SENSITIVITY_SCALE;
        let incoming = [
            (-self.mouse_vy).max(0.0) * sens,
            self.mouse_vy.max(0.0)    * sens,
            (-self.mouse_vx).max(0.0) * sens,
            self.mouse_vx.max(0.0)    * sens,
        ];

        // Accumulate the maximum value per key across all contributing directions.
        let mut pending: HashMap<u16, f32> = HashMap::new();
        for (i, dir) in MouseDir::all().iter().enumerate() {
            let new_v = incoming[i].clamp(0.0, 1.0);
            self.mouse_vel[i] = if new_v > MOUSE_ACTIVE {
                new_v.max(self.mouse_vel[i])
            } else if self.mouse_vel[i] < MOUSE_ZERO {
                0.0
            } else {
                self.mouse_vel[i] * MOUSE_DECAY
            };

            if let Some(hids) = self.mouse_bindings.get(dir) {
                let val = self.mouse_vel[i];
                for &hid in hids {
                    let e = pending.entry(hid).or_insert(0.0);
                    *e = e.max(val);
                }
            }
        }

        for (hid, val) in pending {
            self.analogs.insert(hid, val);
            if let Some(m) = &self.shmem { m.set_analog(hid as usize, (val * 255.0) as u8); }
        }
    }

    fn toggle_connected(&mut self) {
        self.connected = !self.connected;
        if let Some(m) = &self.shmem {
            m.set_connected(self.connected);
            if !self.connected { m.clear_all(); self.analogs.clear(); self.held.clear(); }
        }
    }

    fn drain_hook(&mut self) {
        // Auto-reconnect: retry once per second (~60 ticks at 16 ms) to avoid
        // hammering the filesystem when UAI is not running.
        if self.shmem.is_none() {
            self.reconnect_tick += 1;
            if self.reconnect_tick >= 60 {
                self.reconnect_tick = 0;
                if let Ok(m) = WootingSharedMem::open() {
                    m.set_connected(true);
                    self.shmem     = Some(m);
                    self.connected = true;
                    self.error     = None;
                }
            }
        }

        let events: Vec<HookEvent> = match &mut self.hook_rx {
            Some(rx) => std::iter::from_fn(|| rx.try_recv().ok()).collect(),
            None => return,
        };

        let mut mouse_dx = 0i32;
        let mut mouse_dy = 0i32;

        for ev in events {
            match ev {
                HookEvent::KeyDown(vk)        => { if let Some(h) = hook::vk_to_hid(vk) { self.key_down(h); } }
                HookEvent::KeyUp(vk)          => { if let Some(h) = hook::vk_to_hid(vk) { self.key_up(h); } }
                HookEvent::WheelScrolled(d)   => { self.scroll(d * SCROLL_STEP); }
                HookEvent::MouseMoved{dx, dy} => { mouse_dx += dx; mouse_dy += dy; }
            }
        }

        self.apply_mouse_movement(mouse_dx, mouse_dy);
    }

    fn find_key_grid_pos(&self, target_hid: u16) -> Option<(f32, f32)> {
        for (row_idx, row) in self.rows.iter().enumerate() {
            let mut x = 0.0f32;
            for (_, hid, span) in row {
                if *hid == target_hid {
                    let y = row_idx as f32 * (widget::KEY_SIZE + widget::KEY_GAP);
                    return Some((x, y));
                }
                x += *span as f32 * (widget::KEY_SIZE + widget::KEY_GAP);
            }
        }
        None
    }

    fn build_mouse_labels(bindings: &HashMap<MouseDir, HashSet<u16>>) -> HashMap<u16, String> {
        let mut map: HashMap<u16, String> = HashMap::new();
        for (dir, hids) in bindings {
            for &hid in hids {
                map.entry(hid).or_default().push_str(dir.icon_str());
            }
        }
        map
    }

    fn hid_label(&self, hid: u16) -> &str {
        if hid == 0 { return "—"; }
        for row in &self.rows {
            for (label, h, _) in row {
                if *h == hid && !label.is_empty() { return label.as_str(); }
            }
        }
        "?"
    }
}

pub fn window_size() -> (f32, f32) {
    let max_span = layout::max_row_span() as f32;
    let rows     = layout::rows().len() as f32;

    let w = max_span * (widget::KEY_SIZE + widget::KEY_GAP) - widget::KEY_GAP + 24.0;
    let h = rows * (widget::KEY_SIZE + widget::KEY_GAP) - widget::KEY_GAP + 72.0;
    (w, h)
}
