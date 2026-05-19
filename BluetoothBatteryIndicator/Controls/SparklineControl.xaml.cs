using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BluetoothBatteryIndicator.Models;

namespace BluetoothBatteryIndicator.Controls;

public partial class SparklineControl : UserControl
{
    public SparklineControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
    }

    public static readonly DependencyProperty ReadingsProperty =
        DependencyProperty.Register(nameof(Readings), typeof(List<BatteryReading>),
            typeof(SparklineControl), new PropertyMetadata(null, OnReadingsChanged));

    public List<BatteryReading>? Readings
    {
        get => (List<BatteryReading>?)GetValue(ReadingsProperty);
        set => SetValue(ReadingsProperty, value);
    }

    private static void OnReadingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SparklineControl)d).Render();
    }

    private void Render()
    {
        var readings = Readings;
        var w = ActualWidth;
        var h = ActualHeight;

        if (readings == null || readings.Count < 2 || w <= 0 || h <= 0)
        {
            SparkLine.Points.Clear();
            return;
        }

        // Background zones
        GreenZone.Width = w;
        GreenZone.Height = h * 0.5;
        Canvas.SetTop(GreenZone, 0);

        YellowZone.Width = w;
        YellowZone.Height = h * 0.3;
        Canvas.SetTop(YellowZone, h * 0.5);

        RedZone.Width = w;
        RedZone.Height = h * 0.2;
        Canvas.SetTop(RedZone, h * 0.8);

        // Plot points
        var minTime = readings[0].Timestamp.Ticks;
        var maxTime = readings[^1].Timestamp.Ticks;
        var timeRange = maxTime - minTime;

        if (timeRange <= 0)
        {
            SparkLine.Points.Clear();
            return;
        }

        var points = new PointCollection();
        foreach (var r in readings)
        {
            var x = (r.Timestamp.Ticks - minTime) / (double)timeRange * w;
            var y = h - (r.Level / 100.0 * h);
            points.Add(new System.Windows.Point(x, y));
        }

        SparkLine.Points = points;
    }
}
