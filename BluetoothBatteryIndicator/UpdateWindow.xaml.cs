using System.Windows;

namespace BluetoothBatteryIndicator;

public partial class UpdateWindow : Window
{
    public UpdateWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string status)
    {
        Dispatcher.Invoke(() => StatusText.Text = status);
    }

    public void SetProgress(double percent)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = percent;
            PercentText.Text = $"{percent:0}%";
        });
    }
}
