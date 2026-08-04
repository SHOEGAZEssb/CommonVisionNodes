using BenchmarkDotNet.Attributes;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Execution;
using Stemmer.Cvb;

namespace CommonVisionNodes.Benchmarks;

/// <summary>
/// Measures backend preview generation for the high-resolution camera scenario.
/// </summary>
[MemoryDiagnoser]
public class PreviewGenerationBenchmarks
{
	private const int SourceWidth = 2560;
	private const int SourceHeight = 2048;

	private BinaryImageBufferCache _imageBufferCache = null!;
	private ImageGeneratorNode _imageGenerator = null!;

	[Params(640)]
	public int PreviewMaxDimension { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		_imageGenerator = new ImageGeneratorNode
		{
			Width = SourceWidth,
			Height = SourceHeight,
			Pattern = TestPattern.GradientH
		};
		_imageGenerator.Execute();

		_imageBufferCache = new BinaryImageBufferCache();
		RuntimePreviewFactory.CreatePreviewMessage(
			"camera-preview",
			_imageGenerator,
			PreviewMaxDimension,
			_imageBufferCache);
		RuntimePreviewFactory.CreatePreviewMessage(
			"camera-preview",
			_imageGenerator,
			PreviewMaxDimension,
			_imageBufferCache);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		if (_imageGenerator.ImageOutput.Value is Image image)
			image.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Backend preview: allocate output buffer")]
	public ExecutionMessageDto GenerateAllocatingPreview()
		=> RuntimePreviewFactory.CreatePreviewMessage(
			"camera-preview",
			_imageGenerator,
			PreviewMaxDimension) ?? throw new InvalidOperationException("Preview generation failed.");

	[Benchmark(Description = "Backend preview: reuse output buffer")]
	public ExecutionMessageDto GenerateReusedPreview()
		=> RuntimePreviewFactory.CreatePreviewMessage(
			"camera-preview",
			_imageGenerator,
			PreviewMaxDimension,
			_imageBufferCache) ?? throw new InvalidOperationException("Preview generation failed.");
}
