using System;

namespace UniversalAnalogInputUI.Services.Factories;

/// <summary>Provides the native window handle once the main window is created.</summary>
public interface IWindowHandleProvider
{
    IntPtr Handle { get; }
    event EventHandler<IntPtr>? HandleChanged;
    void SetHandle(IntPtr handle);
}

public sealed class WindowHandleProvider : IWindowHandleProvider
{
    private IntPtr _handle;

    public IntPtr Handle => _handle;

    public event EventHandler<IntPtr>? HandleChanged;

    public void SetHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == _handle)
        {
            return;
        }

        _handle = handle;
        HandleChanged?.Invoke(this, handle);
    }
}
