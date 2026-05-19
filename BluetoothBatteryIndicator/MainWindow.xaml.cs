using System.Windows;
using System.Windows.Controls;
using BluetoothBatteryIndicator.Models;
using BluetoothBatteryIndicator.Services;
using Wpf.Ui.Controls;

namespace BluetoothBatteryIndicator;

public partial class MainWindow : FluentWindow
{
    private readonly BluetoothService _bluetoothService;
    private readonly SettingsService _settingsService;
    private readonly BatteryHistoryService _historyService;

    public MainWindow(BluetoothService bluetoothService, SettingsService settingsService, BatteryHistoryService historyService)
    {
        _bluetoothService = bluetoothService;
        _settingsService = settingsService;
        _historyService = historyService;

        InitializeComponent();

        var version = typeof(App).Assembly.GetName().Version;
        Title = $"Bluetooth Battery Indicator v{version?.Major}.{version?.Minor}.{version?.Build}";

        try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/app.ico")); }
        catch { }

        Width = _settingsService.Settings.WindowWidth;
        Height = _settingsService.Settings.WindowHeight;
        if (_settingsService.Settings.WindowLeft >= 0 && _settingsService.Settings.WindowTop >= 0)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = _settingsService.Settings.WindowLeft;
            Top = _settingsService.Settings.WindowTop;
        }
        if (_settingsService.Settings.WindowMaximized)
            WindowState = WindowState.Maximized;

        DeviceListView.ItemsSource = _bluetoothService.Devices;

        _bluetoothService.DevicesChanged += () => Dispatcher.Invoke(() =>
        {
            DeviceListView.Items.Refresh();
            PopulateTrayDeviceCombo();
            UpdateSparkline();
        });

        PopulateTrayDeviceCombo();
        PopulatePollingCombo();
        AutoUpdateToggle.IsChecked = _settingsService.Settings.AutoUpdate;
    }

    private void PopulatePollingCombo()
    {
        var items = new List<KeyValuePair<int, string>>
        {
            new(30, "30 seconds"),
            new(60, "1 minute"),
            new(300, "5 minutes"),
        };

        PollingCombo.DisplayMemberPath = "Value";
        PollingCombo.SelectedValuePath = "Key";
        PollingCombo.ItemsSource = items;
        PollingCombo.SelectedValue = _settingsService.Settings.PollingIntervalSeconds;

        if (PollingCombo.SelectedIndex < 0)
            PollingCombo.SelectedIndex = 1;
    }

    private void PollingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PollingCombo.SelectedValue is int seconds)
        {
            _settingsService.Settings.PollingIntervalSeconds = seconds;
            _settingsService.Save();
            _bluetoothService.SetPollingInterval(seconds);
        }
    }

    private void PopulateTrayDeviceCombo()
    {
        var currentSelection = _settingsService.Settings.TrayIconDeviceId;
        var items = new List<KeyValuePair<string?, string>>
        {
            new(null, "Lowest battery (auto)")
        };

        foreach (var device in _bluetoothService.Devices)
        {
            items.Add(new(device.Id, device.DisplayName));
        }

        TrayDeviceCombo.DisplayMemberPath = "Value";
        TrayDeviceCombo.SelectedValuePath = "Key";
        TrayDeviceCombo.ItemsSource = items;
        TrayDeviceCombo.SelectedValue = currentSelection;

        if (TrayDeviceCombo.SelectedIndex < 0)
            TrayDeviceCombo.SelectedIndex = 0;
    }

    private void TrayDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TrayDeviceCombo.SelectedValue is string deviceId)
            _settingsService.Settings.TrayIconDeviceId = deviceId;
        else
            _settingsService.Settings.TrayIconDeviceId = null;

        _settingsService.Save();
        _settingsService.NotifyChanged();
    }

    private void DeviceListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeviceListView.SelectedItem is BluetoothDeviceInfo device)
        {
            DetailPanel.Visibility = Visibility.Visible;
            EmptyDetail.Visibility = Visibility.Collapsed;

            DetailName.Text = device.Name;
            DetailType.Text = device.DeviceType;
            DetailAddress.Text = device.Address;
            DetailStatus.Text = device.StatusText;
            DetailBattery.Text = device.BatteryText;
            DetailBattery.Foreground = device.BatteryColor;
            BatteryBar.Value = device.BatteryLevel >= 0 ? device.BatteryLevel : 0;
            BatteryBar.Foreground = device.BatteryColor;
            NicknameBox.Text = device.Nickname;
            PopulateCategoryCombo(device);
            UpdateSparkline();
        }
        else
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            EmptyDetail.Visibility = Visibility.Visible;
        }
    }

    private void UpdateSparkline()
    {
        if (DeviceListView.SelectedItem is BluetoothDeviceInfo device)
        {
            var readings = _historyService.GetHistory(device.Id);
            HistorySparkline.Readings = readings.Count > 1 ? readings : null;
        }
    }

    private void DeviceToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.ToggleSwitch toggle &&
            toggle.DataContext is BluetoothDeviceInfo device)
        {
            _settingsService.SetDeviceEnabled(device.Id, toggle.IsChecked == true);
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _bluetoothService.RefreshBatteryLevels();
    }

    private void AutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is Wpf.Ui.Controls.ToggleSwitch toggle)
        {
            _settingsService.Settings.AutoUpdate = toggle.IsChecked == true;
            _settingsService.Save();
        }
    }

    private void NicknameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitNickname();
    }

    private void NicknameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            CommitNickname();
            DeviceListView.Focus(); // move focus away from textbox
        }
    }

    private void CommitNickname()
    {
        if (DeviceListView.SelectedItem is BluetoothDeviceInfo device)
        {
            var nick = NicknameBox.Text.Trim();
            if (nick != device.Nickname)
            {
                _settingsService.SetNickname(device.Id, nick);
                device.Nickname = nick;
            }
        }
    }

    private static readonly List<KeyValuePair<int, string>> CategoryItems = new()
    {
        new((int)DeviceCategory.Headphones, "🎧 Headphones"),
        new((int)DeviceCategory.Speaker, "🔊 Speaker"),
        new((int)DeviceCategory.Mouse, "🖱 Mouse"),
        new((int)DeviceCategory.Keyboard, "⌨ Keyboard"),
        new((int)DeviceCategory.Gamepad, "🎮 Gamepad"),
        new((int)DeviceCategory.Phone, "📱 Phone"),
        new((int)DeviceCategory.Other, "📶 Other"),
    };

    private void PopulateCategoryCombo(BluetoothDeviceInfo device)
    {
        CategoryCombo.DisplayMemberPath = "Value";
        CategoryCombo.SelectedValuePath = "Key";
        CategoryCombo.ItemsSource = CategoryItems;
        CategoryCombo.SelectedValue = (int)device.Category;
    }

    private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryCombo.SelectedValue is int catInt &&
            DeviceListView.SelectedItem is BluetoothDeviceInfo device)
        {
            var cat = (DeviceCategory)catInt;
            device.Category = cat;
            _settingsService.SetCategoryOverride(device.Id, cat);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        _settingsService.Settings.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            _settingsService.Settings.WindowWidth = Width;
            _settingsService.Settings.WindowHeight = Height;
            _settingsService.Settings.WindowLeft = Left;
            _settingsService.Settings.WindowTop = Top;
        }
        _settingsService.Save();
        Hide();
    }
}
