using System.Diagnostics;

namespace BluetoothBatteryIndicator.Services;

public static class Log
{
    public static void Info(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);
    }
}
