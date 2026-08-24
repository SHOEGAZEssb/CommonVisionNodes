namespace CommonVisionNodes.Contracts;

/// <summary>
/// Selects how the backend should execute a submitted graph.
/// </summary>
public enum ExecutionModeDto
{
	/// <summary>
	/// Execute exactly one graph frame and then complete.
	/// </summary>
	Single,

	/// <summary>
	/// Execute frames repeatedly until the client stops the run.
	/// </summary>
	Continuous
}

/// <summary>
/// Describes the lifecycle state of a graph execution.
/// </summary>
public enum ExecutionStatusDto
{
	/// <summary>
	/// No graph is currently running for the client.
	/// </summary>
	Idle,

	/// <summary>
	/// The execution request has been accepted and the runtime graph is being built.
	/// </summary>
	Starting,

	/// <summary>
	/// Runtime nodes are initializing external resources such as files, devices, or models.
	/// </summary>
	Initializing,

	/// <summary>
	/// The graph is actively executing.
	/// </summary>
	Running,

	/// <summary>
	/// A single-run graph completed successfully.
	/// </summary>
	Completed,

	/// <summary>
	/// Stop has been requested and shutdown is in progress.
	/// </summary>
	Stopping,

	/// <summary>
	/// Execution has stopped.
	/// </summary>
	Stopped,

	/// <summary>
	/// Execution ended because an error occurred.
	/// </summary>
	Failed
}

/// <summary>
/// Reports the execution state of a single node within a frame.
/// </summary>
public enum NodeExecutionStatusDto
{
	/// <summary>
	/// The node has not run in the current frame.
	/// </summary>
	Pending,

	/// <summary>
	/// The node is opening an external resource or otherwise performing one-time initialization.
	/// </summary>
	Initializing,

	/// <summary>
	/// The node is currently running.
	/// </summary>
	Running,

	/// <summary>
	/// The node completed successfully.
	/// </summary>
	Succeeded,

	/// <summary>
	/// The node failed or reported a runtime error.
	/// </summary>
	Failed
}

/// <summary>
/// Identifies the payload carried by an <see cref="ExecutionMessageDto"/>.
/// </summary>
public enum ExecutionMessageTypeDto
{
	/// <summary>
	/// Message contains an overall execution state update.
	/// </summary>
	ExecutionState,

	/// <summary>
	/// Message contains a per-node execution update.
	/// </summary>
	NodeUpdate,

	/// <summary>
	/// Message contains an image preview payload.
	/// </summary>
	ImagePreview,

	/// <summary>
	/// Message contains histogram preview data.
	/// </summary>
	HistogramPreview,

	/// <summary>
	/// Message contains blob overlay preview data.
	/// </summary>
	BlobPreview,

	/// <summary>
	/// Message contains classification overlay preview data.
	/// </summary>
	ClassificationPreview,

	/// <summary>
	/// Message contains CodeReader overlay preview data.
	/// </summary>
	CodeReaderPreview,

	/// <summary>
	/// Message contains text preview data.
	/// </summary>
	TextPreview,

	/// <summary>
	/// Message reports an execution failure.
	/// </summary>
	Failure,

	/// <summary>
	/// Message reports successful completion.
	/// </summary>
	Completed
}

/// <summary>
/// Identifies a control message sent from an execution WebSocket client to the backend.
/// </summary>
public enum PreviewClientMessageTypeDto
{
	/// <summary>
	/// Announces whether the client acknowledges previews after applying them.
	/// </summary>
	Configure,

	/// <summary>
	/// Confirms that one preview has reached the client's display path.
	/// </summary>
	Acknowledge
}

/// <summary>
/// Small client-to-server control message used to pace live preview delivery.
/// </summary>
public sealed class PreviewClientMessageDto
{
	/// <summary>
	/// Control message discriminator.
	/// </summary>
	public PreviewClientMessageTypeDto MessageType { get; set; }

