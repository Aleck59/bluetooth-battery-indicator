using System.Drawing;
using BluetoothBatteryIndicator.Models;

namespace BluetoothBatteryIndicator.Services;

public class TrayIconManager : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly BluetoothService _bluetoothService;
    private readonly SettingsService _settingsService;
    private readonly Action _showFlyout;
    private readonly Action _showSettings;

    public System.Windows.Forms.NotifyIcon NotifyIcon => _notifyIcon;

    public TrayIconManager(BluetoothService bluetoothService, SettingsService settingsService, Action showFlyout, Action showSettings)
    {
        _bluetoothService = bluetoothService;
        _settingsService = settingsService;
        _showFlyout = showFlyout;
        _showSettings = showSettings;

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = CreateBatteryIcon(-1),
            Text = "Bluetooth Battery Indicator",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _notifyIcon.Click += (s, e) =>
        {
            if (e is System.Windows.Forms.MouseEventArgs me && me.Button == System.Windows.Forms.MouseButtons.Left)
                _showFlyout();
        };

        _bluetoothService.DevicesChanged += OnDevicesChanged;
        _settingsService.SettingsChanged += OnDevicesChanged;
    }

    private void OnDevicesChanged()
    {
        UpdateTooltip();
        UpdateIcon();
    }

    private void UpdateIcon()
    {
        var level = GetDisplayBatteryLevel();
        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = CreateBatteryIcon(level);
        oldIcon?.Dispose();
    }

    private int GetDisplayBatteryLevel()
    {
        var targetId = _settingsService.Settings.TrayIconDeviceId;

        if (targetId != null)
        {
            var device = _bluetoothService.Devices.FirstOrDefault(d => d.Id == targetId && d.IsConnected);
            return device?.BatteryLevel ?? -1;
        }

        // Default: show lowest battery among enabled connected devices
        var enabledConnected = _bluetoothService.Devices
            .Where(d => d.IsConnected && d.IsEnabled && d.BatteryLevel >= 0)
            .ToList();

        return enabledConnected.Count > 0 ? enabledConnected.Min(d => d.BatteryLevel) : -1;
    }

    private void UpdateTooltip()
    {
        var enabledDevices = _bluetoothService.Devices
            .Where(d => _settingsService.IsDeviceEnabled(d.Id) && d.IsConnected)
            .ToList();

        string tooltip;
        if (enabledDevices.Count == 0)
        {
            tooltip = "No Bluetooth devices";
        }
        else
        {
            var lines = enabledDevices.Select(d => $"{d.DisplayName}: {d.BatteryText}");
            tooltip = string.Join("\n", lines);
        }

        if (tooltip.Length > 127)
            tooltip = tooltip[..124] + "...";

        _notifyIcon.Text = tooltip;
    }

    private System.Windows.Forms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => _showSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        return menu;
    }

    private static Icon CreateBatteryIcon(int batteryLevel)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Battery body outline
        g.DrawRectangle(Pens.White, 1, 3, 11, 10);
        // Battery tip (positive terminal)
        g.FillRectangle(Brushes.White, 12, 6, 3, 4);

        // Fill level
        if (batteryLevel >= 0)
        {
            var fillColor = batteryLevel > 50 ? Color.LimeGreen
                          : batteryLevel > 20 ? Color.Yellow
                          : Color.Red;
            var fillWidth = (int)(9 * batteryLevel / 100.0);
            if (fillWidth > 0)
            {
                using var fillBrush = new SolidBrush(fillColor);
                g.FillRectangle(fillBrush, 2, 4, fillWidth, 9);
            }
        }

        // Bluetooth symbol overlay (centered in battery body)
        using var btPen = new Pen(Color.White, 1.2f);
        float cx = 7, cy = 8;
        // Vertical line
        g.DrawLine(btPen, cx, cy - 3.5f, cx, cy + 3.5f);
        // Top arrow
        g.DrawLine(btPen, cx, cy - 3.5f, cx + 2.5f, cy - 1.2f);
        g.DrawLine(btPen, cx + 2.5f, cy - 1.2f, cx - 2f, cy + 2f);
        // Bottom arrow
        g.DrawLine(btPen, cx, cy + 3.5f, cx + 2.5f, cy + 1.2f);
        g.DrawLine(btPen, cx + 2.5f, cy + 1.2f, cx - 2f, cy - 2f);

        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
