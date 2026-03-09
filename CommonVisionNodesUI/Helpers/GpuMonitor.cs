using System.Diagnostics;

namespace CommonVisionNodesUI.Helpers;

/// <summary>
/// Monitors GPU utilization for the current process using Windows "GPU Engine"
/// performance counters. Falls back gracefully to "N/A" on unsupported platforms.
/// </summary>
internal sealed class GpuMonitor : IDisposable
{
    private readonly int _pid;
    private List<PerformanceCounter> _counters = [];
    private DateTime _lastRefresh;
    private bool _isSupported = true;

    public GpuMonitor()
    {
        _pid = Environment.ProcessId;
        RefreshCounters();
    }

    /// <summary>
    /// Returns the current GPU utilization percentage for this process,
    /// or <c>null</c> if GPU monitoring is not available.
    /// </summary>
    public float? GetUtilization()
    {
        if (!_isSupported)
            return null;

        // Refresh counter instances periodically (engines can appear/disappear)
        if ((DateTime.UtcNow - _lastRefresh).TotalSeconds > 10)
            RefreshCounters();

        if (_counters.Count == 0)
            return null;

        float maxUtil = 0;
        foreach (var counter in _counters)
        {
            try
            {
                var val = counter.NextValue();
                if (val > maxUtil)
                    maxUtil = val;
            }
            catch
            {
                // Individual counter may become invalid; ignore until next refresh
            }
        }

        return maxUtil;
    }

    private void RefreshCounters()
    {
        DisposeCounters();
        _lastRefresh = DateTime.UtcNow;

        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames();
            var pidPrefix = $"pid_{_pid}_";

            foreach (var instance in instances)
            {
                if (instance.Contains(pidPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, readOnly: true);
                    counter.NextValue(); // First call initializes the counter baseline
                    _counters.Add(counter);
                }
            }
        }
        catch
        {
            // GPU Engine category not available (non-Windows or older OS)
            _isSupported = false;
        }
    }

    private void DisposeCounters()
    {
        foreach (var c in _counters)
            c.Dispose();
        _counters.Clear();
    }

    public void Dispose() => DisposeCounters();
}
