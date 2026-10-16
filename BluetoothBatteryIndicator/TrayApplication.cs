using BluetoothBatteryIndicator.Services;
using BluetoothBatteryIndicator.Ui;

namespace BluetoothBatteryIndicator;

/// <summary>Tray-only part of the app: Bluetooth polling, tray icon, notifications, history.</summary>
public sealed class TrayApplication : System.Windows.Forms.ApplicationContext
{
    // Off the startup path, so it does not count towards Windows' startup impact
    private static readonly TimeSpan UpdateCheckDelay = TimeSpan.FromSeconds(60);

    private readonly SettingsService _settingsService;
    private readonly BluetoothService _bluetoothService;
    private readonly TrayIconManager _trayManager;
    private readonly BatteryHistoryService _historyService;
    private readonly InstanceSignalWindow _signalWindow;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private IUiHost? _ui;

    public TrayApplication()
    {
        _settingsService = new SettingsService();
        _settingsService.Load();

        _bluetoothService = new BluetoothService();
        _bluetoothService.DevicesChanged += SyncDeviceSettings;
        _settingsService.SettingsChanged += SyncDeviceSettings;
        _trayManager = new TrayIconManager(_bluetoothService, _settingsService, ToggleFlyout, ShowSettings, ExitThread);
        _ = new NotificationService(_bluetoothService, _settingsService, _trayManager.NotifyIcon);
        _historyService = new BatteryHistoryService(_bluetoothService);
        _bluetoothService.StartAsync();
        _bluetoothService.SetPollingInterval(_settingsService.Settings.PollingIntervalSeconds);

        _signalWindow = new InstanceSignalWindow(Program.SignalWindowTitle, ShowSettings);

        _updateTimer = new System.Windows.Forms.Timer { Interval = (int)UpdateCheckDelay.TotalMilliseconds };
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Stop();
            await CheckForUpdatesAsync();
        };
        _updateTimer.Start();
    }

    private IUiHost Ui => _ui ??= UiHostFactory.Create(_bluetoothService, _settingsService, _historyService);

    private void ShowSettings() => Ui.ShowSettings();

    private void ToggleFlyout() => Ui.ToggleFlyout();

    private async Task CheckForUpdatesAsync()
    {
        if (!_settingsService.Settings.AutoUpdate)
        {
            Log.Info("Auto-update disabled, skipping update check");
            MemoryTrim.Trim();
            return;
        }

        var updateService = new UpdateService();
        var update = await updateService.CheckAsync();
        if (update == null)
        {
            // Startup work (JIT, device enumeration, HTTP) is done: release what it left behind
            MemoryTrim.Trim();
            return;
        }

        if (await updateService.DownloadAndRunAsync(update, Ui.CreateUpdateProgress()))
            ExitThread();
    }

    private void SyncDeviceSettings()
    {
        foreach (var device in _bluetoothService.Devices)
        {
            device.IsEnabled = _settingsService.IsDeviceEnabled(device.Id);
            device.Nickname = _settingsService.GetNickname(device.Id);
            var catOverride = _settingsService.GetCategoryOverride(device.Id);
            if (catOverride.HasValue)
                device.Category = catOverride.Value;
        }
    }

    protected override void ExitThreadCore()
    {
        _updateTimer.Dispose();
        _signalWindow.Dispose();
        _trayManager.Dispose();
        _bluetoothService.Dispose();
        base.ExitThreadCore();
    }
}