	/// <summary>
	/// Whether this socket supports per-preview acknowledgements.
	/// Used by <see cref="PreviewClientMessageTypeDto.Configure"/> messages.
	/// </summary>
	public bool SupportsAcknowledgements { get; set; }

	/// <summary>
	/// Runtime execution identifier of the applied preview.
	/// </summary>
	public string ExecutionId { get; set; } = string.Empty;

	/// <summary>
	/// Graph node identifier of the applied preview.
	/// </summary>
	public string NodeId { get; set; } = string.Empty;

	/// <summary>
	/// Sequence identifier copied from the applied preview.
	/// </summary>
	public long PreviewSequence { get; set; }
}

/// <summary>
/// Request body used to start graph execution.
/// </summary>
public sealed class ExecutionRequestDto
{
	/// <summary>
	/// Client identifier used to correlate execution state and WebSocket messages.
	/// </summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>
	/// Graph definition to build and execute.
	/// </summary>
	public GraphDto Graph { get; set; } = new();

	/// <summary>
	/// Execution mode requested by the client.
	/// </summary>
	public ExecutionModeDto Mode { get; set; }

	/// <summary>
	/// Requested preview refresh rate in frames per second. A value of 1001 is treated as unlimited.
	/// </summary>
	public int PreviewRefreshRate { get; set; } = 15;

	/// <summary>
	/// Maximum long-edge dimension for preview images. A value of 0 disables downscaling.
	/// </summary>
	public int PreviewImageMaxDimension { get; set; } = 960;
}

/// <summary>
/// Request body used to stop the current execution for a client.
/// </summary>
public sealed class StopExecutionRequestDto
{
	/// <summary>
	/// Client identifier whose execution should be stopped.
	/// </summary>
	public string ClientId { get; set; } = string.Empty;
}

/// <summary>
/// Request body used to trigger a manual trigger node in a running graph.
/// </summary>
public sealed class TriggerNodeRequestDto
{
	/// <summary>
	/// Client identifier that owns the running graph.
	/// </summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>
	/// Graph node identifier of the manual trigger node.
	/// </summary>
	public string NodeId { get; set; } = string.Empty;
}

/// <summary>
/// Request body used to change live execution-level settings.
/// </summary>
public sealed class UpdateExecutionSettingsRequestDto
{
	/// <summary>
	/// Client identifier that owns the running graph.
	/// </summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>
	/// Runtime execution identifier the update targets. Empty values are accepted for compatibility.
	/// </summary>
	public string ExecutionId { get; set; } = string.Empty;

	/// <summary>
	/// Requested preview refresh rate in frames per second. A value of 1001 is treated as unlimited.
	/// </summary>
	public int PreviewRefreshRate { get; set; } = 15;

	/// <summary>
	/// Maximum long-edge dimension for preview images. A value of 0 disables downscaling.
	/// </summary>
	public int PreviewImageMaxDimension { get; set; } = 960;
}

/// <summary>
/// Request body used to update editable properties of a node in a running graph.
/// </summary>
public sealed class UpdateNodePropertiesRequestDto
{
	/// <summary>
	/// Client identifier that owns the running graph.
	/// </summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>
	/// Runtime execution identifier the update targets. Empty values are accepted for compatibility.
	/// </summary>
	public string ExecutionId { get; set; } = string.Empty;

	/// <summary>
	/// Graph node identifier whose properties should be updated.
	/// </summary>
	public string NodeId { get; set; } = string.Empty;

	/// <summary>
	/// Replacement property values for the target node.
	/// </summary>
	public IList<NodePropertyDto> Properties { get; set; } = [];
}

/// <summary>
/// Response returned after an execution request is accepted.
/// </summary>
public sealed class ExecutionAcceptedDto
{
	/// <summary>
	/// Client identifier that owns the accepted execution.
	/// </summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>
	/// Runtime execution identifier assigned by the backend.
	/// </summary>
	public string ExecutionId { get; set; } = string.Empty;

