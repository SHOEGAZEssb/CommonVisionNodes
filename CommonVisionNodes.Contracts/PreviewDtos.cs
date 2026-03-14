namespace CommonVisionNodes.Contracts;

public sealed class ImagePreviewDto
{
    public string NodeId { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image/png";
    public string Base64Data { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string PixelFormat { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class HistogramPreviewDto
{
    public string NodeId { get; set; } = string.Empty;
    public IList<long> Bins { get; set; } = [];
    public double Mean { get; set; }
    public double StdDev { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BlobInfoDto
{
    public int Label { get; set; }
    public int Area { get; set; }
    public double CentroidX { get; set; }
    public double CentroidY { get; set; }
    public int BoundsX { get; set; }
    public int BoundsY { get; set; }
    public int BoundsWidth { get; set; }
    public int BoundsHeight { get; set; }
}

public sealed class BlobPreviewDto
{
    public string NodeId { get; set; } = string.Empty;
    public ImagePreviewDto? Image { get; set; }
    public IList<BlobInfoDto> Blobs { get; set; } = [];
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ClassificationResultDto
{
    public int BlobIndex { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public double Quality { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class ClassificationPreviewDto
{
    public string NodeId { get; set; } = string.Empty;
    public ImagePreviewDto? Image { get; set; }
    public IList<ClassificationResultDto> Results { get; set; } = [];
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TextPreviewDto
{
    public string NodeId { get; set; } = string.Empty;
    public string TypeDescription { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
