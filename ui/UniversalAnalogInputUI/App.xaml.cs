using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using UniversalAnalogInputUI.Services;
using UniversalAnalogInputUI.Services.Interfaces;
using UniversalAnalogInputUI.Services.Factories;
using DotNetEnv;

namespace UniversalAnalogInputUI;

public partial class App : Application
{
    private const string UiInstanceMutexName = "Local\\UniversalAnalogInput_UI";
    private const uint MbIconWarning = 0x00000030;
    private static Mutex? _singleInstanceMutex;

    public static IServiceProvider Services { get; private set; } = null!;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string lpText, string lpCaption, uint uType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const uint WM_SETICON = 0x0080;
    private const uint ICON_SMALL = 0;
    private const uint ICON_BIG = 1;

    public App()
    {
#if DEBUG
        AllocConsole();
        Console.WriteLine("=== Universal Analog Input Debug Console ===");
        Console.WriteLine("Debug output will appear here...");
#endif

        // Load or ignore .env file
        try
        {
            DotNetEnv.Env.Load();
#if DEBUG
            Console.WriteLine("[DEBUG] .env file loaded successfully");
#endif
        }
        catch
        {
            // .env file not found or invalid - ignore
#if DEBUG
            Console.WriteLine("[DEBUG] .env file not found or invalid - using environment variables");
#endif
        }

        // Uses UI_SENTRY_DSN from .env or environment variables
        SentryService.Initialize();

        EnsureSingleInstance();
        this.InitializeComponent();

        this.UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

#if DEBUG
        Console.WriteLine($"Crash log location: {CrashLogger.GetLogFilePath()}");
        Console.WriteLine();
#endif

        CrashLogger.LogMessage("Application started", "App");

        Services = ConfigureServices();
    }

    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IWindowHandleProvider, WindowHandleProvider>();

        services.AddSingleton<IRustInteropService, RustInteropServiceIpc>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<IStatusMonitorService, StatusMonitorService>();
        services.AddSingleton<IGamepadService, GamepadService>();

        // Services requiring initialization after window creation
        services.AddSingleton<IFilePickerService>(sp =>
        {
            var handleProvider = sp.GetRequiredService<IWindowHandleProvider>();
            return new FilePickerService(handleProvider);
        });

        services.AddSingleton<TaskbarBadgeService>(sp =>
        {
            var handleProvider = sp.GetRequiredService<IWindowHandleProvider>();
            return new TaskbarBadgeService(handleProvider);
        });

        services.AddSingleton<IDialogService>(sp =>
        {
            var rustInterop = sp.GetRequiredService<IRustInteropService>();
            return new DialogService(() => MainWindow?.Content?.XamlRoot, rustInterop);
        });

        services.AddSingleton<IProfileManagementService, ProfileManagementService>();
        services.AddSingleton<IHotkeyManagementService, HotkeyManagementService>();
        services.AddSingleton<IMappingManagementService, MappingManagementService>();

        // Main window
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        CrashLogger.LogException(e.Exception, "WinUI UnhandledException");

        if (SentryService.IsEnabled)
        {
            SentryService.CaptureCriticalError("WinUI UnhandledException", e.Exception);
        }

        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            CrashLogger.LogException(ex, "AppDomain UnhandledException");

            if (SentryService.IsEnabled)
            {
                SentryService.CaptureCriticalError("AppDomain UnhandledException", ex);
            }
        }
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        m_window = Services.GetRequiredService<MainWindow>();
        MainWindow = m_window;

        SetWindowIcon(m_window, "Assets/icon.ico");

        // Delay window activation to allow content to render first
        m_window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            m_window.Activate();
        });
    }

    private void SetWindowIcon(Window window, string iconPath)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = System.IO.Path.GetDirectoryName(exePath);
            var fullIconPath = System.IO.Path.Combine(exeDir!, iconPath);

            if (!System.IO.File.Exists(fullIconPath))
            {
                Console.WriteLine($"[WARNING] Icon file not found: {fullIconPath}");
                return;
            }

            IntPtr hIconSmall = LoadImage(IntPtr.Zero, fullIconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            IntPtr hIconLarge = LoadImage(IntPtr.Zero, fullIconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);

            if (hIconSmall != IntPtr.Zero)
            {
                SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, hIconSmall);
            }

            if (hIconLarge != IntPtr.Zero)
            {
                SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, hIconLarge);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to set window icon: {ex.Message}");
        }
    }

    private Window? m_window;

    public static Window? MainWindow { get; private set; }

    public static new App Current => (App)Application.Current;

    private void EnsureSingleInstance()
    {
        try
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, UiInstanceMutexName, out createdNew);

            if (!createdNew)
            {
                CrashLogger.LogMessage("UI launch blocked - instance already running", "App");
                MessageBoxW(
                    IntPtr.Zero,
                    "Universal Analog Input is already running. Please use the existing window.",
                    "Instance already active",
                    MbIconWarning);

                Environment.Exit(0);
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => ReleaseSingleInstanceMutex();
        }
        catch (Exception ex)
        {
            CrashLogger.LogException(ex, "SingleInstance");
            MessageBoxW(
                IntPtr.Zero,
                "An error occurred while checking the single-instance lock. The application will exit.",
                "System error",
                MbIconWarning);
            Environment.Exit(1);
        }
    }

    private static void ReleaseSingleInstanceMutex()
    {
        if (_singleInstanceMutex is null)
        {
            return;
        }

        try
        {
            _singleInstanceMutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        finally
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
    }
}
