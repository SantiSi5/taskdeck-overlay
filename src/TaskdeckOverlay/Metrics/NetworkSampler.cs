using System.Diagnostics;
using System.Net.NetworkInformation;

namespace TaskdeckOverlay.Metrics;

/// <summary>
/// Network throughput from the byte counters of the active network interfaces.
/// Needs no special permissions. The interface list is cached and refreshed
/// only every so often to avoid enumerating adapters on every sample.
/// </summary>
public sealed class NetworkSampler
{
    private const int RefreshEverySamples = 30;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private List<NetworkInterface> _interfaces = new();
    private int _samplesSinceRefresh = RefreshEverySamples;

    private long _prevReceived;
    private long _prevSent;
    private double _prevSeconds;
    private bool _hasPrevious;

    /// <summary>Returns (download, upload) in bytes per second since the previous call.</summary>
    public (double Down, double Up) Sample()
    {
        if (_samplesSinceRefresh >= RefreshEverySamples)
        {
            RefreshInterfaces();
            _samplesSinceRefresh = 0;
        }
        _samplesSinceRefresh++;

        long received = 0;
        long sent = 0;
        foreach (var nic in _interfaces)
        {
            try
            {
                var stats = nic.GetIPv4Statistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch (NetworkInformationException)
            {
                // Adapter disappeared; it will be dropped on the next refresh.
            }
        }

        double now = _clock.Elapsed.TotalSeconds;
        double down = 0;
        double up = 0;

        if (_hasPrevious)
        {
            double elapsed = now - _prevSeconds;
            if (elapsed > 0)
            {
                down = Math.Max(0, received - _prevReceived) / elapsed;
                up = Math.Max(0, sent - _prevSent) / elapsed;
            }
        }

        _prevReceived = received;
        _prevSent = sent;
        _prevSeconds = now;
        _hasPrevious = true;

        return (down, up);
    }

    private void RefreshInterfaces()
    {
        _interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                        && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .ToList();

        // Interfaces changed: restart the baseline so a new adapter does not cause a spike.
        _hasPrevious = false;
    }
}
