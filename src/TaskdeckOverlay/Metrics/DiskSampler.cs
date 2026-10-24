using System.Diagnostics;

namespace TaskdeckOverlay.Metrics;

/// <summary>
/// Disk throughput from the Windows "PhysicalDisk" performance counters.
/// Counters are created lazily; if they are unavailable (for example corrupted
/// counter registry) the sampler reports null instead of throwing.
/// </summary>
public sealed class DiskSampler : IDisposable
{
    private PerformanceCounter? _read;
    private PerformanceCounter? _write;
    private bool _failed;

    /// <summary>Returns (read, write) in bytes per second, or null if unavailable.</summary>
    public (double Read, double Write)? Sample()
    {
        if (_failed)
        {
            return null;
        }

        try
        {
            _read ??= new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", readOnly: true);
            _write ??= new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", readOnly: true);
            return (_read.NextValue(), _write.NextValue());
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException
                                       or System.ComponentModel.Win32Exception)
        {
            _failed = true;
            Dispose();
            return null;
        }
    }

    public void Dispose()
    {
        _read?.Dispose();
        _write?.Dispose();
        _read = null;
        _write = null;
    }
}
