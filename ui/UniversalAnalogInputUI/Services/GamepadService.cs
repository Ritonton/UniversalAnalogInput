using Windows.Gaming.Input;
using UniversalAnalogInputUI.Services.Interfaces;
using System;
using UniversalAnalogInputUI.Services;

namespace UniversalAnalogInputUI.Services
{
    /// <summary>Manages detection of the ViGEm virtual gamepad for the application.</summary>
    public class GamepadService : IGamepadService
    {
        private Gamepad? _vigemGamepad;
        private bool _isInitialized = false;
        private readonly object _gamepadLock = new object();

        // VID/PID for Xbox 360 controllers (including ViGEm virtual)
        private const ushort XBOX360_VID = 0x045E;
        private const ushort XBOX360_PID = 0x028E;

        public GamepadService()
        {
        }

        /// <summary>Initializes gamepad detection and subscribes to raw controller events.</summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            RawGameController.RawGameControllerAdded += OnRawGameControllerAdded;
            RawGameController.RawGameControllerRemoved += OnRawGameControllerRemoved;

            DetectViGEmGamepad();

            _isInitialized = true;
        }

        /// <summary>Unsubscribes from controller events and clears the cached gamepad reference.</summary>
        public void Shutdown()
        {
            if (!_isInitialized)
                return;

            RawGameController.RawGameControllerAdded -= OnRawGameControllerAdded;
            RawGameController.RawGameControllerRemoved -= OnRawGameControllerRemoved;

            _vigemGamepad = null;
            _isInitialized = false;
        }

        /// <summary>Returns the current ViGEm gamepad instance if detected.</summary>
        public Gamepad? GetGamepad()
        {
            lock (_gamepadLock)
            {
                return _vigemGamepad;
            }
        }

        /// <summary>Indicates whether a ViGEm gamepad is currently detected.</summary>
        public bool IsGamepadConnected()
        {
            lock (_gamepadLock)
            {
                return _vigemGamepad != null;
            }
        }

        private void OnRawGameControllerAdded(object? sender, RawGameController controller)
        {
            DetectViGEmGamepad();
        }

        private void OnRawGameControllerRemoved(object? sender, RawGameController controller)
        {
            DetectViGEmGamepad();
        }

        private void DetectViGEmGamepad()
        {
            try
            {
                var rawControllers = RawGameController.RawGameControllers;

                // Use the last Xbox 360 controller because the virtual device is usually added after physical ones.
                RawGameController? lastXbox360 = null;

                foreach (var rawController in rawControllers)
                {
                    ushort vid = rawController.HardwareVendorId;
                    ushort pid = rawController.HardwareProductId;

                    if (vid == XBOX360_VID && pid == XBOX360_PID)
                    {
                        lastXbox360 = rawController;
                    }
                }

                Gamepad? newGamepad = null;
                if (lastXbox360 != null)
                {
                    newGamepad = Gamepad.FromGameController(lastXbox360);
                }

                lock (_gamepadLock)
                {
                    _vigemGamepad = newGamepad;
                }

                CrashLogger.LogMessage($"[GamepadService] Gamepad detection: {(newGamepad != null ? "Found" : "Not found")}", "GamepadService");
            }
            catch (Exception ex)
            {
                CrashLogger.LogMessage($"[GamepadService] Detection error: {ex.Message}", "GamepadService");
                lock (_gamepadLock)
                {
                    _vigemGamepad = null;
                }
            }
        }
    }
}
