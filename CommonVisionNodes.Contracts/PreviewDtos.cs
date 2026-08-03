using System.Text.Json.Serialization;

namespace CommonVisionNodes.Contracts;

/// <summary>
/// Wire format used for an image preview payload.
/// </summary>
public enum ImagePreviewEncodingDto
{
    /// <summary>
    /// Encoded PNG image bytes.
    /// </summary>
    Png,

    /// <summary>
    /// Raw BGRA32 bytes in row-major order.
    /// </summary>
    Bgra32
}

/// <summary>
/// Image preview payload sent from the runtime to the UI.
/// </summary>
public sealed class ImagePreviewDto
{
    /// <summary>
    /// Monotonically increasing identifier assigned by the execution runner for transport acknowledgement.
    /// A value of 0 indicates a producer that does not use acknowledgements.
    /// </summary>
    public long PreviewSequence { get; set; }

    /// <summary>
    /// Graph node identifier that produced the preview.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// MIME media type matching <see cref="Encoding"/>.
    /// </summary>
    public string MediaType { get; set; } = "image/png";

    /// <summary>
    /// Encoding of the image payload.
    /// </summary>
    public ImagePreviewEncodingDto Encoding { get; set; } = ImagePreviewEncodingDto.Png;

    /// <summary>
    /// Base64-encoded preview bytes. Used as a compatibility fallback when previews are sent as text JSON.
    /// </summary>
    public string Base64Data { get; set; } = string.Empty;

    /// <summary>
    /// Raw preview bytes when previews are sent through the binary WebSocket path.
    /// </summary>
    [JsonIgnore]
    public byte[]? BinaryData { get; set; }

    /// <summary>
    /// Source image width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Source image height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Preview payload width in pixels, after optional downscaling.
    /// </summary>
    public int PreviewWidth { get; set; }

    /// <summary>
    /// Preview payload height in pixels, after optional downscaling.
    /// </summary>
    public int PreviewHeight { get; set; }

    /// <summary>
    /// Byte stride for raw encodings, or 0 when the payload is self-describing.
    /// </summary>
    public int Stride { get; set; }

    /// <summary>
    /// Human-readable pixel format description.
    /// </summary>
    public string PixelFormat { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the preview was produced.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Histogram preview payload for one node.
/// </summary>
public sealed class HistogramPreviewDto
{
    /// <summary>
    /// Graph node identifier that produced the preview.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Histogram bin values.
    /// </summary>
    public IList<long> Bins { get; set; } = [];

    /// <summary>
    /// Mean intensity value.
    /// </summary>
    public double Mean { get; set; }

    /// <summary>
    /// Standard deviation of intensity values.
    /// </summary>
    public double StdDev { get; set; }

    /// <summary>
    /// UTC timestamp when the preview was produced.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Serializable details for one detected blob.
/// </summary>
public sealed class BlobInfoDto
{
    /// <summary>
    /// Display label for the blob.
    /// </summary>
    public int Label { get; set; }

    /// <summary>
    /// Blob area in pixels.
    /// </summary>
    public int Area { get; set; }

    /// <summary>
    /// X coordinate of the blob centroid.
    /// </summary>
    public double CentroidX { get; set; }

    /// <summary>
    /// Y coordinate of the blob centroid.
    /// </summary>
    public double CentroidY { get; set; }

    /// <summary>
    /// X origin of the blob bounding box.
    /// </summary>
    public int BoundsX { get; set; }

    /// <summary>
    /// Y origin of the blob bounding box.
    /// </summary>
    public int BoundsY { get; set; }

    /// <summary>
    /// Width of the blob bounding box.
    /// </summary>
    public int BoundsWidth { get; set; }

    /// <summary>
    /// Height of the blob bounding box.
    /// </summary>
    public int BoundsHeight { get; set; }
}

/// <summary>
/// Blob preview payload containing an optional image and blob overlays.
/// </summary>
public sealed class BlobPreviewDto
{
    /// <summary>
    /// Graph node identifier that produced the preview.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Image preview to draw behind blob overlays.
    /// </summary>
    public ImagePreviewDto? Image { get; set; }

    /// <summary>
    /// Blobs to draw over the image.
    /// </summary>
    public IList<BlobInfoDto> Blobs { get; set; } = [];

    /// <summary>
    /// UTC timestamp when the preview was produced.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Serializable classification result for one image point or blob.
/// </summary>
public sealed class ClassificationResultDto
{
    /// <summary>
    /// Zero-based blob index, or -1 when the whole image was classified.
    /// </summary>
    public int BlobIndex { get; set; }

    /// <summary>
    /// Predicted class name.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Classification quality score.
    /// </summary>
    public double Quality { get; set; }

    /// <summary>
    /// X coordinate of the classified point.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y coordinate of the classified point.
    /// </summary>
    public double Y { get; set; }
}

/// <summary>
/// Classification preview payload containing an optional image and result overlays.
/// </summary>
public sealed class ClassificationPreviewDto
{
    /// <summary>
    /// Graph node identifier that produced the preview.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Image preview to draw behind classification overlays.
    /// </summary>
    public ImagePreviewDto? Image { get; set; }

    /// <summary>
    /// Classification results to draw over the image.
    /// </summary>
    public IList<ClassificationResultDto> Results { get; set; } = [];

    /// <summary>
    /// UTC timestamp when the preview was produced.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Serializable image point for a detected CodeReader result.
/// </summary>
public sealed class CodeReaderPointDto
{
    /// <summary>
    /// X coordinate in source image pixels.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y coordinate in source image pixels.
    /// </summary>
    public double Y { get; set; }
}

/// <summary>
/// Serializable decoded code result for image overlay previews.
/// </summary>
public sealed class CodeReaderResultDto
{
    /// <summary>
    /// One-based result index.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Decoded payload.
    /// </summary>
    public string Data { get; set; } = string.Empty;

    /// <summary>
    /// CVB symbology name.
    /// </summary>
    public string Symbology { get; set; } = string.Empty;

    /// <summary>
    /// Decode status reported by CVB.
    /// </summary>
    public string DecodeStatus { get; set; } = string.Empty;

    /// <summary>
    /// X coordinate of the detected code center.
    /// </summary>
    public double CenterX { get; set; }

    /// <summary>
    /// Y coordinate of the detected code center.
    /// </summary>
    public double CenterY { get; set; }

    /// <summary>
    /// Four detected corner points in clockwise order.
    /// </summary>
    public IList<CodeReaderPointDto> Corners { get; set; } = [];

    /// <summary>
    /// 2D result quality, when available.
    /// </summary>
    public int? Quality { get; set; }
}

/// <summary>
/// CodeReader preview payload containing an optional image and code corner overlays.
/// </summary>
public sealed class CodeReaderPreviewDto
{
    /// <summary>
    /// Graph node identifier that produced the preview.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Image preview to draw behind code overlays.
    /// </summary>
    public ImagePreviewDto? Image { get; set; }

    /// <summary>
    /// Decoded code results to draw over the image.
    /// </summary>
    public IList<CodeReaderResultDto> Results { get; set; } = [];

    /// <summary>
    /// Indicates whether decoding hit the configured time limit.
    /// </summary>
    public bool TimeLimitReached { get; set; }

    /// <summary>
    /// UTC timestamp when the preview was produced.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Text preview payload for non-image runtime values.
/// </summary>
public sealed class TextPreviewDto
{
    /// <summary>
    /// Graph node identifier that produced the preview.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Short type description for the displayed value.
    /// </summary>
    public string TypeDescription { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable text to display.
    /// </summary>
    public string DisplayText { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the preview was produced.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
