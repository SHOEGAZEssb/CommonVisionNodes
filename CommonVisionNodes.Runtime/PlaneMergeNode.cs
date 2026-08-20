using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes.Runtime;

/// <summary>
/// Combines configured single-plane image inputs into one multi-plane image.
/// </summary>
public sealed class PlaneMergeNode : Node
{
	/// <summary>
	/// Largest number of individual plane inputs supported by this node.
	/// </summary>
	public const int MaximumPlaneCount = 16;

	private readonly List<Port> _planeInputs = [];
	private readonly List<double> _planeWeights = [];
	private Image? _lastResult;
	private int _planeCount = 3;

	/// <summary>
	/// Individual image inputs, named <c>Plane 0</c>, <c>Plane 1</c>, and so on.
	/// Each input must be a single-plane image with matching dimensions and data type.
	/// </summary>
	public IReadOnlyList<Port> PlaneInputs => _planeInputs;

	/// <summary>
	/// Per-plane intensity multipliers. Values are serialized as a comma-separated list.
	/// </summary>
	public string PlaneWeights
	{
		get => string.Join(',', _planeWeights.Select(weight => weight.ToString("G17", CultureInfo.InvariantCulture)));
		set
		{
			var values = (value ?? string.Empty)
				.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(ParseWeight)
				.ToList();
			SynchronizeWeights(values);
		}
	}

	/// <summary>
	/// Per-plane intensity multipliers in input-port order.
	/// </summary>
	public IReadOnlyList<double> Weights => _planeWeights;

	/// <summary>
	/// Output port that provides the merged multi-plane image.
	/// </summary>
	public Port ImageOutput { get; }

	/// <summary>
	/// Number of plane inputs exposed by this node and planes in its output image.
	/// </summary>
	public int PlaneCount
	{
		get => _planeCount;
		set
		{
			var normalized = Math.Clamp(value, 1, MaximumPlaneCount);
			if (_planeCount == normalized && _planeInputs.Count == normalized)
				return;

			_planeCount = normalized;
			SynchronizeInputs();
			SynchronizeWeights();
			DisposeLastResult();
		}
	}

	/// <summary>
	/// Creates a plane-merging node with a three-plane default configuration.
	/// </summary>
	public PlaneMergeNode()
	{
		SynchronizeInputs();
		SynchronizeWeights();
		ImageOutput = AddOutput("Image", typeof(Image), "The multi-plane image created from the input planes.");
	}

	/// <summary>
	/// Sets the intensity multiplier for one input plane.
	/// </summary>
	/// <param name="planeIndex">Zero-based input-plane index.</param>
	/// <param name="weight">Non-negative intensity multiplier.</param>
	public void SetPlaneWeight(int planeIndex, double weight)
	{
		if ((uint)planeIndex >= (uint)_planeWeights.Count)
			throw new ArgumentOutOfRangeException(nameof(planeIndex));

		_planeWeights[planeIndex] = NormalizeWeight(weight);
	}

	/// <inheritdoc/>
	public override void Execute()
	{
		var sources = _planeInputs.Select((input, index) => input.Value as Image
			?? throw new InvalidOperationException($"Plane Merge input 'Plane {index}' must contain an image."))
			.ToArray();

		DisposeLastResult();
		_lastResult = MergePlanes(sources, _planeWeights);
		ImageOutput.Value = _lastResult;
	}

	/// <inheritdoc/>
	public override string CodeVariableName => "mergedImage";

	/// <inheritdoc/>
	public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

	/// <inheritdoc/>
	public override void EmitCode(CodeEmitContext context)
	{
		var inputVariables = _planeInputs.Select(context.ResolveInput).ToArray();
		if (inputVariables.Any(variable => variable is null))
			return;

		var variableName = context.GetUniqueVariable(CodeVariableName);
		var weights = string.Join(", ", _planeWeights.Select(CodeEmitContext.FormatDouble));
		context.Builder.AppendLine($"using var {variableName} = MergePlanes([{string.Join(", ", inputVariables!)}], [{weights}]);");
		context.RegisterOutput(ImageOutput, variableName);
	}

