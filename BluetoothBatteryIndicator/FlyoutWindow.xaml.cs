using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Animation;
using BluetoothBatteryIndicator.Models;
using BluetoothBatteryIndicator.Services;

namespace BluetoothBatteryIndicator;

public partial class FlyoutWindow : Window
{
    private readonly BluetoothService _bluetoothService;
    private readonly SettingsService _settingsService;
    private readonly ObservableCollection<BluetoothDeviceInfo> _visibleDevices = new();
    private readonly Action? _openSettings;
    private bool _isClosing;

    public FlyoutWindow(BluetoothService bluetoothService, SettingsService settingsService, Action? openSettings = null)
    {
        _bluetoothService = bluetoothService;
        _settingsService = settingsService;
        _openSettings = openSettings;
        InitializeComponent();

        DeviceList.ItemsSource = _visibleDevices;
        _bluetoothService.DevicesChanged += () => Dispatcher.Invoke(UpdateView);
        _settingsService.SettingsChanged += () => Dispatcher.Invoke(UpdateView);
        UpdateView();
    }

    public void ShowAtTray()
    {
        UpdateView();
        PositionNearTray();
        Show();
        Activate();
        _isClosing = false;
        var storyboard = (Storyboard)FindResource("SlideIn");
        storyboard.Begin(this);
    }

    public void HideAnimated()
    {
        if (_isClosing) return;
        _isClosing = true;
        var storyboard = (Storyboard)FindResource("SlideOut");
        storyboard.Begin(this);
    }

    private void SlideOut_Completed(object? sender, EventArgs e)
    {
        Hide();
        _isClosing = false;
    }

    private void PositionNearTray()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 8;
        Top = workArea.Bottom - ActualHeight - 8;

        Dispatcher.InvokeAsync(() =>
        {
            Top = workArea.Bottom - ActualHeight - 8;
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        HideAnimated();
    }

    private void UpdateView()
    {
        _visibleDevices.Clear();
        foreach (var device in _bluetoothService.Devices)
        {
            if (device.IsEnabled && device.IsConnected)
                _visibleDevices.Add(device);
        }

        EmptyState.Visibility = _visibleDevices.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        HideAnimated();
        _openSettings?.Invoke();
    }
}
