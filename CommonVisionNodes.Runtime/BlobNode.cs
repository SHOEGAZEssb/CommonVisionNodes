using System.Text;
using Stemmer.Cvb;

namespace CommonVisionNodes
{
    /// <summary>
    /// Bounding rectangle of a detected blob, suitable for passing to downstream
    /// nodes (e.g. classification or sub-image extraction).
    /// </summary>
    /// <param name="X">X origin of the bounding box in pixels.</param>
    /// <param name="Y">Y origin of the bounding box in pixels.</param>
    /// <param name="Width">Width of the bounding box in pixels.</param>
    /// <param name="Height">Height of the bounding box in pixels.</param>
    public readonly record struct BlobRect(int X, int Y, int Width, int Height);

    /// <summary>
    /// Describes a single blob (connected component) found in a binary image.
    /// </summary>
    public sealed class BlobInfo
    {
        /// <summary>
        /// Label index of this blob (1-based).
        /// </summary>
        public int Label { get; init; }

        /// <summary>
        /// Number of pixels belonging to this blob.
        /// </summary>
        public int Area { get; init; }

        /// <summary>
        /// X coordinate of the blob centroid.
        /// </summary>
        public double CentroidX { get; init; }

        /// <summary>
        /// Y coordinate of the blob centroid.
        /// </summary>
        public double CentroidY { get; init; }

        /// <summary>
        /// X origin of the bounding box.
        /// </summary>
        public int BoundsX { get; init; }

        /// <summary>
        /// Y origin of the bounding box.
        /// </summary>
        public int BoundsY { get; init; }

        /// <summary>
        /// Width of the bounding box.
        /// </summary>
        public int BoundsWidth { get; init; }

        /// <summary>
        /// Height of the bounding box.
        /// </summary>
        public int BoundsHeight { get; init; }
    }

    /// <summary>
    /// Performs connected-component (blob) analysis on a binary image.
    /// Pixels with value ≥ <see cref="ForegroundThreshold"/> are treated as foreground.
    /// Blobs smaller than <see cref="MinArea"/> are discarded.
    /// </summary>
    public sealed class BlobNode : Node
    {
        /// <summary>
        /// Input port that receives the binary (or binarized) image.
        /// </summary>
        public Port ImageInput { get; }

        /// <summary>
        /// Output port that passes the source image through.
        /// </summary>
        public Port ImageOutput { get; }

        /// <summary>
        /// Output port that provides the bounding rectangles of detected blobs.
        /// </summary>
        public Port BlobsOutput { get; }

        /// <summary>
        /// Number of blobs found after the last execution.
        /// </summary>
        public int BlobCount { get; private set; }

        /// <summary>
        /// Details for each detected blob, sorted by area (largest first).
        /// </summary>
        public IReadOnlyList<BlobInfo> Blobs { get; private set; } = [];

        /// <summary>
        /// Minimum pixel intensity to be considered foreground.
        /// </summary>
        public int ForegroundThreshold { get; set; } = 128;

        /// <summary>
        /// Blobs with fewer pixels than this value are ignored.
        /// </summary>
        public int MinArea { get; set; } = 1;

        /// <summary>
        /// Blobs with more pixels than this value are ignored.
        /// A value of 0 means no upper limit.
        /// </summary>
        public int MaxArea { get; set; }

        /// <summary>
        /// Maximum number of blobs to return (largest first).
        /// A value of 0 means no limit.
        /// </summary>
        public int MaxBlobCount { get; set; }

        /// <summary>
        /// When <c>true</c>, pixels <em>below</em> the threshold are treated as foreground.
        /// </summary>
        public bool InvertForeground { get; set; }

        /// <summary>
        /// When <c>true</c>, 8-connectivity (including diagonals) is used instead of 4-connectivity.
        /// </summary>
        public bool Use8Connectivity { get; set; }

        public BlobNode()
        {
            ImageInput = AddInput("Image", typeof(Image), "The binary image to analyze for connected components.");
            ImageOutput = AddOutput("Image", typeof(Image), "The source image passed through unchanged.");
            BlobsOutput = AddOutput("Blobs", typeof(IReadOnlyList<BlobRect>), "Bounding rectangles of the detected blobs, sorted by area (largest first).");
        }

        /// <inheritdoc/>
        public override void Execute()
        {
            var source = (Image)ImageInput.Value!;
            int width = source.Width;
            int height = source.Height;
            byte threshold = (byte)Math.Clamp(ForegroundThreshold, 0, 255);

            bool invert = InvertForeground;

            // Read first plane into a bool grid
            bool[,] fg = new bool[height, width];
            var srcAccess = source.Planes[0].GetLinearAccess();
            unsafe
            {
                byte* srcBase = (byte*)srcAccess.BasePtr;
                long srcYInc = srcAccess.YInc;
                long srcXInc = srcAccess.XInc;
                for (int y = 0; y < height; y++)
                {
                    byte* row = srcBase + y * srcYInc;
                    for (int x = 0; x < width; x++)
                    {
                        byte val = *(row + x * srcXInc);
                        fg[y, x] = invert ? val < threshold : val >= threshold;
                    }
                }
            }

            bool use8 = Use8Connectivity;

            // Two-pass connected-component labeling
            int[,] labels = new int[height, width];
            int nextLabel = 1;
            int[] parent = new int[width * height + 1]; // union-find

            int Find(int a)
            {
                while (parent[a] != a)
                {
                    parent[a] = parent[parent[a]];
                    a = parent[a];
                }
                return a;
            }

            void Union(int a, int b)
            {
                a = Find(a);
                b = Find(b);
                if (a != b)
                    parent[Math.Max(a, b)] = Math.Min(a, b);
            }

            // First pass
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!fg[y, x]) continue;

