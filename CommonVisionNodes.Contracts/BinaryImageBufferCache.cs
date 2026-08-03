namespace CommonVisionNodes.Contracts;

/// <summary>
/// Reuses two exact-size raw image buffers per preview node.
/// </summary>
/// <remarks>
/// Alternating buffers keeps the most recently delivered frame stable while the next frame is
/// received. This avoids a large-object-heap allocation for every raw preview without allowing
/// an in-progress receive to overwrite the buffer currently owned by the frontend.
/// </remarks>
public sealed class BinaryImageBufferCache
{
    private readonly Dictionary<string, BufferPair> _buffers = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the next reusable buffer for a raw image preview.
    /// </summary>
    /// <param name="imagePreview">Preview metadata used to identify the producer.</param>
    /// <param name="requiredByteCount">Exact number of bytes required by the frame.</param>
    /// <returns>An exact-size buffer that is different from the one returned for the preceding frame from the same node.</returns>
    public byte[] GetNextBuffer(ImagePreviewDto imagePreview, int requiredByteCount)
    {
        ArgumentNullException.ThrowIfNull(imagePreview);
        return GetNextBuffer(imagePreview.NodeId, requiredByteCount);
    }

    /// <summary>
    /// Gets the next reusable buffer for an image producer.
    /// </summary>
    /// <param name="bufferKey">Stable image producer identifier.</param>
    /// <param name="requiredByteCount">Exact number of bytes required by the frame.</param>
    /// <returns>An exact-size buffer that alternates for successive calls with the same key.</returns>
    public byte[] GetNextBuffer(string bufferKey, int requiredByteCount)
    {
        ArgumentNullException.ThrowIfNull(bufferKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredByteCount);

        if (!_buffers.TryGetValue(bufferKey, out var pair) || pair.ByteCount != requiredByteCount)
        {
            pair = new BufferPair(requiredByteCount);
            _buffers[bufferKey] = pair;
        }

        return pair.GetNextBuffer();
    }

    /// <summary>
    /// Releases references to all cached buffers.
    /// </summary>
    public void Clear() => _buffers.Clear();

    private sealed class BufferPair(int byteCount)
    {
        private readonly byte[] _first = GC.AllocateUninitializedArray<byte>(byteCount);
        private byte[]? _second;
        private bool _returnSecond;

        public int ByteCount { get; } = byteCount;

        public byte[] GetNextBuffer()
        {
            if (_returnSecond)
                _second ??= GC.AllocateUninitializedArray<byte>(ByteCount);

            var result = _returnSecond ? _second! : _first;
            _returnSecond = !_returnSecond;
            return result;
        }
    }
}
