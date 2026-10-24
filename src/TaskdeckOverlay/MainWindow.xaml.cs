using System.Windows;
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

    private int _lastCpu = -1;
    private int _lastRam = -1;

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

        _cpu.Sample(); // prime the first reading
        Refresh();
        UpdateTimerState();
    }

    private void Window_Closed(object? sender, EventArgs e) => _timer.Stop();

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

        // Only touch the UI when a value actually changed.
        if (cpu != _lastCpu)
        {
            _lastCpu = cpu;
            CpuText.Text = $"CPU {cpu,3}%";
        }

        if (ram != _lastRam)
        {
            _lastRam = ram;
            RamText.Text = $"RAM {ram,3}%";
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
