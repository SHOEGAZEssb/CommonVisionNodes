using System.Text.Json;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;

namespace CommonVisionNodesUI;

/// <summary>
/// Source-generated JSON metadata for persisted graph files.
/// </summary>
/// <remarks>
/// Graph files use the existing browser API JSON conventions, with indentation for readability.
/// </remarks>
[JsonSourceGenerationOptions(
	JsonSerializerDefaults.Web,
	WriteIndented = true,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	UseStringEnumConverter = true)]
[JsonSerializable(typeof(GraphDto))]
internal partial class GraphFileJsonSerializerContext : JsonSerializerContext;
