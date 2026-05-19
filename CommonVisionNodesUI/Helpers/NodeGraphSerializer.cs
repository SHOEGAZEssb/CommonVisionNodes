using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.Helpers;

/// <summary>
/// Serializes and deserializes node graphs using the UI file format.
/// </summary>
public static class NodeGraphSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static NodeGraphSerializer()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    /// <summary>
    /// Serializes a graph to indented JSON.
    /// </summary>
    /// <param name="graph">Graph to serialize.</param>
    /// <returns>JSON representation of the graph.</returns>
    public static string Serialize(GraphDto graph)
        => JsonSerializer.Serialize(graph, JsonOptions);

    /// <summary>
    /// Deserializes a graph from JSON.
    /// </summary>
    /// <param name="json">JSON content.</param>
    /// <returns>The deserialized graph, or <c>null</c> if the JSON represents null.</returns>
    public static GraphDto? Deserialize(string json)
        => JsonSerializer.Deserialize<GraphDto>(json, JsonOptions);
}
