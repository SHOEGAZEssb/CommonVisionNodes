using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes.Runtime;

/// <summary>
/// Specifies whether Plane Split outputs own copied pixels or link to the source planes.
/// </summary>
public enum PlaneSplitMode
{
	/// <summary>
	/// Allocate and copy every plane into an independent image.
	/// </summary>
	Copy,

	/// <summary>
	/// Create mapped images that share their source planes' pixels.
	/// </summary>
	Link
}

/// <summary>
/// Exposes every configured plane of an image as its own single-plane image output.
/// </summary>
public sealed class PlaneSplitNode : Node
{
	/// <summary>
	/// Largest number of individual plane outputs supported by this node.
	/// </summary>
	public const int MaximumPlaneCount = 16;

	private readonly List<Port> _planeOutputs = [];
	private Image?[] _lastResults = [];
	private int _planeCount = 3;

	/// <summary>
	/// Input port that receives the multi-plane source image.
	/// </summary>
	public Port ImageInput { get; }

	/// <summary>
	/// Individual image outputs, named <c>Plane 0</c>, <c>Plane 1</c>, and so on.
	/// </summary>
	public IReadOnlyList<Port> PlaneOutputs => _planeOutputs;

	/// <summary>
	/// Whether plane outputs copy or link their source pixels.
	/// </summary>
	public PlaneSplitMode Mode { get; set; } = PlaneSplitMode.Copy;

	/// <summary>
	/// Number of plane outputs exposed by this node.
	/// The input image must have the same number of planes at execution time.
	/// </summary>
	public int PlaneCount
	{
		get => _planeCount;
		set
		{
			var normalized = Math.Clamp(value, 1, MaximumPlaneCount);
			if (_planeCount == normalized && _planeOutputs.Count == normalized)
				return;

			_planeCount = normalized;
			SynchronizeOutputs();
		}
	}

	/// <summary>
	/// Creates a plane-splitting node with a three-plane default configuration.
	/// </summary>
	public PlaneSplitNode()
	{
		ImageInput = AddInput("Image", typeof(Image), "The multi-plane image to split.");
		SynchronizeOutputs();
	}

	/// <inheritdoc/>
	public override void Execute()
	{
		var source = (Image)ImageInput.Value!;
		if (source.Planes.Count != PlaneCount)
		{
			throw new InvalidOperationException(
				$"Plane Split is configured for {PlaneCount} plane(s), but the input image has {source.Planes.Count}.");
		}

		DisposeLastResults();
		_lastResults = new Image[PlaneCount];

		for (var planeIndex = 0; planeIndex < PlaneCount; planeIndex++)
		{
			var result = CreatePlaneImage(source, planeIndex);
			_lastResults[planeIndex] = result;
			_planeOutputs[planeIndex].Value = result;
		}
	}

	/// <inheritdoc/>
	public override string CodeVariableName => "splitPlane";

	/// <inheritdoc/>
	public override IReadOnlyList<string> RequiredUsings => Mode == PlaneSplitMode.Copy
		? ["System.Runtime.InteropServices"]
		: [];

	/// <inheritdoc/>
	public override void EmitCode(CodeEmitContext context)
	{
		var inputVar = context.ResolveInput(ImageInput);
		if (inputVar is null)
			return;

		for (var planeIndex = 0; planeIndex < PlaneCount; planeIndex++)
		{
			var variableName = context.GetUniqueVariable($"{CodeVariableName}{planeIndex}");
			var expression = Mode == PlaneSplitMode.Link
				? $"Image.FromPlanes(MappingOption.LinkPixels, {inputVar}.Planes[{planeIndex}])"
				: $"SplitPlane({inputVar}, {planeIndex})";
			context.Builder.AppendLine($"using var {variableName} = {expression};");
			context.RegisterOutput(_planeOutputs[planeIndex], variableName);
		}
	}

