using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BluetoothBatteryIndicator.Services;

public class TrayIconManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private static readonly Color LowBatteryColor = Color.FromArgb(232, 17, 35);

    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly BluetoothService _bluetoothService;
    private readonly SettingsService _settingsService;
    private readonly Action _showFlyout;
    private readonly Action _showSettings;
    private readonly Action _exit;
    private readonly SynchronizationContext _uiContext;
    private string? _iconKey;

    public System.Windows.Forms.NotifyIcon NotifyIcon => _notifyIcon;

    public TrayIconManager(BluetoothService bluetoothService, SettingsService settingsService, Action showFlyout, Action showSettings, Action exit)
    {
        _bluetoothService = bluetoothService;
        _settingsService = settingsService;
        _showFlyout = showFlyout;
        _showSettings = showSettings;
        _exit = exit;
        _uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("TrayIconManager must be created on the UI thread");

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Bluetooth Battery Indicator",
            ContextMenuStrip = CreateContextMenu()
        };

        _notifyIcon.Click += (s, e) =>
        {
            if (e is System.Windows.Forms.MouseEventArgs me && me.Button == System.Windows.Forms.MouseButtons.Left)
                _showFlyout();
        };

        _bluetoothService.DevicesChanged += OnDevicesChanged;
        _settingsService.SettingsChanged += OnDevicesChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        OnDevicesChanged();
    }

    private void OnDevicesChanged()
    {
        UpdateTooltip();
        UpdateIcon();
        UpdateVisibility();
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Taskbar light/dark switch arrives as General
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle)
            _uiContext.Post(_ => UpdateIcon(), null);
    }

    /// <summary>
    /// Shows the icon only while the tracked device (or any enabled device in auto mode) is connected,
    /// unless the user turned auto-hide off. Settings stay reachable by launching the app again.
    /// </summary>
    private void UpdateVisibility()
    {
        _notifyIcon.Visible = !_settingsService.Settings.HideTrayIconWhenDisconnected || HasTrackedConnectedDevice();
    }

    private bool HasTrackedConnectedDevice()
    {
        var targetId = _settingsService.Settings.TrayIconDeviceId;
        if (targetId != null)
            return _bluetoothService.Devices.Any(d => d.Id == targetId && d.IsConnected);

        return _bluetoothService.Devices.Any(d => d.IsConnected && _settingsService.IsDeviceEnabled(d.Id));
    }

    private void UpdateIcon()
    {
        var level = GetDisplayBatteryLevel();
        var size = System.Windows.Forms.SystemInformation.SmallIconSize.Width;
        var lightTaskbar = IsTaskbarLight();
        var low = level >= 0 && level <= _settingsService.Settings.LowBatteryThreshold;

        // Regenerate only when something visible changed
        var key = $"{FillStep(level)}|{size}|{lightTaskbar}|{low}";
        if (key == _iconKey) return;
        _iconKey = key;

        var oldIcon = _notifyIcon.Icon;
        _notifyIcon.Icon = CreateBatteryIcon(level, size, lightTaskbar, low);
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
            .Where(d => d.IsConnected && _settingsService.IsDeviceEnabled(d.Id) && d.BatteryLevel >= 0)
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
            tooltip = tooltip.Substring(0, 124) + "...";

        _notifyIcon.Text = tooltip;
    }

    private System.Windows.Forms.ContextMenuStrip CreateContextMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => _showSettings());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => _exit());
        return menu;
    }

    private static bool IsTaskbarLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
        }
        catch { return false; }
    }

    private static bool IsWindows11 => Environment.OSVersion.Version.Build >= 22000;

    /// <summary>Charge quantized to whole percent; -1 means unknown (empty outline).</summary>
    private static int FillStep(int batteryLevel) =>
        batteryLevel < 0 ? -1 : Math.Min(Math.Max(batteryLevel, 0), 100);

    private static Icon CreateBatteryIcon(int batteryLevel, int size, bool lightTaskbar, bool low)
    {
        var foreground = lightTaskbar ? Color.Black : Color.White;

        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            DrawBattery(g, FillStep(batteryLevel), size, foreground, low ? LowBatteryColor : foreground, IsWindows11);
        }

        // GetHicon allocates an unmanaged handle that Icon.FromHandle does not own: copy it, then free it
        var hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>
    /// Draws a battery with the proportions of the Windows taskbar battery icon
    /// (at 125% scaling: 20 px icon, 18x13 body, 1x3 terminal), snapped to whole pixels.
    /// Windows 11 gets rounded corners.
    /// </summary>
    private static void DrawBattery(Graphics g, int level, int size, Color outline, Color fill, bool rounded)
    {
        var k = size / 16f;
        var stroke = Math.Max(1, (int)k);
        var bodyW = (int)Math.Round(14.4 * k);
        var bodyH = (int)Math.Round(10.4 * k);
        var nubW = stroke;
        var nubH = (int)Math.Round(bodyH * 0.25);
        if ((bodyH - nubH) % 2 != 0) nubH++; // keep the terminal vertically centered on whole pixels

        var x = (size - bodyW - nubW) / 2;
        var y = (size - bodyH) / 2;

        var inset = 2 * stroke; // outline + equal gap before the charge bar
        var innerW = bodyW - 2 * inset;
        var innerH = bodyH - 2 * inset;
        var fillW = level <= 0 ? 0 : Math.Max(1, (int)Math.Round(innerW * level / 100.0));

        using var outlineBrush = new SolidBrush(outline);
        using var fillBrush = new SolidBrush(fill);

        if (!rounded)
        {
            g.SmoothingMode = SmoothingMode.None;
            g.FillRectangle(outlineBrush, x, y, bodyW, stroke);
            g.FillRectangle(outlineBrush, x, y + bodyH - stroke, bodyW, stroke);
            g.FillRectangle(outlineBrush, x, y, stroke, bodyH);
            g.FillRectangle(outlineBrush, x + bodyW - stroke, y, stroke, bodyH);
            g.FillRectangle(outlineBrush, x + bodyW, y + (bodyH - nubH) / 2, nubW, nubH);
            if (fillW > 0)
                g.FillRectangle(fillBrush, x + inset, y + inset, fillW, innerH);
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        var radius = Math.Max(1.5f, 2 * k);
        using (var pen = new Pen(outline, stroke))
        using (var body = RoundedRect(x + stroke / 2f, y + stroke / 2f, bodyW - stroke, bodyH - stroke, radius))
            g.DrawPath(pen, body);
        using (var nub = RoundedRect(x + bodyW - stroke / 2f, y + (bodyH - nubH) / 2f, nubW + stroke / 2f, nubH, nubW))
            g.FillPath(outlineBrush, nub);
        if (fillW > 0)
        {
            using var bar = RoundedRect(x + inset, y + inset, fillW, innerH, Math.Max(0.5f, radius - inset / 2f));
            g.FillPath(fillBrush, bar);
        }
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var d = Math.Min(2 * r, Math.Min(w, h));
        var path = new GraphicsPath();
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
    }
}
