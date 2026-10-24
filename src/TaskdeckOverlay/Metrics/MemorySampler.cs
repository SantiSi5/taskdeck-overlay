namespace TaskdeckOverlay.Metrics;

/// <summary>Physical memory usage from the Win32 GlobalMemoryStatusEx call.</summary>
public sealed class MemorySampler
{
    /// <summary>Returns used physical memory in percent (0-100).</summary>
    public double UsedPercent()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };

        return NativeMethods.GlobalMemoryStatusEx(ref status) ? status.dwMemoryLoad : 0;
    }
}
