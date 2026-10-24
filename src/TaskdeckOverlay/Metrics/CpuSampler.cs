namespace TaskdeckOverlay.Metrics;

/// <summary>
/// Total CPU usage from the Win32 GetSystemTimes call. No extra dependencies
/// and no performance counters, so it is very cheap to call once per second.
/// </summary>
public sealed class CpuSampler
{
    private ulong _prevIdle;
    private ulong _prevTotal;
    private bool _hasPrevious;

    /// <summary>Returns CPU usage in percent (0-100) since the previous call.</summary>
    public double Sample()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return 0;
        }

        ulong idleNow = idle.ToUInt64();
        // Kernel time already includes idle time.
        ulong totalNow = kernel.ToUInt64() + user.ToUInt64();

        double usage = 0;
        if (_hasPrevious)
        {
            ulong totalDelta = totalNow - _prevTotal;
            ulong idleDelta = idleNow - _prevIdle;
            if (totalDelta > 0)
            {
                usage = (totalDelta - idleDelta) * 100.0 / totalDelta;
            }
        }

        _prevIdle = idleNow;
        _prevTotal = totalNow;
        _hasPrevious = true;

        return Math.Clamp(usage, 0, 100);
    }
}
