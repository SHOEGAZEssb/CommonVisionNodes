namespace CommonVisionNodes.Benchmarks;

public readonly record struct PreviewSize(int Width, int Height)
{
	public int Stride => checked(Width * 4);

	public int ByteCount => checked(Stride * Height);

	public override string ToString() => $"{Width}x{Height}";
}
