using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TaskdeckOverlay.Metrics;

namespace TaskdeckOverlay;

public partial class MainWindow : Window
{
    // Slow sampling keeps CPU use of the overlay itself close to zero.
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private readonly DispatcherTimer _timer;
    private readonly CpuSampler _cpu = new();
    private readonly MemorySampler _memory = new();
    private readonly DiskSampler _disk = new();
    private readonly NetworkSampler _network = new();

    public MainWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = SampleInterval };
        _timer.Tick += (_, _) => Refresh();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Start in the top-right corner of the work area.
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 16;
        Top = area.Top + 16;

        // Prime the first readings (rate-based samplers need two samples).
        _cpu.Sample();
        _disk.Sample();
        _network.Sample();

        Refresh();
        UpdateTimerState();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _disk.Dispose();
    }

    private void Window_StateChanged(object? sender, EventArgs e) => UpdateTimerState();

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateTimerState();

    // Do not sample while the window is hidden or minimized.
    private void UpdateTimerState()
    {
        bool active = IsVisible && WindowState != WindowState.Minimized;
        if (active && !_timer.IsEnabled)
        {
            _timer.Start();
        }
        else if (!active && _timer.IsEnabled)
        {
            _timer.Stop();
        }
    }

    private void Refresh()
    {
        int cpu = (int)Math.Round(_cpu.Sample());
        int ram = (int)Math.Round(_memory.UsedPercent());

        SetText(CpuText, $"CPU {cpu,3}%");
        SetText(RamText, $"RAM {ram,3}%");

        var disk = _disk.Sample();
        SetText(DiskText, disk is { } d
            ? $"DSK R {Formatting.Rate(d.Read)}  W {Formatting.Rate(d.Write)}"
            : "DSK N/A");

        var (down, up) = _network.Sample();
        SetText(NetText, $"NET ↓ {Formatting.Rate(down)}  ↑ {Formatting.Rate(up)}");
    }

    // Only touch the UI when the text actually changed.
    private static void SetText(TextBlock block, string text)
    {
        if (block.Text != text)
        {
            block.Text = text;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OpacityUp_Click(object sender, RoutedEventArgs e) => Opacity = Math.Min(1.0, Opacity + 0.1);

    private void OpacityDown_Click(object sender, RoutedEventArgs e) => Opacity = Math.Max(0.3, Opacity - 0.1);

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
