using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskdeckOverlay.Core;
using TaskdeckOverlay.Metrics;
using TaskdeckOverlay.Sources;

namespace TaskdeckOverlay;

public partial class MainWindow : Window
{
    // Slow sampling keeps CPU use of the overlay itself close to zero.
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    // Hardware sensors are heavier to read, so they are sampled every N ticks.
    private const int HardwareEveryTicks = 2;

    private readonly DispatcherTimer _timer;
    private readonly CpuSampler _cpu = new();
    private readonly MemorySampler _memory = new();
    private readonly DiskSampler _disk = new();
    private readonly NetworkSampler _network = new();

    private const int MaxTaskRows = 4;
    private const int DefaultPort = 47600;

    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(3);

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly DispatcherTimer _flashTimer;
    private readonly TrayIcon _tray;
    private readonly TaskTracker _tasks = new();
    private HookServer? _hookServer;
    private string? _hookStatus;

    private readonly bool _isAdmin = HardwareSampler.IsAdministrator();
    private HardwareSampler? _hardware;
    private HardwareReading? _lastHardware;
    private int _tick;

    public MainWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = SampleInterval };
        _timer.Tick += (_, _) => Refresh();

        _flashTimer = new DispatcherTimer { Interval = FlashDuration };
        _flashTimer.Tick += (_, _) =>
        {
            _flashTimer.Stop();
            Chrome.BorderBrush = Brushes.Transparent;
        };

        Opacity = _settings.Opacity;
        SensorsItem.IsChecked = _settings.SensorsEnabled;
        SoundItem.IsChecked = _settings.SoundEnabled;

        // Events come from server threads; marshal to the UI thread.
        _tasks.StatusChanged += (name, status) => Dispatcher.BeginInvoke(() => Notify(name, status));

        _tray = new TrayIcon();
        _tray.ToggleRequested += ToggleOverlay;
        _tray.ResetPositionRequested += ResetPosition;
        _tray.ExitRequested += Close;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyInitialPosition();

        // Prime the first readings (rate-based samplers need two samples).
        _cpu.Sample();
        _disk.Sample();
        _network.Sample();

        SetSensorsEnabled(SensorsItem.IsChecked);
        StartHookServer();

        Refresh();
        UpdateTimerState();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        SaveSettings();
        _flashTimer.Stop();
        _timer.Stop();
        _disk.Dispose();
        _hardware?.Dispose();
        _hookServer?.Dispose();
        _tray.Dispose();
    }

    private void Window_StateChanged(object? sender, EventArgs e) => UpdateTimerState();

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        _tray.SetOverlayVisible(IsVisible);
        UpdateTimerState();
    }

    private void ToggleOverlay()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        Show();
        Refresh();
    }

    // Recovery option when the overlay ends up somewhere awkward.
    private void ResetPosition()
    {
        if (!IsVisible)
        {
            Show();
        }

        MoveToDefaultPosition();
        SaveSettings();
    }

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

    private void ApplyInitialPosition()
    {
        // Reuse the saved position only if it is still on a connected monitor. Checking the
        // virtual-screen rectangle is not enough: with monitors of different sizes it has
        // gaps that belong to no screen, where the window would be invisible.
        if (_settings.Left is { } left && _settings.Top is { } top
            && IsOnAnyMonitor(left + 20, top + 20))
        {
            Left = left;
            Top = top;
            return;
        }

        MoveToDefaultPosition();
    }

    // Top-right corner of the primary work area.
    private void MoveToDefaultPosition()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 16;
        Top = area.Top + 16;
    }

    // WPF positions are device-independent units; MonitorFromPoint expects physical pixels.
    private bool IsOnAnyMonitor(double x, double y)
    {
        var toDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        var device = toDevice.Transform(new Point(x, y));
        var pt = new NativeMethods.POINT { X = (int)device.X, Y = (int)device.Y };
        return NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONULL) != IntPtr.Zero;
    }

    private void SaveSettings()
    {
        _settings.Left = Left;
        _settings.Top = Top;
        _settings.Opacity = Opacity;
        _settings.SensorsEnabled = SensorsItem.IsChecked;
        _settings.SoundEnabled = SoundItem.IsChecked;
        _settings.Save();
    }

    // Visual cue (colored border for a few seconds) plus an optional system sound.
    // While the overlay is hidden the border cannot be seen, so a tray balloon is used instead.
    private void Notify(string name, AgentTaskStatus status)
    {
        if (!IsVisible)
        {
            string text = status switch
            {
                AgentTaskStatus.Done => "Finished",
                AgentTaskStatus.Waiting => "Waiting for you",
                _ => "Failed"
            };
            _tray.ShowBalloon(name, text);
            return;
        }

        (Color color, SystemSound sound) = status switch
        {
            AgentTaskStatus.Done => (Color.FromRgb(0x3F, 0xB9, 0x50), SystemSounds.Asterisk),
            AgentTaskStatus.Waiting => (Color.FromRgb(0xD2, 0x99, 0x22), SystemSounds.Exclamation),
            _ => (Color.FromRgb(0xF8, 0x51, 0x49), SystemSounds.Hand)
        };

        Chrome.BorderBrush = new SolidColorBrush(color);
        _flashTimer.Stop();
        _flashTimer.Start();

        if (SoundItem.IsChecked)
        {
            sound.Play();
        }
    }

    private void StartHookServer()
    {
        string? token = Environment.GetEnvironmentVariable("TASKDECK_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            _hookStatus = "set TASKDECK_TOKEN (see docs)";
            return;
        }

        int port = int.TryParse(Environment.GetEnvironmentVariable("TASKDECK_PORT"), out var p) ? p : DefaultPort;
        _hookServer = new HookServer(_tasks, port, token);
        _hookServer.Start();

        if (!_hookServer.Running)
        {
            _hookStatus = _hookServer.Error;
        }
    }

    private string FormatTasks()
    {
        if (_hookStatus is not null)
        {
            return $"TASKS  {_hookStatus}";
        }

        var rows = _tasks.Snapshot(DateTime.UtcNow, MaxTaskRows);
        if (rows.Count == 0)
        {
            return "TASKS  idle";
        }

        return string.Join(Environment.NewLine, rows.Select(FormatTaskRow));
    }

    private static string FormatTaskRow(AgentTaskSnapshot t)
    {
        string label = t.Status switch
        {
            AgentTaskStatus.Running => "RUN ",
            AgentTaskStatus.Waiting => "WAIT",
            AgentTaskStatus.Done => "DONE",
            _ => "ERR "
        };

        string name = t.Name.Length > 14 ? t.Name[..13] + "…" : t.Name;
        string time = t.Elapsed.TotalHours >= 1
            ? $"{(int)t.Elapsed.TotalHours}:{t.Elapsed.Minutes:00}:{t.Elapsed.Seconds:00}"
            : $"{t.Elapsed.Minutes}:{t.Elapsed.Seconds:00}";
        string progress = t.Total > 0 ? $"  {t.Completed}/{t.Total}" : string.Empty;

        return $"{label} {name,-14} {time,7}{progress}";
    }

    private void SetSensorsEnabled(bool enabled)
    {
        _hardware?.Dispose();
        _hardware = null;
        _lastHardware = null;

        GpuText.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

        if (enabled)
        {
            _hardware = new HardwareSampler();
            _tick = 0;
        }
    }

    private void Refresh()
    {
        int cpu = (int)Math.Round(_cpu.Sample());
        int ram = (int)Math.Round(_memory.UsedPercent());

        if (_hardware is not null && _tick++ % HardwareEveryTicks == 0)
        {
            _lastHardware = _hardware.Sample();
        }

        string cpuTemp = _hardware is null
            ? string.Empty
            : _lastHardware?.CpuTemperature is { } ct ? $"  {ct:0}°C"
            : _isAdmin ? "  N/A" : "  N/A (needs admin)";
        SetText(CpuText, $"CPU {cpu,3}%{cpuTemp}");
        SetText(RamText, $"RAM {ram,3}%");

        if (_hardware is not null)
        {
            SetText(GpuText, FormatGpu(_hardware, _lastHardware));
        }

        var disk = _disk.Sample();
        SetText(DiskText, disk is { } d
            ? $"DSK R {Formatting.Rate(d.Read)}  W {Formatting.Rate(d.Write)}"
            : "DSK N/A");

        var (down, up) = _network.Sample();
        SetText(NetText, $"NET ↓ {Formatting.Rate(down)}  ↑ {Formatting.Rate(up)}");

        SetText(TasksText, FormatTasks());
    }

    private static string FormatGpu(HardwareSampler sampler, HardwareReading? reading)
    {
        if (!sampler.Available || reading is null || reading.GpuLoad is null)
        {
            return "GPU N/A";
        }

        string text = $"GPU {reading.GpuLoad:0}%";
        if (reading.GpuTemperature is { } t)
        {
            text += $"  {t:0}°C";
        }

        if (reading.GpuMemoryUsedGb is { } used && reading.GpuMemoryTotalGb is { } total)
        {
            text += $"  {used:0.0}/{total:0.0} GB";
        }

        return text;
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

    private void Sensors_Click(object sender, RoutedEventArgs e)
    {
        SetSensorsEnabled(SensorsItem.IsChecked);
        SaveSettings();
    }

    private void Sound_Click(object sender, RoutedEventArgs e) => SaveSettings();

    private void OpacityUp_Click(object sender, RoutedEventArgs e)
    {
        Opacity = Math.Round(Math.Min(1.0, Opacity + 0.1), 1);
        SaveSettings();
    }

    private void OpacityDown_Click(object sender, RoutedEventArgs e)
    {
        Opacity = Math.Round(Math.Max(0.3, Opacity - 0.1), 1);
        SaveSettings();
    }

    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