	/// <inheritdoc/>
	public override void EmitHelperMethods(StringBuilder sb)
	{
		sb.AppendLine("static Image MergePlanes(Image[] sources, double[] weights)");
		sb.AppendLine("{");
		sb.AppendLine("    if (sources.Length == 0 || weights.Length != sources.Length)");
		sb.AppendLine("        throw new ArgumentException(\"At least one plane is required.\", nameof(sources));");
		sb.AppendLine("    var first = sources[0];");
		sb.AppendLine("    if (first.Planes.Count != 1)");
		sb.AppendLine("        throw new InvalidOperationException(\"Every Plane Merge input must have exactly one plane.\");");
		sb.AppendLine("    var result = new Image(first.Size, sources.Length, first.Planes[0].DataType);");
		sb.AppendLine("    var width = first.Width;");
		sb.AppendLine("    var height = first.Height;");
		sb.AppendLine("    for (int planeIndex = 0; planeIndex < sources.Length; planeIndex++)");
		sb.AppendLine("    {");
		sb.AppendLine("        var source = sources[planeIndex];");
		sb.AppendLine("        if (source.Planes.Count != 1 || source.Size != first.Size || !source.Planes[0].DataType.Equals(first.Planes[0].DataType))");
		sb.AppendLine("            throw new InvalidOperationException(\"Plane Merge inputs must be single-plane images with matching size and data type.\");");
		sb.AppendLine("        var weight = weights[planeIndex];");
		sb.AppendLine("        if (!double.IsFinite(weight) || weight < 0)");
		sb.AppendLine("            throw new ArgumentOutOfRangeException(nameof(weights));");
		sb.AppendLine("        var dataType = source.Planes[0].DataType;");
		sb.AppendLine("        var applyWeight = weight != 1.0;");
		sb.AppendLine("        if (applyWeight && (!dataType.IsUnsignedInteger || dataType.BytesPerPixel is < 1 or > 2 || dataType.BitsPerPixel is < 1 or > 16))");
		sb.AppendLine("            throw new NotSupportedException(\"Non-unit plane weights require unsigned 8- or 16-bit input planes.\");");
		sb.AppendLine("        var sourceAccess = source.Planes[0].GetLinearAccess();");
		sb.AppendLine("        var targetAccess = result.Planes[planeIndex].GetLinearAccess();");
		sb.AppendLine("        var bytesPerPixel = Math.Max(1, dataType.BytesPerPixel);");
		sb.AppendLine("        var maximum = applyWeight ? (1u << dataType.BitsPerPixel) - 1u : 0u;");
		sb.AppendLine("        var sourceXInc = sourceAccess.XInc.ToInt64();");
		sb.AppendLine("        var sourceYInc = sourceAccess.YInc.ToInt64();");
		sb.AppendLine("        var targetXInc = targetAccess.XInc.ToInt64();");
		sb.AppendLine("        var targetYInc = targetAccess.YInc.ToInt64();");
		sb.AppendLine("        for (int y = 0; y < height; y++)");
		sb.AppendLine("        {");
		sb.AppendLine("            for (int x = 0; x < width; x++)");
		sb.AppendLine("            {");
		sb.AppendLine("                var sourcePixel = sourceAccess.BasePtr + (nint)(y * sourceYInc + x * sourceXInc);");
		sb.AppendLine("                var targetPixel = targetAccess.BasePtr + (nint)(y * targetYInc + x * targetXInc);");
		sb.AppendLine("                if (!applyWeight)");
		sb.AppendLine("                {");
		sb.AppendLine("                    for (int b = 0; b < bytesPerPixel; b++)");
		sb.AppendLine("                        Marshal.WriteByte(targetPixel, b, Marshal.ReadByte(sourcePixel, b));");
		sb.AppendLine("                    continue;");
		sb.AppendLine("                }");
		sb.AppendLine("                var rawValue = dataType.BytesPerPixel == 1 ? Marshal.ReadByte(sourcePixel) : (ushort)Marshal.ReadInt16(sourcePixel);");
		sb.AppendLine("                var weighted = (uint)Math.Clamp(Math.Round(rawValue * weight, MidpointRounding.AwayFromZero), 0, maximum);");
		sb.AppendLine("                if (dataType.BytesPerPixel == 1)");
		sb.AppendLine("                    Marshal.WriteByte(targetPixel, (byte)weighted);");
		sb.AppendLine("                else");
		sb.AppendLine("                    Marshal.WriteInt16(targetPixel, unchecked((short)weighted));");
		sb.AppendLine("            }");
		sb.AppendLine("        }");
		sb.AppendLine("    }");
		sb.AppendLine("    return result;");
		sb.AppendLine("}");
	}

	private void SynchronizeInputs()
	{
		while (_planeInputs.Count > _planeCount)
		{
			var input = _planeInputs[^1];
			RemoveInput(input);
			_planeInputs.RemoveAt(_planeInputs.Count - 1);
		}

		while (_planeInputs.Count < _planeCount)
		{
			var planeIndex = _planeInputs.Count;
			_planeInputs.Add(AddInput(
				$"Plane {planeIndex}",
				typeof(Image),
				$"A single-plane image used as output plane {planeIndex}."));
		}
	}

