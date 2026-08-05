using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI.Helpers;

/// <summary>
/// Serializes and deserializes node graphs using the UI file format.
/// </summary>
public static class NodeGraphSerializer
{
	/// <summary>
	/// Serializes a graph to indented JSON.
	/// </summary>
	/// <param name="graph">Graph to serialize.</param>
	/// <returns>JSON representation of the graph.</returns>
	public static string Serialize(GraphDto graph)
		=> JsonSerializer.Serialize(graph, NodeGraphJsonSerializerContext.Default.GraphDto);

	/// <summary>
	/// Deserializes a graph from JSON.
	/// </summary>
	/// <param name="json">JSON content.</param>
	/// <returns>The deserialized graph, or <c>null</c> if the JSON represents null.</returns>
	public static GraphDto? Deserialize(string json)
		=> JsonSerializer.Deserialize(json, NodeGraphJsonSerializerContext.Default.GraphDto);
}

/// <summary>
/// Source-generated metadata for the legacy graph helper's indented file format.
/// </summary>
[JsonSourceGenerationOptions(
	WriteIndented = true,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	UseStringEnumConverter = true)]
[JsonSerializable(typeof(GraphDto))]
internal partial class NodeGraphJsonSerializerContext : JsonSerializerContext;
