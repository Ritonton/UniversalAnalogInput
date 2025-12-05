// IPC server built on Tokio named pipes with a length-prefixed protocol.

use super::protocol::{IpcCommand, IpcResponse};
use super::PIPE_NAME;
use log::{error, info};
use std::sync::{Arc, Mutex};
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::windows::named_pipe::{NamedPipeServer as TokioNamedPipeServer, ServerOptions};
use tokio::sync::mpsc;

const MAX_MESSAGE_SIZE: usize = 1024 * 1024; // 1MB safety limit

/// IPC server using Tokio async I/O and Windows named pipes.
pub struct IpcServer {
    notification_tx: Arc<Mutex<Option<mpsc::UnboundedSender<IpcResponse>>>>,
    shutdown_tx: Arc<Mutex<Option<mpsc::UnboundedSender<()>>>>,
    shutdown_complete_tx: Arc<Mutex<Option<std::sync::mpsc::Sender<()>>>>,
    runtime: Mutex<Option<tokio::runtime::Runtime>>,
    connected_pipe: Mutex<Option<TokioNamedPipeServer>>,
}

impl IpcServer {
    /// Create a new IPC server.
    pub fn new() -> Result<Self, String> {
        Ok(Self {
            notification_tx: Arc::new(Mutex::new(None)),
            shutdown_tx: Arc::new(Mutex::new(None)),
            shutdown_complete_tx: Arc::new(Mutex::new(None)),
            runtime: Mutex::new(None),
            connected_pipe: Mutex::new(None),
        })
    }

    /// Request server shutdown and receive a completion signal.
    pub fn request_shutdown(&self) -> std::sync::mpsc::Receiver<()> {
        let (complete_tx, complete_rx) = std::sync::mpsc::channel();

        {
            let mut guard = self.shutdown_complete_tx.lock().unwrap();
            *guard = Some(complete_tx);
        }

        {
            let tx_guard = self.shutdown_tx.lock().unwrap();
            if let Some(ref tx) = *tx_guard {
                let _ = tx.send(());
            }
        }

        complete_rx
    }

    /// Queue a notification to be sent to the client.
    pub fn queue_notification(&self, notification: IpcResponse) {
        let tx_guard = self.notification_tx.lock().unwrap();
        if let Some(ref tx) = *tx_guard {
            let _ = tx.send(notification);
        }
    }

    /// Wait for a client connection with a timeout.
    pub fn wait_for_connection_with_timeout(
        &self,
        timeout: std::time::Duration,
    ) -> Result<(), String> {
        let mut runtime_guard = self.runtime.lock().unwrap();
        if runtime_guard.is_none() {
            *runtime_guard = Some(
                tokio::runtime::Builder::new_multi_thread()
                    .enable_all()
                    .build()
                    .map_err(|e| format!("Failed to create Tokio runtime: {}", e))?,
            );
        }

        let runtime = runtime_guard.as_ref().unwrap();

        runtime.block_on(async {
            let server = ServerOptions::new()
                .first_pipe_instance(true)
                .create(PIPE_NAME)
                .map_err(|e| format!("Failed to create named pipe: {}", e))?;

            info!(
                "[IPC] Waiting for client connection (timeout: {:?})...",
                timeout
            );

            match tokio::time::timeout(timeout, server.connect()).await {
                Ok(Ok(())) => {
                    info!("[IPC] Client connected");

                    let mut pipe_guard = self.connected_pipe.lock().unwrap();
                    *pipe_guard = Some(server);

                    Ok(())
                }
                Ok(Err(e)) => Err(format!("Connection failed: {}", e)),
                Err(_) => Err("Connection timeout - no client connected".to_string()),
            }
        })
    }

    /// Start the event-driven server loop for a single client connection.
    /// Returns when the client disconnects.
    pub fn run_event_loop<F>(&self, handler: F) -> Result<(), String>
    where
        F: Fn(IpcCommand) -> IpcResponse + Send + 'static,
    {
        let mut runtime_guard = self.runtime.lock().unwrap();
        if runtime_guard.is_none() {
            *runtime_guard = Some(
                tokio::runtime::Builder::new_multi_thread()
                    .enable_all()
                    .build()
                    .map_err(|e| format!("Failed to create Tokio runtime: {}", e))?,
            );
        }

        let runtime = runtime_guard.as_ref().unwrap();
        let notification_tx_arc = Arc::clone(&self.notification_tx);
        let shutdown_tx_arc = Arc::clone(&self.shutdown_tx);
        let shutdown_complete_tx_arc = Arc::clone(&self.shutdown_complete_tx);

        runtime.block_on(async {
            info!("[IPC] Starting event-driven server on {}", PIPE_NAME);

            let mut server = {
                let mut pipe_guard = self.connected_pipe.lock().unwrap();
                pipe_guard.take().ok_or_else(|| {
                    "No connected pipe - call wait_for_connection_with_timeout first".to_string()
                })?
            };

            info!("[IPC] Using already-connected pipe, handling messages...");

            let (notif_tx, notif_rx) = mpsc::unbounded_channel();
            {
                let mut tx_guard = notification_tx_arc.lock().unwrap();
                *tx_guard = Some(notif_tx);
            }

            let (shutdown_tx, shutdown_rx) = mpsc::unbounded_channel();
            {
                let mut tx_guard = shutdown_tx_arc.lock().unwrap();
                *tx_guard = Some(shutdown_tx);
            }

            crate::ui_notifier::send_current_keyboard_status();

            let result = handle_client(&mut server, &handler, notif_rx, shutdown_rx).await;

            {
                let mut tx_guard = notification_tx_arc.lock().unwrap();
                *tx_guard = None;
            }
            {
                let mut tx_guard = shutdown_tx_arc.lock().unwrap();
                *tx_guard = None;
            }

            {
                let mut tx_guard = shutdown_complete_tx_arc.lock().unwrap();
                if let Some(tx) = tx_guard.take() {
                    let _ = tx.send(());
                    info!("[IPC] Shutdown completion signaled");
                }
            }

            info!("[IPC] Client disconnected");
            result.map_err(|e| format!("Client handler error: {}", e))
        })
    }