                    int above = y > 0 && fg[y - 1, x] ? labels[y - 1, x] : 0;
                    int left = x > 0 && fg[y, x - 1] ? labels[y, x - 1] : 0;
                    int aboveLeft = use8 && y > 0 && x > 0 && fg[y - 1, x - 1] ? labels[y - 1, x - 1] : 0;
                    int aboveRight = use8 && y > 0 && x < width - 1 && fg[y - 1, x + 1] ? labels[y - 1, x + 1] : 0;

                    // Collect all non-zero neighbor labels
                    int minLabel = 0;
                    foreach (int n in (ReadOnlySpan<int>)[above, left, aboveLeft, aboveRight])
                    {
                        if (n != 0)
                            minLabel = minLabel == 0 ? n : Math.Min(minLabel, n);
                    }

                    if (minLabel == 0)
                    {
                        int lbl = nextLabel++;
                        parent[lbl] = lbl;
                        labels[y, x] = lbl;
                    }
                    else
                    {
                        labels[y, x] = minLabel;
                        foreach (int n in (ReadOnlySpan<int>)[above, left, aboveLeft, aboveRight])
                        {
                            if (n != 0 && n != minLabel)
                                Union(n, minLabel);
                        }
                    }
                }
            }

            // Second pass — flatten labels and collect stats
            var stats = new Dictionary<int, (long sumX, long sumY, int area, int minX, int minY, int maxX, int maxY)>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (labels[y, x] == 0) continue;

                    int root = Find(labels[y, x]);
                    labels[y, x] = root;

                    if (!stats.TryGetValue(root, out var s))
                        s = (0, 0, 0, x, y, x, y);

                    stats[root] = (
                        s.sumX + x,
                        s.sumY + y,
                        s.area + 1,
                        Math.Min(s.minX, x),
                        Math.Min(s.minY, y),
                        Math.Max(s.maxX, x),
                        Math.Max(s.maxY, y)
                    );
                }
            }

            // Build blob list
            int maxArea = MaxArea;
            var blobs = new List<BlobInfo>();
            int blobIndex = 0;
            foreach (var (label, s) in stats)
            {
                if (s.area < MinArea) continue;
                if (maxArea > 0 && s.area > maxArea) continue;
                blobIndex++;
                blobs.Add(new BlobInfo
                {
                    Label = blobIndex,
                    Area = s.area,
                    CentroidX = (double)s.sumX / s.area,
                    CentroidY = (double)s.sumY / s.area,
                    BoundsX = s.minX,
                    BoundsY = s.minY,
                    BoundsWidth = s.maxX - s.minX + 1,
                    BoundsHeight = s.maxY - s.minY + 1
                });
            }

            blobs.Sort((a, b) => b.Area.CompareTo(a.Area));

            int maxCount = MaxBlobCount;
            if (maxCount > 0 && blobs.Count > maxCount)
                blobs.RemoveRange(maxCount, blobs.Count - maxCount);

            Blobs = blobs;
            BlobCount = blobs.Count;

            ImageOutput.Value = source;
            BlobsOutput.Value = blobs
                .Select(b => new BlobRect(b.BoundsX, b.BoundsY, b.BoundsWidth, b.BoundsHeight))
                .ToList();
        }

        // Code generation

        /// <inheritdoc/>
        public override string CodeVariableName => "blobs";

        /// <inheritdoc/>
        public override IReadOnlyList<string> RequiredUsings => ["System.Runtime.InteropServices"];

        /// <inheritdoc/>
        public override void EmitCode(CodeEmitContext context)
        {
            var inputVar = context.ResolveInput(ImageInput);
            if (inputVar == null) return;

            var varName = context.GetUniqueVariable(CodeVariableName);
            var sb = context.Builder;
            sb.AppendLine($"// Blob analysis (threshold: {ForegroundThreshold}, invert: {InvertForeground}, connectivity: {(Use8Connectivity ? 8 : 4)}, minArea: {MinArea}, maxArea: {MaxArea}, maxCount: {MaxBlobCount})");
            sb.AppendLine($"var {varName} = FindBlobRects({inputVar}, {ForegroundThreshold}, {InvertForeground.ToString().ToLowerInvariant()}, {Use8Connectivity.ToString().ToLowerInvariant()}, {MinArea}, {MaxArea}, {MaxBlobCount});");
            sb.AppendLine($"Console.WriteLine($\"Blobs found: {{{varName}.Count}}\");");
            sb.AppendLine($"foreach (var r in {varName})");
            sb.AppendLine($"    Console.WriteLine($\"  Rect=({{r.X}},{{r.Y}},{{r.Width}},{{r.Height}})\");");
            context.RegisterOutput(ImageOutput, inputVar);
            context.RegisterOutput(BlobsOutput, varName);
        }

        /// <inheritdoc/>
        public override void EmitHelperMethods(StringBuilder sb)
        {
            sb.AppendLine("readonly record struct BlobRect(int X, int Y, int Width, int Height);");
            sb.AppendLine();
            sb.AppendLine("static List<BlobRect> FindBlobRects(Image source, int fgThreshold, bool invert, bool use8, int minArea, int maxArea, int maxCount)");
            sb.AppendLine("{");
            sb.AppendLine("    int width = source.Width, height = source.Height;");
            sb.AppendLine("    byte threshold = (byte)Math.Clamp(fgThreshold, 0, 255);");
            sb.AppendLine("    var srcAccess = source.Planes[0].GetLinearAccess();");
            sb.AppendLine("    bool[,] fg = new bool[height, width];");
            sb.AppendLine("    for (int y = 0; y < height; y++)");
            sb.AppendLine("        for (int x = 0; x < width; x++)");
            sb.AppendLine("        {");
            sb.AppendLine("            var ptr = srcAccess.BasePtr + (nint)(y * srcAccess.YInc + x * srcAccess.XInc);");
            sb.AppendLine("            byte val = Marshal.ReadByte(ptr);");
            sb.AppendLine("            fg[y, x] = invert ? val < threshold : val >= threshold;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("    int[,] labels = new int[height, width];");
            sb.AppendLine("    int nextLabel = 1;");
            sb.AppendLine("    int[] parent = new int[width * height + 1];");
            sb.AppendLine("    int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }");
            sb.AppendLine("    void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b); }");
            sb.AppendLine();
            sb.AppendLine("    for (int y = 0; y < height; y++)");
            sb.AppendLine("        for (int x = 0; x < width; x++)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (!fg[y, x]) continue;");
            sb.AppendLine("            int above = y > 0 && fg[y - 1, x] ? labels[y - 1, x] : 0;");
            sb.AppendLine("            int left = x > 0 && fg[y, x - 1] ? labels[y, x - 1] : 0;");
            sb.AppendLine("            int aboveLeft = use8 && y > 0 && x > 0 && fg[y - 1, x - 1] ? labels[y - 1, x - 1] : 0;");
            sb.AppendLine("            int aboveRight = use8 && y > 0 && x < width - 1 && fg[y - 1, x + 1] ? labels[y - 1, x + 1] : 0;");
            sb.AppendLine("            int minLabel = 0;");
            sb.AppendLine("            foreach (int n in new[] { above, left, aboveLeft, aboveRight })");
            sb.AppendLine("                if (n != 0) minLabel = minLabel == 0 ? n : Math.Min(minLabel, n);");
            sb.AppendLine("            if (minLabel == 0) { int lbl = nextLabel++; parent[lbl] = lbl; labels[y, x] = lbl; }");
            sb.AppendLine("            else { labels[y, x] = minLabel; foreach (int n in new[] { above, left, aboveLeft, aboveRight }) if (n != 0 && n != minLabel) Union(n, minLabel); }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("    var stats = new Dictionary<int, (long sumX, long sumY, int area, int minX, int minY, int maxX, int maxY)>();");
            sb.AppendLine("    for (int y = 0; y < height; y++)");
            sb.AppendLine("        for (int x = 0; x < width; x++)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (labels[y, x] == 0) continue;");
            sb.AppendLine("            int root = Find(labels[y, x]); labels[y, x] = root;");
            sb.AppendLine("            if (!stats.TryGetValue(root, out var s)) s = (0, 0, 0, x, y, x, y);");
            sb.AppendLine("            stats[root] = (s.sumX + x, s.sumY + y, s.area + 1, Math.Min(s.minX, x), Math.Min(s.minY, y), Math.Max(s.maxX, x), Math.Max(s.maxY, y));");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("    var result = new List<(int area, BlobRect rect)>();");
            sb.AppendLine("    foreach (var (label, s) in stats)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (s.area < minArea) continue;");
            sb.AppendLine("        if (maxArea > 0 && s.area > maxArea) continue;");
            sb.AppendLine("        result.Add((s.area, new BlobRect(s.minX, s.minY, s.maxX - s.minX + 1, s.maxY - s.minY + 1)));");
            sb.AppendLine("    }");
            sb.AppendLine("    result.Sort((a, b) => b.area.CompareTo(a.area));");
            sb.AppendLine("    if (maxCount > 0 && result.Count > maxCount) result.RemoveRange(maxCount, result.Count - maxCount);");
            sb.AppendLine("    return result.Select(r => r.rect).ToList();");
            sb.AppendLine("}");
        }
    }
}
