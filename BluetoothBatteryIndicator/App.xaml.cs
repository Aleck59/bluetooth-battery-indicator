using System.Runtime.InteropServices;
using System.Windows;
using BluetoothBatteryIndicator.Services;
using Wpf.Ui.Appearance;

namespace BluetoothBatteryIndicator;

public partial class App : Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    private const uint WM_SHOWSETTINGS = 0x0400 + 1; // WM_USER + 1

    private TrayIconManager? _trayManager;
    private BluetoothService? _bluetoothService;
    private SettingsService? _settingsService;
    private NotificationService? _notificationService;
    private BatteryHistoryService? _historyService;
    private MainWindow? _mainWindow;
    private FlyoutWindow? _flyoutWindow;
    private System.Threading.Mutex? _mutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        AttachConsole(-1); // Attach to parent console if launched from terminal

        _mutex = new System.Threading.Mutex(true, "BluetoothBatteryIndicator_SingleInstance", out bool isNew);
        if (!isNew)
        {
            Log.Info("Another instance is already running. Signaling it to show settings.");
            var hwnd = FindWindow(null, "Bluetooth Battery Indicator");
            if (hwnd != IntPtr.Zero)
                PostMessage(hwnd, WM_SHOWSETTINGS, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        Log.Info("Starting Bluetooth Battery Indicator");

        ApplicationThemeManager.ApplySystemTheme();
        Log.Info($"Applied system theme");

        // Check for updates before initializing
        var tempSettings = new SettingsService();
        tempSettings.Load();
        if (tempSettings.Settings.AutoUpdate)
        {
            Log.Info("Auto-update enabled, checking for updates...");
            var updateWindow = new UpdateWindow();
            var updateService = new UpdateService(updateWindow);
            if (await updateService.CheckAndUpdate())
                return; // App is shutting down for update
            Log.Info("No update needed, continuing startup");
        }
        else
        {
            Log.Info("Auto-update disabled, skipping update check");
        }

        _settingsService = new SettingsService();
        _settingsService.Load();

        _bluetoothService = new BluetoothService();
        _bluetoothService.DevicesChanged += SyncDeviceSettings;
        _settingsService.SettingsChanged += SyncDeviceSettings;
        _trayManager = new TrayIconManager(_bluetoothService, _settingsService, ShowFlyout, ShowMainWindow);
        _notificationService = new NotificationService(_bluetoothService, _settingsService, _trayManager.NotifyIcon);
        _historyService = new BatteryHistoryService(_bluetoothService);
        _bluetoothService.StartAsync();
        _bluetoothService.SetPollingInterval(_settingsService.Settings.PollingIntervalSeconds);

        // Hidden message-only window to receive signals from second instances
        var msgSource = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Bluetooth Battery Indicator")
        {
            Width = 0,
            Height = 0,
            PositionX = -100,
            PositionY = -100,
            WindowStyle = 0x800000, // WS_BORDER to make it findable but invisible
        });
        msgSource.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg == WM_SHOWSETTINGS)
            {
                ShowMainWindow();
                handled = true;
            }
            return IntPtr.Zero;
        });
    }

    private void SyncDeviceSettings()
    {
        foreach (var device in _bluetoothService!.Devices)
        {
            device.IsEnabled = _settingsService!.IsDeviceEnabled(device.Id);
            device.Nickname = _settingsService.GetNickname(device.Id);
            var catOverride = _settingsService.GetCategoryOverride(device.Id);
            if (catOverride.HasValue)
                device.Category = catOverride.Value;
        }
    }

    private void ShowFlyout()
    {
        if (_flyoutWindow == null || !_flyoutWindow.IsLoaded)
        {
            _flyoutWindow = new FlyoutWindow(_bluetoothService!, _settingsService!, ShowMainWindow);
        }

        if (_flyoutWindow.IsVisible)
            _flyoutWindow.HideAnimated();
        else
            _flyoutWindow.ShowAtTray();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
        {
            _mainWindow = new MainWindow(_bluetoothService!, _settingsService!, _historyService!);
        }
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayManager?.Dispose();
        _bluetoothService?.Dispose();
        if (_settingsService != null) // only release if we're the owning instance
        {
            _mutex?.ReleaseMutex();
        }
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
