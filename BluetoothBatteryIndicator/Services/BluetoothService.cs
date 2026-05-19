using System.Collections.ObjectModel;
using System.Windows.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using BluetoothBatteryIndicator.Models;

namespace BluetoothBatteryIndicator.Services;

public class BluetoothService : IDisposable
{
    private static readonly string[] RequestedProperties = new[]
    {
        "System.Devices.Aep.DeviceAddress",
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.IsPaired",
        "System.Devices.Aep.Bluetooth.Le.IsConnectable",
        "System.Devices.Aep.Bluetooth.Cod.Major",
        "System.Devices.Aep.Bluetooth.Cod.Minor",
        "System.Devices.Aep.Bluetooth.Le.Appearance",
    };

    private DeviceWatcher? _classicWatcher;
    private DeviceWatcher? _bleWatcher;
    private DispatcherTimer? _pollTimer;
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<BluetoothDeviceInfo> Devices { get; } = new();
    public event Action? DevicesChanged;

    public BluetoothService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void StartAsync()
    {
        Log.Info("Starting Bluetooth device watchers");
        StartWatcher(
            BluetoothDevice.GetDeviceSelectorFromPairingState(true),
            "Classic",
            ref _classicWatcher);

        StartWatcher(
            BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
            "BLE",
            ref _bleWatcher);

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _pollTimer.Tick += (_, _) => RefreshBatteryLevels();
        _pollTimer.Start();

        // Initial poll after a short delay to let watchers populate
        Task.Delay(2000).ContinueWith(_ => _dispatcher.Invoke(RefreshBatteryLevels));
    }

    private void StartWatcher(string selector, string deviceType, ref DeviceWatcher? watcher)
    {
        watcher = DeviceInformation.CreateWatcher(
            selector,
            RequestedProperties,
            DeviceInformationKind.AssociationEndpoint);

        watcher.Added += (_, info) => _dispatcher.Invoke(() => OnDeviceAdded(info, deviceType));
        watcher.Updated += (_, update) => _dispatcher.Invoke(() => OnDeviceUpdated(update));
        watcher.Removed += (_, update) => _dispatcher.Invoke(() => OnDeviceRemoved(update));
        watcher.Start();
    }

    private void OnDeviceAdded(DeviceInformation info, string deviceType)
    {
        if (string.IsNullOrWhiteSpace(info.Name)) return;
        if (Devices.Any(d => d.Id == info.Id)) return;

        var device = new BluetoothDeviceInfo
        {
            Id = info.Id,
            Name = info.Name,
            DeviceType = deviceType,
            Address = GetProperty<string>(info.Properties, "System.Devices.Aep.DeviceAddress") ?? "",
            IsConnected = GetProperty<bool>(info.Properties, "System.Devices.Aep.IsConnected"),
            Category = ClassifyDevice(info.Properties, deviceType),
        };

        Log.Info($"Device added: {device.Name} ({device.DeviceType}, {device.Category}, connected={device.IsConnected})");
        Devices.Add(device);
        DevicesChanged?.Invoke();
    }

    private void OnDeviceUpdated(DeviceInformationUpdate update)
    {
        var device = Devices.FirstOrDefault(d => d.Id == update.Id);
        if (device == null) return;

        if (update.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var conn) && conn is bool connected)
            device.IsConnected = connected;

