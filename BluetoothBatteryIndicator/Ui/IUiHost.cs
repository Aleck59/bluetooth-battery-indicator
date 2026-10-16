using System.Runtime.CompilerServices;
using BluetoothBatteryIndicator.Services;

namespace BluetoothBatteryIndicator.Ui;

/// <summary>
/// Everything the tray part needs from the windows. The interface mentions no WPF types, so
/// referencing it does not load WPF; only <see cref="UiHostFactory.Create"/> does.
/// </summary>
public interface IUiHost
{
    void ShowSettings();
    void ToggleFlyout();
    IUpdateProgress CreateUpdateProgress();
}

public static class UiHostFactory
{
    // NoInlining: keeps the WpfUiHost reference (and with it WPF) out of callers' JIT
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IUiHost Create(BluetoothService bluetoothService, SettingsService settingsService,
        BatteryHistoryService historyService) =>
        new WpfUiHost(bluetoothService, settingsService, historyService);
}
