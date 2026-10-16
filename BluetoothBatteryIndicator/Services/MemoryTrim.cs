using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BluetoothBatteryIndicator.Services;

public static class MemoryTrim
{
    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    /// <summary>
    /// Collects garbage and hands unused pages back to Windows. Used once windows are hidden,
    /// since the WPF code and data they pulled in are not needed while the app sits in the tray.
    /// </summary>
    public static void Trim()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var process = Process.GetCurrentProcess();
        SetProcessWorkingSetSize(process.Handle, (IntPtr)(-1), (IntPtr)(-1));
    }
}
