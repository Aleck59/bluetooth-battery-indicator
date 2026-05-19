using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BluetoothBatteryIndicator.Models;

public class BluetoothDeviceInfo : INotifyPropertyChanged
{
    private string _name = "";
    private string _nickname = "";
    private int _batteryLevel = -1;
    private bool _isConnected;
    private bool _isEnabled = true;

    private DeviceCategory _category = DeviceCategory.Other;

    public string Id { get; set; } = "";
    public string Address { get; set; } = "";
    public string DeviceType { get; set; } = "Classic"; // Classic or BLE

    public DeviceCategory Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); OnPropertyChanged(nameof(CategoryIcon)); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string Nickname
    {
        get => _nickname;
        set { _nickname = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? Name : Nickname;

    public int BatteryLevel
    {
        get => _batteryLevel;
        set { _batteryLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(BatteryText)); OnPropertyChanged(nameof(BatteryColor)); }
    }

    public System.Windows.Media.SolidColorBrush BatteryColor => BatteryLevel switch
    {
        < 0 => new(System.Windows.Media.Color.FromRgb(128, 128, 128)),
        <= 20 => new(System.Windows.Media.Color.FromRgb(255, 60, 60)),
        <= 50 => new(System.Windows.Media.Color.FromRgb(255, 200, 0)),
        _ => new(System.Windows.Media.Color.FromRgb(80, 220, 80)),
    };

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(); }
    }

    public string BatteryText => BatteryLevel >= 0 ? $"{BatteryLevel}%" : "N/A";
    public string StatusText => IsConnected ? "Connected" : "Disconnected";
    public string CategoryIcon => Category switch
    {
        DeviceCategory.Headphones => "🎧",
        DeviceCategory.Speaker => "🔊",
        DeviceCategory.Mouse => "🖱",
        DeviceCategory.Keyboard => "⌨",
        DeviceCategory.Gamepad => "🎮",
        DeviceCategory.Phone => "📱",
        _ => "📶",
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum DeviceCategory
{
    Other,
    Headphones,
    Speaker,
    Mouse,
    Keyboard,
    Gamepad,
    Phone,
}
