using BluetoothBatteryIndicator.Models;

namespace BluetoothBatteryIndicator.Services;

public class NotificationService
{
    private readonly BluetoothService _bluetoothService;
    private readonly SettingsService _settingsService;
    private readonly HashSet<string> _notifiedDevices = new();
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;

    public NotificationService(BluetoothService bluetoothService, SettingsService settingsService, System.Windows.Forms.NotifyIcon notifyIcon)
    {
        _bluetoothService = bluetoothService;
        _settingsService = settingsService;
        _notifyIcon = notifyIcon;
        _bluetoothService.DevicesChanged += CheckBatteryLevels;
    }

    private void CheckBatteryLevels()
    {
        var threshold = _settingsService.Settings.LowBatteryThreshold;

        foreach (var device in _bluetoothService.Devices)
        {
            if (!device.IsConnected || device.BatteryLevel < 0) continue;

            if (device.BatteryLevel <= threshold)
            {
                if (_notifiedDevices.Add(device.Id))
                {
                    _notifyIcon.ShowBalloonTip(
                        5000,
                        "Low Battery",
                        $"{device.Name} is at {device.BatteryLevel}%",
                        System.Windows.Forms.ToolTipIcon.Warning);
                }
            }
            else
            {
                // Reset once battery goes above threshold
                _notifiedDevices.Remove(device.Id);
            }
        }
    }
}
