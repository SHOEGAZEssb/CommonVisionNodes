using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime.Execution;

namespace CommonVisionNodes.Runtime;

public sealed class RuntimeCodeGenerationService
{
    private readonly RuntimeGraphFactory _graphFactory;

    public RuntimeCodeGenerationService(RuntimeGraphFactory graphFactory)
    {
        _graphFactory = graphFactory;
    }

    public string GenerateCode(GraphDto graphDto)
    {
        using var graph = _graphFactory.Build(graphDto);
        return CodeGenerator.Generate(graph.Graph);
    }
}
