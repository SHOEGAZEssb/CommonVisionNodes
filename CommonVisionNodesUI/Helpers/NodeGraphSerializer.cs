using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.Helpers;

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

    public static string Serialize(GraphDto graph)
        => JsonSerializer.Serialize(graph, JsonOptions);

    public static GraphDto? Deserialize(string json)
        => JsonSerializer.Deserialize<GraphDto>(json, JsonOptions);
}
