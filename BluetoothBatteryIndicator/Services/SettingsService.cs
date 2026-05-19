using System.IO;
using System.Text.Json;
using BluetoothBatteryIndicator.Models;

namespace BluetoothBatteryIndicator.Services;

public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BluetoothBatteryIndicator");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { Settings = new AppSettings(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
        }
        catch { }
    }

    public bool IsDeviceEnabled(string deviceId)
    {
        return !Settings.DeviceEnabled.TryGetValue(deviceId, out var enabled) || enabled;
    }

    public string GetNickname(string deviceId)
    {
        return Settings.DeviceNicknames.TryGetValue(deviceId, out var nick) ? nick : "";
    }

    public void SetNickname(string deviceId, string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            Settings.DeviceNicknames.Remove(deviceId);
        else
            Settings.DeviceNicknames[deviceId] = nickname;
        Save();
        SettingsChanged?.Invoke();
    }

    public DeviceCategory? GetCategoryOverride(string deviceId)
    {
        return Settings.DeviceCategories.TryGetValue(deviceId, out var cat) ? (DeviceCategory)cat : null;
    }

    public void SetCategoryOverride(string deviceId, DeviceCategory category)
    {
        Settings.DeviceCategories[deviceId] = (int)category;
        Save();
        SettingsChanged?.Invoke();
    }

    public void SetDeviceEnabled(string deviceId, bool enabled)
    {
        Settings.DeviceEnabled[deviceId] = enabled;
        Save();
        SettingsChanged?.Invoke();
    }

    public event Action? SettingsChanged;

    public void NotifyChanged() => SettingsChanged?.Invoke();
}