    /// Check if a client is connected.
    pub fn is_connected(&self) -> bool {
        self.notification_tx.lock().unwrap().is_some()
    }

    /// Disconnect the current client (no-op).
    pub fn disconnect(&self) {}

    /// Signal shutdown (no-op).
    pub fn signal_shutdown(&self) {}
}

/// Handle a single client connection with the length-prefixed protocol.
async fn handle_client<F>(
    pipe: &mut TokioNamedPipeServer,
    handler: &F,
    mut notification_rx: mpsc::UnboundedReceiver<IpcResponse>,
    mut shutdown_rx: mpsc::UnboundedReceiver<()>,
) -> Result<(), Box<dyn std::error::Error>>
where
    F: Fn(IpcCommand) -> IpcResponse,
{
    use super::protocol::IpcResponseType;

    loop {
        tokio::select! {
            msg_result = read_message(pipe) => {
                match msg_result {
                    Ok(msg) => {
                        let response = handler(msg);

                        if let Err(e) = write_message(pipe, &response).await {
                            error!("[IPC] Write error: {}", e);
                            break;
                        }
                    }
                    Err(e) => {
                        error!("[IPC] Read error: {}", e);
                        break;
                    }
                }
            }

            Some(notification) = notification_rx.recv() => {
                if let Err(e) = write_message(pipe, &notification).await {
                    error!("[IPC] Notification write error: {}", e);
                    break;
                }
            }

            Some(_) = shutdown_rx.recv() => {
                info!("[IPC] Shutdown signal received - sending Shutdown notification");
                let shutdown_notif = IpcResponse::notification(IpcResponseType::Shutdown);

                if let Err(e) = write_message(pipe, &shutdown_notif).await {
                    error!("[IPC] Failed to send Shutdown notification: {}", e);
                    break;
                }

                info!("[IPC] Shutdown notification sent successfully");

                match read_message(pipe).await {
                    Err(_) => {
                        info!("[IPC] Client disconnected after shutdown (expected)");
                    }
                    Ok(_) => {
                        info!("[IPC] Received unexpected message after shutdown");
                    }
                }

                break;
            }

            else => {
                break;
            }
        }
    }

    Ok(())
}

/// Read a length-prefixed message from the pipe.
async fn read_message(
    pipe: &mut TokioNamedPipeServer,
) -> Result<IpcCommand, Box<dyn std::error::Error>> {
    info!("[IPC] Waiting to read message length...");

    let mut len_buf = [0u8; 4];
    pipe.read_exact(&mut len_buf).await?;
    let len = u32::from_le_bytes(len_buf) as usize;

    info!("[IPC] Message length: {} bytes", len);

    if len > MAX_MESSAGE_SIZE {
        return Err(format!("Message too large: {} bytes", len).into());
    }

    let mut payload = vec![0u8; len];
    pipe.read_exact(&mut payload).await?;

    info!(
        "[IPC] Received payload: {}",
        String::from_utf8_lossy(&payload)
    );

    let command = IpcCommand::from_bytes(&payload)?;
    info!("[IPC] Parsed command: {:?}", command);
    Ok(command)
}

/// Write a length-prefixed message to the pipe.
async fn write_message(
    pipe: &mut TokioNamedPipeServer,
    msg: &IpcResponse,
) -> Result<(), Box<dyn std::error::Error>> {
    let payload = msg.to_bytes()?;

    info!(
        "[IPC] Sending response: {} ({} bytes)",
        String::from_utf8_lossy(&payload),
        payload.len()
    );

    let len = payload.len() as u32;
    pipe.write_all(&len.to_le_bytes()).await?;

    pipe.write_all(&payload).await?;

    pipe.flush().await?;

    info!("[IPC] Response sent successfully");

    Ok(())
}

// Safe to send/sync with Tokio runtime
unsafe impl Send for IpcServer {}
unsafe impl Sync for IpcServer {}
