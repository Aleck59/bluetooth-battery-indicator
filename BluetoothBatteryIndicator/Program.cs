using System.Runtime.InteropServices;
using BluetoothBatteryIndicator.Services;

namespace BluetoothBatteryIndicator;

/// <summary>
/// Entry point. The tray part runs on a plain WinForms message loop; WPF (and WPF-UI) is loaded
/// only when a window is opened, which keeps idle memory and startup cost low.
/// </summary>
public static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    public const string SignalWindowTitle = "Bluetooth Battery Indicator";

    [STAThread]
    public static void Main()
    {
        AttachConsole(-1); // Attach to parent console if launched from terminal

        using var mutex = new Mutex(true, "BluetoothBatteryIndicator_SingleInstance", out bool isNew);
        if (!isNew)
        {
            Log.Info("Another instance is already running. Signaling it to show settings.");
            var hwnd = FindWindow(null, SignalWindowTitle);
            if (hwnd != IntPtr.Zero)
                PostMessage(hwnd, InstanceSignalWindow.WM_SHOWSETTINGS, IntPtr.Zero, IntPtr.Zero);
            return;
        }

        Log.Info("Starting Bluetooth Battery Indicator");
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Forms.WindowsFormsSynchronizationContext());

        System.Windows.Forms.Application.Run(new TrayApplication());

        mutex.ReleaseMutex();
    }
}
