using System.Security.Principal;
using LibreHardwareMonitor.Hardware;

namespace TaskdeckOverlay.Metrics;

/// <summary>Latest values from hardware sensors. Any value may be null when unavailable.</summary>
public sealed record HardwareReading(
    double? CpuTemperature,
    double? GpuLoad,
    double? GpuTemperature,
    double? GpuMemoryUsedGb,
    double? GpuMemoryTotalGb);

/// <summary>
/// CPU temperature and GPU metrics through LibreHardwareMonitor.
/// Only the CPU and GPU groups are enabled and only those two devices are
/// updated, to keep the cost low. CPU temperature normally needs the app to run
/// as administrator; without it, values simply come back as null.
/// </summary>
public sealed class HardwareSampler : IDisposable
{
    private readonly Computer? _computer;
    private readonly IHardware? _cpu;
    private readonly IHardware? _gpu;

    public bool Available { get; }

    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public HardwareSampler()
    {
        try
        {
            _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            _computer.Open();

            _cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            _gpu = _computer.Hardware.FirstOrDefault(h =>
                       h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                   ?? _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuIntel);

            Available = true;
        }
        catch (Exception)
        {
            // Driver or permission problems inside the third-party library must not
            // take the overlay down: report "unavailable" and keep running.
            Available = false;
            _computer?.Close();
            _computer = null;
        }
    }

    public HardwareReading Sample()
    {
        if (!Available)
        {
            return new HardwareReading(null, null, null, null, null);
        }

        _cpu?.Update();
        _gpu?.Update();

        double? cpuTemp = CpuTemperature(_cpu);
        double? gpuLoad = null;
        double? gpuTemp = null;
        double? memUsedMb = null;
        double? memTotalMb = null;

        if (_gpu is not null)
        {
            foreach (var sensor in _gpu.Sensors)
            {
                switch (sensor.SensorType, sensor.Name)
                {
                    case (SensorType.Load, "GPU Core"):
                        gpuLoad = sensor.Value;
                        break;
                    case (SensorType.Temperature, "GPU Core"):
                        gpuTemp = sensor.Value;
                        break;
                    case (SensorType.SmallData, "GPU Memory Used"):
                        memUsedMb = sensor.Value;
                        break;
                    case (SensorType.SmallData, "GPU Memory Total"):
                        memTotalMb = sensor.Value;
                        break;
                }
            }
        }

        return new HardwareReading(
            cpuTemp,
            gpuLoad,
            gpuTemp,
            memUsedMb / 1024.0,
            memTotalMb / 1024.0);
    }

    private static double? CpuTemperature(IHardware? cpu)
    {
        if (cpu is null)
        {
            return null;
        }

        var temps = cpu.Sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue).ToList();
        if (temps.Count == 0)
        {
            return null;
        }

        // Prefer the package-level reading; otherwise use the hottest core.
        var package = temps.FirstOrDefault(s =>
            s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
            || s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
            || s.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase));

        return package?.Value ?? temps.Max(s => s.Value);
    }

    public void Dispose() => _computer?.Close();
}
