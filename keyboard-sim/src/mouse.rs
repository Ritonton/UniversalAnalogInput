#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum MouseDir {
    Up,
    Down,
    Left,
    Right,
}

impl MouseDir {
    pub fn icon_str(self) -> &'static str {
        match self {
            Self::Up    => crate::icons::ARROW_UP,
            Self::Down  => crate::icons::ARROW_DOWN,
            Self::Left  => crate::icons::ARROW_LEFT,
            Self::Right => crate::icons::ARROW_RIGHT,
        }
    }

    pub fn label(self) -> &'static str {
        match self {
            Self::Up    => "Up",
            Self::Down  => "Down",
            Self::Left  => "Left",
            Self::Right => "Right",
        }
    }

    pub fn all() -> [MouseDir; 4] {
        [Self::Up, Self::Down, Self::Left, Self::Right]
    }
}