        DevicesChanged?.Invoke();
    }

    private void OnDeviceRemoved(DeviceInformationUpdate update)
    {
        var device = Devices.FirstOrDefault(d => d.Id == update.Id);
        if (device != null)
        {
            Log.Info($"Device removed: {device.Name}");
            Devices.Remove(device);
            DevicesChanged?.Invoke();
        }
    }

    public void RefreshBatteryLevels()
    {
        Log.Info($"Refreshing battery levels for {Devices.Count} devices");
        foreach (var device in Devices.ToList())
        {
            _ = ReadBatteryAsync(device);
        }
    }

    private async Task ReadBatteryAsync(BluetoothDeviceInfo device)
    {
        try
        {
            if (device.DeviceType == "BLE")
            {
                using var bleDevice = await BluetoothLEDevice.FromIdAsync(device.Id);
                if (bleDevice == null) return;

                var gattResult = await bleDevice.GetGattServicesForUuidAsync(
                    GattServiceUuids.Battery);

                if (gattResult.Status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success
                    && gattResult.Services.Count > 0)
                {
                    var service = gattResult.Services[0];
                    var chars = await service.GetCharacteristicsAsync();
                    if (chars.Status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success
                        && chars.Characteristics.Count > 0)
                    {
                        var readResult = await chars.Characteristics[0].ReadValueAsync();
                        if (readResult.Status == Windows.Devices.Bluetooth.GenericAttributeProfile.GattCommunicationStatus.Success)
                        {
                            var reader = Windows.Storage.Streams.DataReader.FromBuffer(readResult.Value);
                            device.BatteryLevel = reader.ReadByte();
                            DevicesChanged?.Invoke();
                            return;
                        }
                    }
                }
            }

            // Fallback: try reading from device properties (works for Classic + some BLE)
            var selector = device.DeviceType == "BLE"
                ? BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)
                : BluetoothDevice.GetDeviceSelectorFromPairingState(true);

            var devices = await DeviceInformation.FindAllAsync(
                device.Id,
                new[] { "System.Devices.Aep.DeviceAddress", "System.DeviceInterface.Bluetooth.DeviceAddress" });

            // Try the registry-based approach for battery
            var batteryLevel = await GetBatteryFromRegistryAsync(device.Address);
            if (batteryLevel >= 0)
            {
                device.BatteryLevel = batteryLevel;
                DevicesChanged?.Invoke();
            }
        }
        catch { }
    }

    private static Task<int> GetBatteryFromRegistryAsync(string address)
    {
        return Task.Run(() =>
        {
            try
            {
                // Windows stores BT battery in registry under Bluetooth device keys
                var cleanAddr = address.Replace(":", "").Replace("-", "").ToUpperInvariant();
                var keyPath = $@"SYSTEM\CurrentControlSet\Enum\BTHENUM";

                using var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                if (baseKey == null) return -1;

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    using var subKey = baseKey.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    foreach (var deviceKeyName in subKey.GetSubKeyNames())
                    {
                        if (!deviceKeyName.ToUpperInvariant().Contains(cleanAddr)) continue;

                        // Check for battery info in device parameters
                        using var devParamsKey = subKey.OpenSubKey($@"{deviceKeyName}\Device Parameters");
                        if (devParamsKey?.GetValue("BatteryLevel") is int level)
                            return level;
                        if (devParamsKey?.GetValue("BatteryPercent") is int percent)
                            return percent;
                    }
                }

                // Also check BTHLE enum
                var blePath = $@"SYSTEM\CurrentControlSet\Enum\BTHLE";
                using var bleBase = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(blePath);
                if (bleBase == null) return -1;

                foreach (var subKeyName in bleBase.GetSubKeyNames())
                {
                    using var subKey = bleBase.OpenSubKey(subKeyName);
                    if (subKey == null) continue;

                    foreach (var deviceKeyName in subKey.GetSubKeyNames())
                    {
                        if (!deviceKeyName.ToUpperInvariant().Contains(cleanAddr)) continue;

                        using var devParamsKey = subKey.OpenSubKey($@"{deviceKeyName}\Device Parameters");
                        if (devParamsKey?.GetValue("BatteryLevel") is int level)
                            return level;
                        if (devParamsKey?.GetValue("BatteryPercent") is int percent)
                            return percent;
                    }
                }
            }
            catch { }
            return -1;
        });
    }

    private static T? GetProperty<T>(IReadOnlyDictionary<string, object> props, string key)
    {
        if (props.TryGetValue(key, out var val) && val is T typed)
            return typed;
        return default;
    }

    private static DeviceCategory ClassifyDevice(IReadOnlyDictionary<string, object> props, string deviceType)
    {
        // Try Classic Bluetooth CoD
        var major = GetProperty<byte>(props, "System.Devices.Aep.Bluetooth.Cod.Major");
        var minor = GetProperty<byte>(props, "System.Devices.Aep.Bluetooth.Cod.Minor");

        if (major == 4) // Audio/Video
        {
            return minor switch
            {
                1 or 2 => DeviceCategory.Headphones, // Headset
                6 => DeviceCategory.Headphones,       // Headphones
                4 => DeviceCategory.Headphones,       // Microphone
                _ => DeviceCategory.Speaker,
            };
        }

        if (major == 5) // Peripheral
        {
            return minor switch
            {
                1 => DeviceCategory.Gamepad,    // Joystick
                2 => DeviceCategory.Gamepad,    // Gamepad
                16 => DeviceCategory.Keyboard,  // Keyboard
                32 => DeviceCategory.Mouse,     // Mouse
                48 => DeviceCategory.Keyboard,  // Combo
                _ => DeviceCategory.Other,
            };
        }

        if (major == 2) // Phone
            return DeviceCategory.Phone;

        // Try BLE Appearance
        if (deviceType == "BLE")
        {
            var appearance = GetProperty<ushort>(props, "System.Devices.Aep.Bluetooth.Le.Appearance");
            var category = (appearance >> 6) & 0x3FF; // top 10 bits = category
            return category switch
            {
                3 => DeviceCategory.Headphones,  // HID Generic -> check subcategory
                15 => DeviceCategory.Headphones, // Generic Media Player
                _ when (appearance & 0x3F) == 1 => DeviceCategory.Keyboard,
                _ when (appearance & 0x3F) == 2 => DeviceCategory.Mouse,
                _ when (appearance & 0x3F) == 5 => DeviceCategory.Gamepad,
                _ => DeviceCategory.Other,
            };
        }

        return DeviceCategory.Other;
    }

    public void SetPollingInterval(int seconds)
    {
        if (_pollTimer != null)
            _pollTimer.Interval = TimeSpan.FromSeconds(seconds);
    }

    public void Dispose()
    {
        _pollTimer?.Stop();
        _classicWatcher?.Stop();
        _bleWatcher?.Stop();
    }
}

// Helper to reference GATT UUIDs
internal static class GattServiceUuids
{
    public static readonly Guid Battery = new("0000180f-0000-1000-8000-00805f9b34fb");
}
