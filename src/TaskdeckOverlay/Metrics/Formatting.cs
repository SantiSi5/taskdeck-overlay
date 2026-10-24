namespace TaskdeckOverlay.Metrics;

internal static class Formatting
{
    /// <summary>Formats a bytes-per-second rate as a short string such as "1.2 MB/s".</summary>
    public static string Rate(double bytesPerSecond)
    {
        string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
        double value = Math.Max(0, bytesPerSecond);
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }
}
