namespace Cvb.Uno.Toolkit.Helpers;

/// <summary>
/// Keeps only the latest pending value for each key and coordinates a single scheduled drain.
/// </summary>
/// <typeparam name="TKey">Key used to identify replaceable values.</typeparam>
/// <typeparam name="TValue">Buffered value type.</typeparam>
public sealed class CoalescingMessageBuffer<TKey, TValue> where TKey : notnull
{
    private readonly object _sync = new();
    private readonly Dictionary<TKey, TValue> _pending;
    private bool _drainScheduled;

    /// <summary>
    /// Creates a coalescing buffer.
    /// </summary>
    /// <param name="comparer">Optional key comparer.</param>
    public CoalescingMessageBuffer(IEqualityComparer<TKey>? comparer = null)
    {
        _pending = new Dictionary<TKey, TValue>(comparer);
    }

    /// <summary>
    /// Adds or replaces a pending value.
    /// </summary>
    /// <returns><see langword="true"/> when the caller must schedule a drain.</returns>
    public bool AddOrReplace(TKey key, TValue value)
    {
        lock (_sync)
        {
            _pending[key] = value;
            if (_drainScheduled)
                return false;

            _drainScheduled = true;
            return true;
        }
    }

    /// <summary>
    /// Removes at most <paramref name="maximumCount"/> pending values.
    /// </summary>
    /// <param name="maximumCount">Maximum number of values returned for one drain.</param>
    /// <param name="requiresAnotherDrain">
    /// <see langword="true"/> when buffered values remain and the caller must schedule another drain.
    /// </param>
    public IReadOnlyList<TValue> TakeBatch(int maximumCount, out bool requiresAnotherDrain)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);

        lock (_sync)
        {
            var batch = _pending.Take(maximumCount).ToArray();
            foreach (var item in batch)
                _pending.Remove(item.Key);

            requiresAnotherDrain = _pending.Count > 0;
            _drainScheduled = requiresAnotherDrain;
            return Array.ConvertAll(batch, static item => item.Value);
        }
    }

    /// <summary>
    /// Clears buffered values. An already scheduled drain remains valid and will observe an empty buffer.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
            _pending.Clear();
    }

    /// <summary>
    /// Releases the scheduled-drain marker after dispatching failed.
    /// </summary>
    public void CancelScheduledDrain()
    {
        lock (_sync)
            _drainScheduled = false;
    }
}
