namespace CommonVisionNodes.Contracts;

public enum ExecutionModeDto
{
    Single,
    Continuous
}

public enum ExecutionStatusDto
{
    Idle,
    Starting,
    Initializing,
    Running,
    Completed,
    Stopping,
    Stopped,
    Failed
}

public enum NodeExecutionStatusDto
{
    Pending,
    Running,
    Succeeded,
    Failed
}

public enum ExecutionMessageTypeDto
{
    ExecutionState,
    NodeUpdate,
    ImagePreview,
    HistogramPreview,
    BlobPreview,
    ClassificationPreview,
    TextPreview,
    Failure,
    Completed
}

public sealed class ExecutionRequestDto
{
    public string ClientId { get; set; } = string.Empty;
    public GraphDto Graph { get; set; } = new();
    public ExecutionModeDto Mode { get; set; }
    public int PreviewRefreshRate { get; set; } = 30;
}

public sealed class StopExecutionRequestDto
{
    public string ClientId { get; set; } = string.Empty;
}

public sealed class ExecutionAcceptedDto
{
    public string ClientId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public ExecutionStatusDto Status { get; set; }
}

public sealed class ExecutionStateDto
{
    public string ClientId { get; set; } = string.Empty;
    public string ExecutionId { get; set; } = string.Empty;
    public ExecutionStatusDto Status { get; set; }
    public string? Message { get; set; }
    public long FramesProcessed { get; set; }
    public double? FramesPerSecond { get; set; }
    public double? LastExecutionDurationMs { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class NodeExecutionUpdateDto
{
    public string NodeId { get; set; } = string.Empty;
    public NodeExecutionStatusDto Status { get; set; }
    public string? Message { get; set; }
    public double? ExecutionDurationMs { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ExecutionMessageDto
{
    public ExecutionMessageTypeDto MessageType { get; set; }
    public ExecutionStateDto? ExecutionState { get; set; }
    public NodeExecutionUpdateDto? NodeUpdate { get; set; }
    public ImagePreviewDto? ImagePreview { get; set; }
    public HistogramPreviewDto? HistogramPreview { get; set; }
    public BlobPreviewDto? BlobPreview { get; set; }
    public ClassificationPreviewDto? ClassificationPreview { get; set; }
    public TextPreviewDto? TextPreview { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
