using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BluetoothBatteryIndicator.Services;

public class TrayIconManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // Battery glyphs shared by Segoe MDL2 Assets (Windows 10) and Segoe Fluent Icons (Windows 11).
    // Battery0..Battery9 are U+E850..U+E859, Battery10 is U+E83F.
    private const char BatteryEmptyGlyph = '';
    private const char BatteryFullGlyph = '';
    private const char BatteryUnknownGlyph = '';

    private static readonly Color LowBatteryColor = Color.FromArgb(232, 17, 35);

    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly BluetoothService _bluetoothService;
    private readonly SettingsService _settingsService;
    private readonly Action _showFlyout;
    private readonly Action _showSettings;
    private readonly System.Windows.Threading.Dispatcher _dispatcher;
    private readonly FontFamily? _iconFont;
    private string? _iconKey;

    public System.Windows.Forms.NotifyIcon NotifyIcon => _notifyIcon;

    public TrayIconManager(BluetoothService bluetoothService, SettingsService settingsService, Action showFlyout, Action showSettings)
    {
        _bluetoothService = bluetoothService;
        _settingsService = settingsService;
        _showFlyout = showFlyout;
        _showSettings = showSettings;
        _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _iconFont = ResolveIconFont();

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
            _dispatcher.BeginInvoke(UpdateIcon);
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
        var key = $"{GlyphFor(level)}|{size}|{lightTaskbar}|{low}";
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
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
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

    private static FontFamily? ResolveIconFont()
    {
        // Windows 11 draws the system battery with Segoe Fluent Icons, Windows 10 with Segoe MDL2 Assets
        var candidates = Environment.OSVersion.Version.Build >= 22000
            ? new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" }
            : new[] { "Segoe MDL2 Assets", "Segoe Fluent Icons" };

        foreach (var name in candidates)
        {
            try
            {
                var family = new FontFamily(name);
                Log.Info($"Tray icon font: {name}");
                return family;
            }
            catch (ArgumentException) { }
        }

        Log.Info("No Segoe icon font found, using drawn tray icon");
        return null;
    }

    private static char GlyphFor(int batteryLevel)
    {
        if (batteryLevel < 0) return BatteryUnknownGlyph;
        var step = (int)Math.Round(Math.Min(Math.Max(batteryLevel, 0), 100) / 10.0);
        return step == 10 ? BatteryFullGlyph : (char)(BatteryEmptyGlyph + step);
    }

    private Icon CreateBatteryIcon(int batteryLevel, int size, bool lightTaskbar, bool low)
    {
        var foreground = lightTaskbar ? Color.Black : Color.White;

        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            if (_iconFont == null || !DrawGlyphBattery(g, _iconFont, batteryLevel, size, foreground, low))
                DrawFallbackBattery(g, batteryLevel, size, foreground, low);
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

    private static bool DrawGlyphBattery(Graphics g, FontFamily font, int batteryLevel, int size, Color foreground, bool low)
    {
        using var levelPath = GlyphPath(font, GlyphFor(batteryLevel), size);
        var bounds = levelPath.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        // Center on whole pixels so the glyph keeps the pixel grid it was designed on
        var dx = (float)Math.Floor((size - bounds.Width) / 2) - bounds.X;
        var dy = (float)Math.Floor((size - bounds.Height) / 2) - bounds.Y;
        using var shift = new Matrix();
        shift.Translate(dx, dy);
        levelPath.Transform(shift);

        if (!low)
        {
            using var brush = new SolidBrush(foreground);
            g.FillPath(brush, levelPath);
            return true;
        }

        // Low battery: red charge level under the regular outline, like the system indicator
        using var outlinePath = GlyphPath(font, BatteryEmptyGlyph, size);
        outlinePath.Transform(shift);
        using (var lowBrush = new SolidBrush(LowBatteryColor))
            g.FillPath(lowBrush, levelPath);
        using (var brush = new SolidBrush(foreground))
            g.FillPath(brush, outlinePath);
        return true;
    }

    private static GraphicsPath GlyphPath(FontFamily font, char glyph, int size)
    {
        var path = new GraphicsPath();
        path.AddString(glyph.ToString(), font, (int)FontStyle.Regular, size, PointF.Empty, StringFormat.GenericTypographic);
        return path;
    }

    /// <summary>Hand-drawn Windows 10 style battery for systems without the Segoe icon fonts.</summary>
    private static void DrawFallbackBattery(Graphics g, int batteryLevel, int size, Color foreground, bool low)
    {
        g.SmoothingMode = SmoothingMode.None;
        float unit = size / 16f;
        using var pen = new Pen(foreground, Math.Max(1, (int)unit));
        using var brush = new SolidBrush(foreground);

        var body = new RectangleF(1 * unit, 4 * unit, 12 * unit, 8 * unit);
        g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
        g.FillRectangle(brush, 13 * unit, 6 * unit, 2 * unit, 4 * unit);

        if (batteryLevel > 0)
        {
            using var fill = new SolidBrush(low ? LowBatteryColor : foreground);
            var inner = 9 * unit;
            g.FillRectangle(fill, 3 * unit, 6 * unit, inner * Math.Min(batteryLevel, 100) / 100f, 4 * unit + 1);
        }
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
        _iconFont?.Dispose();
    }
}