	/// <inheritdoc/>
	public override void EmitHelperMethods(StringBuilder sb)
	{
		if (Mode == PlaneSplitMode.Link)
			return;

		sb.AppendLine("static Image SplitPlane(Image source, int planeIndex)");
		sb.AppendLine("{");
		sb.AppendLine("    if ((uint)planeIndex >= (uint)source.Planes.Count)");
		sb.AppendLine("        throw new ArgumentOutOfRangeException(nameof(planeIndex));");
		sb.AppendLine("    var sourcePlane = source.Planes[planeIndex];");
		sb.AppendLine("    var result = new Image(source.Size, 1, sourcePlane.DataType);");
		sb.AppendLine("    var sourceAccess = sourcePlane.GetLinearAccess();");
		sb.AppendLine("    var targetAccess = result.Planes[0].GetLinearAccess();");
		sb.AppendLine("    var bytesPerPixel = Math.Max(1, sourcePlane.DataType.BytesPerPixel);");
		sb.AppendLine("    var width = source.Width;");
		sb.AppendLine("    var height = source.Height;");
		sb.AppendLine("    var sourceXInc = sourceAccess.XInc.ToInt64();");
		sb.AppendLine("    var sourceYInc = sourceAccess.YInc.ToInt64();");
		sb.AppendLine("    var targetXInc = targetAccess.XInc.ToInt64();");
		sb.AppendLine("    var targetYInc = targetAccess.YInc.ToInt64();");
		sb.AppendLine("    for (int y = 0; y < height; y++)");
		sb.AppendLine("    {");
		sb.AppendLine("        for (int x = 0; x < width; x++)");
		sb.AppendLine("        {");
		sb.AppendLine("            var sourcePixel = sourceAccess.BasePtr + (nint)(y * sourceYInc + x * sourceXInc);");
		sb.AppendLine("            var targetPixel = targetAccess.BasePtr + (nint)(y * targetYInc + x * targetXInc);");
		sb.AppendLine("            for (int b = 0; b < bytesPerPixel; b++)");
		sb.AppendLine("                Marshal.WriteByte(targetPixel, b, Marshal.ReadByte(sourcePixel, b));");
		sb.AppendLine("        }");
		sb.AppendLine("    }");
		sb.AppendLine("    return result;");
		sb.AppendLine("}");
	}

	private void SynchronizeOutputs()
	{
		DisposeLastResults();

		while (_planeOutputs.Count > _planeCount)
		{
			var output = _planeOutputs[^1];
			RemoveOutput(output);
			_planeOutputs.RemoveAt(_planeOutputs.Count - 1);
		}

		while (_planeOutputs.Count < _planeCount)
		{
			var planeIndex = _planeOutputs.Count;
			_planeOutputs.Add(AddOutput(
				$"Plane {planeIndex}",
				typeof(Image),
				$"A single-plane image sourced from input plane {planeIndex}."));
		}
	}

	private Image CreatePlaneImage(Image source, int planeIndex)
		=> Mode == PlaneSplitMode.Link
			? Image.FromPlanes(MappingOption.LinkPixels, source.Planes[planeIndex])
			: CopyPlane(source, planeIndex);

	private static unsafe Image CopyPlane(Image source, int planeIndex)
	{
		var sourcePlane = source.Planes[planeIndex];
		var result = new Image(source.Size, 1, sourcePlane.DataType);
		var sourceAccess = sourcePlane.GetLinearAccess();
		var targetAccess = result.Planes[0].GetLinearAccess();
		var bytesPerPixel = Math.Max(1, sourcePlane.DataType.BytesPerPixel);

		var sourceBase = (byte*)sourceAccess.BasePtr;
		var targetBase = (byte*)targetAccess.BasePtr;
		var width = source.Width;
		var height = source.Height;
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
				Buffer.MemoryCopy(sourcePixel, targetPixel, bytesPerPixel, bytesPerPixel);
			}
		}

		return result;
	}

	private void DisposeLastResults()
	{
		foreach (var output in _planeOutputs)
			output.Value = null;

		foreach (var result in _lastResults)
			result?.Dispose();
		_lastResults = [];
	}
}
