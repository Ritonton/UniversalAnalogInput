using Windows.Gaming.Input;

namespace UniversalAnalogInputUI.Services.Interfaces
{
    /// <summary>Defines detection and lifecycle management for the ViGEm virtual gamepad.</summary>
    public interface IGamepadService
    {
        /// <summary>Initializes gamepad detection and subscribes to controller events.</summary>
        void Initialize();

        /// <summary>Unsubscribes from controller events and clears cached state.</summary>
        void Shutdown();

        /// <summary>Gets the current ViGEm gamepad, or null if not detected.</summary>
        Gamepad? GetGamepad();

        /// <summary>Indicates whether a ViGEm gamepad is currently detected.</summary>
        bool IsGamepadConnected();
    }
}