	/// <summary>
	/// Initial status of the accepted execution.
	/// </summary>
	public ExecutionStatusDto Status { get; set; }
}

/// <summary>
/// Overall execution state sent to subscribed clients.
/// </summary>
public sealed class ExecutionStateDto
{
	/// <summary>
	/// Client identifier associated with this state update.
	/// </summary>
	public string ClientId { get; set; } = string.Empty;

	/// <summary>
	/// Runtime execution identifier associated with this state update.
	/// </summary>
	public string ExecutionId { get; set; } = string.Empty;

	/// <summary>
	/// Current execution status.
	/// </summary>
	public ExecutionStatusDto Status { get; set; }

	/// <summary>
	/// Optional human-readable status message.
	/// </summary>
	public string? Message { get; set; }

	/// <summary>
	/// Number of completed graph frames processed by the current execution.
	/// </summary>
	public long FramesProcessed { get; set; }

	/// <summary>
	/// Estimated completed frames per second for the most recent reporting window.
	/// </summary>
	public double? FramesPerSecond { get; set; }

	/// <summary>
	/// Duration of the most recent graph frame in milliseconds.
	/// </summary>
	public double? LastExecutionDurationMs { get; set; }

	/// <summary>
	/// UTC timestamp when the state was produced.
	/// </summary>
	public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Per-node execution update sent to subscribed clients.
/// </summary>
public sealed class NodeExecutionUpdateDto
{
	/// <summary>
	/// Graph node identifier associated with the update.
	/// </summary>
	public string NodeId { get; set; } = string.Empty;

	/// <summary>
	/// Node execution status.
	/// </summary>
	public NodeExecutionStatusDto Status { get; set; }

	/// <summary>
	/// Optional node-specific message, such as an error or analysis summary.
	/// </summary>
	public string? Message { get; set; }

	/// <summary>
	/// Duration of the node's most recent execution in milliseconds.
	/// </summary>
	public double? ExecutionDurationMs { get; set; }

	/// <summary>
	/// Estimated completed frames per second when this node is a graph terminal.
	/// </summary>
	public double? FramesPerSecond { get; set; }

	/// <summary>
	/// UTC timestamp when the update was produced.
	/// </summary>
	public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// WebSocket message envelope for execution state, node updates, previews, and failures.
/// </summary>
public sealed class ExecutionMessageDto
{
	/// <summary>
	/// Runtime execution identifier associated with this message.
	/// </summary>
	public string ExecutionId { get; set; } = string.Empty;

	/// <summary>
	/// Discriminator indicating which payload property is populated.
	/// </summary>
	public ExecutionMessageTypeDto MessageType { get; set; }

	/// <summary>
	/// Overall execution state payload.
	/// </summary>
	public ExecutionStateDto? ExecutionState { get; set; }

	/// <summary>
	/// Per-node execution update payload.
	/// </summary>
	public NodeExecutionUpdateDto? NodeUpdate { get; set; }

	/// <summary>
	/// Image preview payload.
	/// </summary>
	public ImagePreviewDto? ImagePreview { get; set; }

	/// <summary>
	/// Histogram preview payload.
	/// </summary>
	public HistogramPreviewDto? HistogramPreview { get; set; }

	/// <summary>
	/// Blob preview payload.
	/// </summary>
	public BlobPreviewDto? BlobPreview { get; set; }

	/// <summary>
	/// Classification preview payload.
	/// </summary>
	public ClassificationPreviewDto? ClassificationPreview { get; set; }

	/// <summary>
	/// CodeReader preview payload.
	/// </summary>
	public CodeReaderPreviewDto? CodeReaderPreview { get; set; }

	/// <summary>
	/// Text preview payload.
	/// </summary>
	public TextPreviewDto? TextPreview { get; set; }

	/// <summary>
	/// Error text for failure messages.
	/// </summary>
	public string? Error { get; set; }

	/// <summary>
	/// UTC timestamp when the envelope was produced.
	/// </summary>
	public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
}
