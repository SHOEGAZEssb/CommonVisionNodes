using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.Services;

public interface IBackendClient
{
    Task<IReadOnlyList<NodeDefinitionDto>> GetNodeDefinitionsAsync(CancellationToken cancellationToken = default);

    Task<ExecutionAcceptedDto> ExecuteAsync(ExecutionRequestDto request, CancellationToken cancellationToken = default);

    Task StopAsync(string clientId, CancellationToken cancellationToken = default);

    Task<string> GenerateCodeAsync(GraphDto graph, CancellationToken cancellationToken = default);

    Task ListenAsync(string clientId, Func<ExecutionMessageDto, Task> onMessage, CancellationToken cancellationToken = default);
}