	private void SynchronizeWeights(IEnumerable<double>? values = null)
	{
		if (values is not null)
		{
			_planeWeights.Clear();
			_planeWeights.AddRange(values.Select(NormalizeWeight));
		}

		if (_planeWeights.Count > _planeCount)
			_planeWeights.RemoveRange(_planeCount, _planeWeights.Count - _planeCount);

		while (_planeWeights.Count < _planeCount)
			_planeWeights.Add(1.0);
	}

	private static unsafe Image MergePlanes(IReadOnlyList<Image> sources, IReadOnlyList<double> weights)
	{
		if (sources.Count == 0 || weights.Count != sources.Count)
			throw new ArgumentException("At least one plane is required.", nameof(sources));

		var first = sources[0];
		if (first.Planes.Count != 1)
			throw new InvalidOperationException("Every Plane Merge input must have exactly one plane.");

		var result = new Image(first.Size, sources.Count, first.Planes[0].DataType);
		var width = first.Width;
		var height = first.Height;
		for (var planeIndex = 0; planeIndex < sources.Count; planeIndex++)
		{
			var source = sources[planeIndex];
			if (source.Planes.Count != 1 || source.Size != first.Size || !source.Planes[0].DataType.Equals(first.Planes[0].DataType))
			{
				result.Dispose();
				throw new InvalidOperationException("Plane Merge inputs must be single-plane images with matching size and data type.");
			}

			CopyPlane(source.Planes[0], result.Planes[planeIndex], width, height, weights[planeIndex]);
		}

		return result;
	}

	private static unsafe void CopyPlane(ImagePlane sourcePlane, ImagePlane targetPlane, int width, int height, double weight)
	{
		if (!double.IsFinite(weight) || weight < 0)
			throw new ArgumentOutOfRangeException(nameof(weight));

		var dataType = sourcePlane.DataType;
		var applyWeight = weight != 1.0;
		if (applyWeight && (!dataType.IsUnsignedInteger || dataType.BytesPerPixel is < 1 or > 2 || dataType.BitsPerPixel is < 1 or > 16))
			throw new NotSupportedException("Non-unit plane weights require unsigned 8- or 16-bit input planes.");

		var sourceAccess = sourcePlane.GetLinearAccess();
		var targetAccess = targetPlane.GetLinearAccess();
		var bytesPerPixel = Math.Max(1, dataType.BytesPerPixel);
		var maximum = applyWeight ? (1u << dataType.BitsPerPixel) - 1u : 0u;
		var sourceBase = (byte*)sourceAccess.BasePtr;
		var targetBase = (byte*)targetAccess.BasePtr;
		var sourceXInc = sourceAccess.XInc.ToInt64();
		var sourceYInc = sourceAccess.YInc.ToInt64();
		var targetXInc = targetAccess.XInc.ToInt64();
		var targetYInc = targetAccess.YInc.ToInt64();

		for (var y = 0; y < height; y++)
		{
			var sourceRow = sourceBase + y * sourceYInc;
			var targetRow = targetBase + y * targetYInc;
			for (var x = 0; x < width; x++)
			{
				var sourcePixel = sourceRow + x * sourceXInc;
				var targetPixel = targetRow + x * targetXInc;
				if (!applyWeight)
				{
					Buffer.MemoryCopy(sourcePixel, targetPixel, bytesPerPixel, bytesPerPixel);
					continue;
				}

				var rawValue = dataType.BytesPerPixel == 1
					? Marshal.ReadByte((nint)sourcePixel)
					: unchecked((ushort)Marshal.ReadInt16((nint)sourcePixel));
				var weighted = (uint)Math.Clamp(Math.Round(rawValue * weight, MidpointRounding.AwayFromZero), 0, maximum);
				if (dataType.BytesPerPixel == 1)
					Marshal.WriteByte((nint)targetPixel, (byte)weighted);
				else
					Marshal.WriteInt16((nint)targetPixel, unchecked((short)weighted));
			}
		}
	}

	private void DisposeLastResult()
	{
		ImageOutput.Value = null;
		_lastResult?.Dispose();
		_lastResult = null;
	}

	private static double ParseWeight(string value)
		=> double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? NormalizeWeight(parsed)
			: 1.0;

	private static double NormalizeWeight(double value)
		=> double.IsFinite(value) ? Math.Max(0, value) : 1.0;
}
