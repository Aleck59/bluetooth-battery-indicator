namespace BluetoothBatteryIndicator.Models;

public class AppSettings
{
    public Dictionary<string, bool> DeviceEnabled { get; set; } = new();
    public Dictionary<string, string> DeviceNicknames { get; set; } = new();
    public Dictionary<string, int> DeviceCategories { get; set; } = new();
    public int PollingIntervalSeconds { get; set; } = 60;
    public int LowBatteryThreshold { get; set; } = 20;
    public bool AutoUpdate { get; set; } = true;
    public string? TrayIconDeviceId { get; set; } // null = show lowest battery
    public double WindowWidth { get; set; } = 750;
    public double WindowHeight { get; set; } = 600;
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public bool WindowMaximized { get; set; } = false;
}
