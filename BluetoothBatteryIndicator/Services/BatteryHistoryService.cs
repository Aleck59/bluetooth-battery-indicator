using System.IO;
using System.Text.Json;
using BluetoothBatteryIndicator.Models;

namespace BluetoothBatteryIndicator.Services;

public class BatteryHistoryService
{
    private static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BluetoothBatteryIndicator", "history");

    private readonly Dictionary<string, List<BatteryReading>> _cache = new();
    private readonly Dictionary<string, DateTime> _lastRecordTime = new();
    private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);

    private readonly BluetoothService _bluetoothService;

    public BatteryHistoryService(BluetoothService bluetoothService)
    {
        _bluetoothService = bluetoothService;
        _bluetoothService.DevicesChanged += OnDevicesChanged;
        Directory.CreateDirectory(HistoryDir);
    }

    private void OnDevicesChanged()
    {
        foreach (var device in _bluetoothService.Devices)
        {
            if (device.BatteryLevel < 0 || !device.IsConnected) continue;
            Record(device.Id, device.BatteryLevel);
        }
    }

    private void Record(string deviceId, int level)
    {
        var now = DateTime.UtcNow;

        if (_lastRecordTime.TryGetValue(deviceId, out var lastTime))
        {
            var history = GetHistory(deviceId);
            var lastLevel = history.Count > 0 ? history[history.Count - 1].Level : -1;

            // Only record if level changed or enough time passed
            if (lastLevel == level && now - lastTime < MinInterval)
                return;
        }

        var readings = GetHistory(deviceId);
        readings.Add(new BatteryReading { Timestamp = now, Level = level });
        _lastRecordTime[deviceId] = now;

        // Prune old entries
        var cutoff = now - RetentionPeriod;
        readings.RemoveAll(r => r.Timestamp < cutoff);

        Save(deviceId, readings);
    }

    public List<BatteryReading> GetHistory(string deviceId)
    {
        if (_cache.TryGetValue(deviceId, out var cached))
            return cached;

        var file = GetFilePath(deviceId);
        List<BatteryReading> readings;

        if (File.Exists(file))
        {
            try
            {
                var json = File.ReadAllText(file);
                readings = JsonSerializer.Deserialize<List<BatteryReading>>(json) ?? new();
            }
            catch { readings = new(); }
        }
        else
        {
            readings = new();
        }

        _cache[deviceId] = readings;
        return readings;
    }

    private void Save(string deviceId, List<BatteryReading> readings)
    {
        _cache[deviceId] = readings;
        try
        {
            var json = JsonSerializer.Serialize(readings);
            File.WriteAllText(GetFilePath(deviceId), json);
        }
        catch { }
    }

    private static string GetFilePath(string deviceId)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var digest = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(deviceId));
        var hash = BitConverter.ToString(digest).Replace("-", "").Substring(0, 16);
        return Path.Combine(HistoryDir, $"{hash}.json");
    }
}
