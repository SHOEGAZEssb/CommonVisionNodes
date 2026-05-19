using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime.Execution;

namespace CommonVisionNodes.Runtime;

/// <summary>
/// Builds a runtime graph from a DTO and emits equivalent standalone CVB SDK code.
/// </summary>
public sealed class RuntimeCodeGenerationService
{
    private readonly RuntimeGraphFactory _graphFactory;

    /// <summary>
    /// Creates a code generation service.
    /// </summary>
    /// <param name="graphFactory">Factory used to materialize the graph before generation.</param>
    public RuntimeCodeGenerationService(RuntimeGraphFactory graphFactory)
    {
        _graphFactory = graphFactory;
    }

    /// <summary>
    /// Generates standalone C# code for a serialized graph.
    /// </summary>
    /// <param name="graphDto">Graph to generate code for.</param>
    /// <returns>C# source code that uses the CVB SDK directly.</returns>
    public string GenerateCode(GraphDto graphDto)
    {
        using var graph = _graphFactory.Build(graphDto);
        return CodeGenerator.Generate(graph.Graph);
    }
}
