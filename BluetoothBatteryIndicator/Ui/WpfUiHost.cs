using System.Windows;
using BluetoothBatteryIndicator.Services;
using Wpf.Ui.Appearance;

namespace BluetoothBatteryIndicator.Ui;

/// <summary>Creates the WPF application object and windows on first use.</summary>
internal sealed class WpfUiHost : IUiHost
{
    private static readonly TimeSpan TrimDelay = TimeSpan.FromSeconds(2);

    private readonly BluetoothService _bluetoothService;
    private readonly SettingsService _settingsService;
    private readonly BatteryHistoryService _historyService;
    private readonly System.Windows.Threading.DispatcherTimer _trimTimer;
    private MainWindow? _mainWindow;
    private FlyoutWindow? _flyoutWindow;

    public WpfUiHost(BluetoothService bluetoothService, SettingsService settingsService, BatteryHistoryService historyService)
    {
        _bluetoothService = bluetoothService;
        _settingsService = settingsService;
        _historyService = historyService;

        // The App object carries the WPF-UI resource dictionaries; Run() is never called because
        // the WinForms loop already pumps messages for this thread.
        if (Application.Current == null)
            new App().InitializeComponent();

        ApplicationThemeManager.ApplySystemTheme();
        Log.Info("WPF UI initialized");

        _trimTimer = new System.Windows.Threading.DispatcherTimer { Interval = TrimDelay };
        _trimTimer.Tick += (_, _) =>
        {
            _trimTimer.Stop();
            if (!AnyWindowVisible())
                MemoryTrim.Trim();
        };
    }

    public void ShowSettings()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
            _mainWindow = Track(new MainWindow(_bluetoothService, _settingsService, _historyService));

        _mainWindow.Show();
        _mainWindow.Activate();
    }

    public void ToggleFlyout()
    {
        if (_flyoutWindow == null || !_flyoutWindow.IsLoaded)
            _flyoutWindow = Track(new FlyoutWindow(_bluetoothService, _settingsService, ShowSettings));

        if (_flyoutWindow.IsVisible)
            _flyoutWindow.HideAnimated();
        else
            _flyoutWindow.ShowAtTray();
    }

    public IUpdateProgress CreateUpdateProgress() => Track(new UpdateWindow());

    private T Track<T>(T window) where T : Window
    {
        // Keyboard input (Tab, accelerators) for WPF windows running on a WinForms message loop
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(window);
        window.IsVisibleChanged += (_, _) =>
        {
            if (!window.IsVisible)
            {
                _trimTimer.Stop();
                _trimTimer.Start();
            }
        };
        return window;
    }

    private bool AnyWindowVisible() =>
        Application.Current.Windows.Cast<Window>().Any(w => w.IsVisible);
}
