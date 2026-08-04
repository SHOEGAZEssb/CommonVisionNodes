using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.Services;

/// <summary>
/// Client abstraction for the CommonVisionNodes backend API and execution WebSocket.
/// </summary>
public interface IBackendClient
{
	/// <summary>
	/// Gets all node definitions exposed by the backend.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Available node definitions.</returns>
	Task<IReadOnlyList<NodeDefinitionDto>> GetNodeDefinitionsAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Shows a native path picker on the local execution backend.
	/// </summary>
	Task<PathPickerResultDto> PickPathAsync(
		PathPickerRequestDto request,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Starts graph execution.
	/// </summary>
	/// <param name="request">Execution request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Accepted execution metadata.</returns>
	Task<ExecutionAcceptedDto> ExecuteAsync(ExecutionRequestDto request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Stops execution for a client.
	/// </summary>
	/// <param name="clientId">Client identifier.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task StopAsync(string clientId, CancellationToken cancellationToken = default);

	/// <summary>
	/// Queues a manual trigger for a running graph.
	/// </summary>
	/// <param name="request">Trigger request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task TriggerNodeAsync(TriggerNodeRequestDto request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates live execution settings.
	/// </summary>
	/// <param name="request">Settings update request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task UpdateExecutionSettingsAsync(UpdateExecutionSettingsRequestDto request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Updates live node properties.
	/// </summary>
	/// <param name="request">Node property update request.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task UpdateNodePropertiesAsync(UpdateNodePropertiesRequestDto request, CancellationToken cancellationToken = default);

	/// <summary>
	/// Generates standalone code for a graph.
	/// </summary>
	/// <param name="graph">Graph to generate code for.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Generated code.</returns>
	Task<string> GenerateCodeAsync(GraphDto graph, CancellationToken cancellationToken = default);

	/// <summary>
	/// Listens for execution messages for a client.
	/// </summary>
	/// <param name="clientId">Client identifier.</param>
	/// <param name="onMessage">Callback invoked for each message.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	Task ListenAsync(string clientId, Func<ExecutionMessageDto, Task> onMessage, CancellationToken cancellationToken = default);
}
