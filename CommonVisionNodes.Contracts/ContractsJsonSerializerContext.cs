using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommonVisionNodes.Contracts;

/// <summary>
/// Source-generated JSON metadata for the contracts exchanged between the UI and backend.
/// </summary>
/// <remarks>
/// Keeping the wire types rooted here makes the browser WebAssembly build safe to trim while
/// retaining the existing camel-case, string-enum JSON contract.
/// </remarks>
[JsonSourceGenerationOptions(
	JsonSerializerDefaults.Web,
	DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	UseStringEnumConverter = true)]
[JsonSerializable(typeof(GraphDto))]
[JsonSerializable(typeof(List<NodeDefinitionDto>), TypeInfoPropertyName = "NodeDefinitionList")]
[JsonSerializable(typeof(PathPickerRequestDto))]
[JsonSerializable(typeof(PathPickerResultDto))]
[JsonSerializable(typeof(ExecutionRequestDto))]
[JsonSerializable(typeof(ExecutionAcceptedDto))]
[JsonSerializable(typeof(StopExecutionRequestDto))]
[JsonSerializable(typeof(TriggerNodeRequestDto))]
[JsonSerializable(typeof(UpdateExecutionSettingsRequestDto))]
[JsonSerializable(typeof(UpdateNodePropertiesRequestDto))]
[JsonSerializable(typeof(PreviewClientMessageDto))]
[JsonSerializable(typeof(ExecutionMessageDto))]
public partial class ContractsJsonSerializerContext : JsonSerializerContext;
