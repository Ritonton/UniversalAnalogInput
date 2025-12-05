// IPC communication module using Named Pipes with OVERLAPPED I/O
// Zero polling, event-driven bidirectional communication

pub mod protocol;
pub mod server;

pub use protocol::{
    IpcCommand, IpcResponse, MappingInfo, ProfileMetadata, SubProfileMetadata, UiEventData,
};
pub use server::IpcServer;

/// Named pipe path for communication
pub const PIPE_NAME: &str = r"\\.\pipe\universal-analog-input";
