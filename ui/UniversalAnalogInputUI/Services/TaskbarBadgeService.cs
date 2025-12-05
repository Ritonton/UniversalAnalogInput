using System;
using System.IO;
using System.Runtime.InteropServices;
using UniversalAnalogInputUI.Services;

namespace UniversalAnalogInputUI.Services;

/// <summary>Manages taskbar overlay badges for the unpackaged app using the ITaskbarList3 COM interface.</summary>
public class TaskbarBadgeService : IDisposable
{
    // COM interface for taskbar overlay icons (works for unpackaged apps)
    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
        void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);
        void SetProgressState(IntPtr hwnd, int tbpFlags);
        void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
        void UnregisterTab(IntPtr hwndTab);
        void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
        void SetTabActive(IntPtr hwndTab, IntPtr hwndInsertBefore, uint dwReserved);
        void ThumbBarAddButtons(IntPtr hwnd, uint cButtons, IntPtr pButtons);
        void ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, IntPtr pButtons);
        void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
        void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string pszDescription);
        void SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string pszTip);
        void SetThumbnailClip(IntPtr hwnd, IntPtr prcClip);
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarInstance { }

    [DllImport("user32.dll")]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    private const int GWL_WNDPROC = -4;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly Services.Factories.IWindowHandleProvider _windowHandleProvider;
    private ITaskbarList3? _taskbarList;
    private uint _taskbarButtonCreatedMessage;
    private bool _taskbarButtonReady = false;
    private bool? _pendingKeyboardStatus = null;
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _oldWndProc;

    /// <summary>Raised when the taskbar button is ready to receive overlay updates.</summary>
    public event EventHandler? TaskbarButtonReady;

    public TaskbarBadgeService(Services.Factories.IWindowHandleProvider windowHandleProvider)
    {
        _windowHandleProvider = windowHandleProvider;
        _windowHandleProvider.HandleChanged += OnHandleChanged;
        InitializeIfPossible();
    }

    private void OnHandleChanged(object? sender, IntPtr handle)
    {
        InitializeIfPossible();
    }

    /// <summary>Initializes COM bindings and hooks the window procedure to watch for taskbar readiness.</summary>
    private void InitializeIfPossible()
    {
        if (_windowHandleProvider.Handle == IntPtr.Zero || _taskbarList != null)
        {
            return;
        }

        try
        {
            SetCurrentProcessExplicitAppUserModelID("UniversalAnalogInput.App");

            _taskbarButtonCreatedMessage = RegisterWindowMessage("TaskbarButtonCreated");
            _taskbarList = (ITaskbarList3)new TaskbarInstance();
            _taskbarList.HrInit();

            _wndProcDelegate = new WndProcDelegate(WndProc);
            _oldWndProc = SetWindowLongPtr(_windowHandleProvider.Handle, GWL_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
        }
        catch (Exception ex)
        {
            CrashLogger.LogMessage($"[TaskbarBadgeService] Initialization failed: {ex.Message}", "TaskbarBadgeService");
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _taskbarButtonCreatedMessage)
        {
            _taskbarButtonReady = true;
            TaskbarButtonReady?.Invoke(this, EventArgs.Empty);

            if (_pendingKeyboardStatus.HasValue)
            {
                SetBadge(_pendingKeyboardStatus.Value);
                _pendingKeyboardStatus = null;
            }
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    /// <summary>Sets or clears the taskbar overlay badge based on keyboard connection status.</summary>
    /// <param name="keyboardConnected">True to clear badge, false to show warning badge</param>
    public void SetBadge(bool keyboardConnected)
    {
        if (_taskbarList == null)
        {
            return;
        }

        if (!_taskbarButtonReady)
        {
            _pendingKeyboardStatus = keyboardConnected;
            return;
        }

        try
        {
            if (keyboardConnected)
            {
                _taskbarList.SetOverlayIcon(_windowHandleProvider.Handle, IntPtr.Zero, string.Empty);
            }
            else
            {
                IntPtr hIcon = LoadWarningIcon();
                if (hIcon != IntPtr.Zero && hIcon != new IntPtr(1))
                {
                    _taskbarList.SetOverlayIcon(_windowHandleProvider.Handle, hIcon, "No keyboard detected");
                    DestroyIcon(hIcon); // Taskbar makes its own copy
                }
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogMessage($"[TaskbarBadgeService] SetBadge failed: {ex.Message}", "TaskbarBadgeService");
        }
    }

    /// <summary>Loads the system warning icon used when no keyboard is detected.</summary>
    private IntPtr LoadWarningIcon()
    {
        string imageres = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "imageres.dll");
        return ExtractIcon(IntPtr.Zero, imageres, 93);
    }

    /// <summary>Restores the original window procedure and releases COM references.</summary>
    public void Dispose()
    {
        _windowHandleProvider.HandleChanged -= OnHandleChanged;

        if (_oldWndProc != IntPtr.Zero && _windowHandleProvider.Handle != IntPtr.Zero)
        {
            SetWindowLongPtr(_windowHandleProvider.Handle, GWL_WNDPROC, _oldWndProc);
        }

        _taskbarList = null;
        _wndProcDelegate = null;
    }
}
