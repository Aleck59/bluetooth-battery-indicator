namespace BluetoothBatteryIndicator.Services;

/// <summary>Hidden top-level window that a second instance finds by title and signals to open settings.</summary>
public sealed class InstanceSignalWindow : System.Windows.Forms.NativeWindow, IDisposable
{
    public const uint WM_SHOWSETTINGS = 0x0400 + 1; // WM_USER + 1

    private readonly Action _onSignal;

    public InstanceSignalWindow(string title, Action onSignal)
    {
        _onSignal = onSignal;
        CreateHandle(new System.Windows.Forms.CreateParams { Caption = title });
    }

    protected override void WndProc(ref System.Windows.Forms.Message m)
    {
        if (m.Msg == WM_SHOWSETTINGS)
        {
            _onSignal();
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose() => DestroyHandle();
}
