using CommonVisionNodes.Contracts;
using Stemmer.Cvb;

namespace CommonVisionNodes.Runtime.Execution;

/// <summary>
/// Creates execution preview messages from runtime node state.
/// </summary>
public sealed class RuntimePreviewFactory
{
    private const int BgraBytesPerPixel = 4;

    /// <summary>
    /// Creates the appropriate preview message for a runtime node.
    /// </summary>
    /// <param name="nodeId">Serialized graph node id.</param>
    /// <param name="node">Runtime node instance.</param>
    /// <param name="previewImageMaxDimension">Maximum preview long edge, or 0 to keep full resolution.</param>
    /// <returns>A preview message, or <c>null</c> when the node has no preview data.</returns>
    public static ExecutionMessageDto? CreatePreviewMessage(string nodeId, Node node, int previewImageMaxDimension)
    {
        return node switch
        {
            ImageNode imageNode => CreateImagePreviewMessage(nodeId, imageNode.CachedImage, previewImageMaxDimension),
            SaveImageNode saveImageNode => CreateImagePreviewMessage(nodeId, saveImageNode.ImageInput.Value as Image, previewImageMaxDimension),
            GevServerNode gevServerNode => CreateImagePreviewMessage(nodeId, gevServerNode.ImageInput.Value as Image, previewImageMaxDimension),
            DeviceNode deviceNode => CreateImagePreviewMessage(nodeId, deviceNode.ImageOutput.Value as Image, previewImageMaxDimension),
            BinarizeNode binarizeNode => CreateImagePreviewMessage(nodeId, binarizeNode.ImageOutput.Value as Image, previewImageMaxDimension),
            SubImageNode subImageNode => CreateImagePreviewMessage(nodeId, subImageNode.ImageOutput.Value as Image, previewImageMaxDimension),
            MatrixTransformNode transformNode => CreateImagePreviewMessage(nodeId, transformNode.ImageOutput.Value as Image, previewImageMaxDimension),
            ImageGeneratorNode generatorNode => CreateImagePreviewMessage(nodeId, generatorNode.ImageOutput.Value as Image, previewImageMaxDimension),
            FilterNode filterNode => CreateImagePreviewMessage(nodeId, filterNode.ImageOutput.Value as Image, previewImageMaxDimension),
            MorphologyNode morphologyNode => CreateImagePreviewMessage(nodeId, morphologyNode.ImageOutput.Value as Image, previewImageMaxDimension),
            NormalizeNode normalizeNode => CreateImagePreviewMessage(nodeId, normalizeNode.ImageOutput.Value as Image, previewImageMaxDimension),
            CSharpNode csharpNode => CreateImagePreviewMessage(nodeId, csharpNode.ImageOutput.Value as Image, previewImageMaxDimension),
            HistogramNode histogramNode => CreateHistogramPreviewMessage(nodeId, histogramNode),
            BlobNode blobNode => CreateBlobPreviewMessage(nodeId, blobNode, previewImageMaxDimension),
            PolimagoClassifyNode classifyNode => CreateClassificationPreviewMessage(nodeId, classifyNode, previewImageMaxDimension),
            CodeReaderNode codeReaderNode => CreateCodeReaderPreviewMessage(nodeId, codeReaderNode, previewImageMaxDimension),
            GenericVisualizerNode genericVisualizerNode => CreateGenericPreviewMessage(nodeId, genericVisualizerNode.LastValue, previewImageMaxDimension),
            _ => null
        };
    }

    private static ExecutionMessageDto? CreateImagePreviewMessage(string nodeId, Image? image, int previewImageMaxDimension)
    {
        var preview = CreateImagePreview(nodeId, image, previewImageMaxDimension);
        return preview is null
            ? null
            : new ExecutionMessageDto
            {
                MessageType = ExecutionMessageTypeDto.ImagePreview,
                ImagePreview = preview
            };
    }

    private static ExecutionMessageDto CreateHistogramPreviewMessage(string nodeId, HistogramNode node)
        => new()
        {
            MessageType = ExecutionMessageTypeDto.HistogramPreview,
            HistogramPreview = new HistogramPreviewDto
            {
                NodeId = nodeId,
                Bins = [.. node.Bins],
                Mean = node.Mean,
                StdDev = node.StdDev,
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

    private static ExecutionMessageDto CreateBlobPreviewMessage(string nodeId, BlobNode node, int previewImageMaxDimension)
        => new()
        {
            MessageType = ExecutionMessageTypeDto.BlobPreview,
            BlobPreview = new BlobPreviewDto
            {
                NodeId = nodeId,
                Image = CreateImagePreview(nodeId, node.ImageOutput.Value as Image, previewImageMaxDimension),
                Blobs = [.. node.Blobs.Select(blob => new BlobInfoDto
                {
                    Label = blob.Label,
                    Area = blob.Area,
                    CentroidX = blob.CentroidX,
                    CentroidY = blob.CentroidY,
                    BoundsX = blob.BoundsX,
                    BoundsY = blob.BoundsY,
                    BoundsWidth = blob.BoundsWidth,
                    BoundsHeight = blob.BoundsHeight
                })],
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

    private static ExecutionMessageDto CreateClassificationPreviewMessage(string nodeId, PolimagoClassifyNode node, int previewImageMaxDimension)
        => new()
        {
            MessageType = ExecutionMessageTypeDto.ClassificationPreview,
            ClassificationPreview = new ClassificationPreviewDto
            {
                NodeId = nodeId,
                Image = CreateImagePreview(nodeId, node.ImageOutput.Value as Image, previewImageMaxDimension),
                Results = [.. node.Results.Select(result => new ClassificationResultDto
                {
                    BlobIndex = result.BlobIndex,
                    ClassName = result.ClassName,
                    Quality = result.Quality,
                    X = result.X,
                    Y = result.Y
                })],
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

    private static ExecutionMessageDto CreateCodeReaderPreviewMessage(string nodeId, CodeReaderNode node, int previewImageMaxDimension)
        => new()
        {
            MessageType = ExecutionMessageTypeDto.CodeReaderPreview,
            CodeReaderPreview = new CodeReaderPreviewDto
            {
                NodeId = nodeId,
                Image = CreateImagePreview(nodeId, node.ImageOutput.Value as Image, previewImageMaxDimension),
                Results = [.. node.Results.Select(result => new CodeReaderResultDto
                {
                    Index = result.Index,
                    Data = result.Data,
                    Symbology = result.Symbology,
                    DecodeStatus = result.DecodeStatus,
                    CenterX = result.CenterX,
                    CenterY = result.CenterY,
                    Corners = [.. result.Corners.Select(corner => new CodeReaderPointDto
                    {
                        X = corner.X,
                        Y = corner.Y
                    })],
                    Quality = result.Quality
                })],
                TimeLimitReached = node.TimeLimitReached,
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

    private static ExecutionMessageDto? CreateGenericPreviewMessage(string nodeId, object? value, int previewImageMaxDimension)
    {
        return value switch
        {
            Image image => CreateImagePreviewMessage(nodeId, image, previewImageMaxDimension),
            IReadOnlyList<BlobInfo> blobs => CreateTextPreviewMessage(nodeId, "BlobInfo[]", string.Join(Environment.NewLine, blobs.Select(blob =>
                $"#{blob.Label} area={blob.Area} center=({blob.CentroidX:F1},{blob.CentroidY:F1}) bounds=({blob.BoundsX},{blob.BoundsY}) {blob.BoundsWidth}x{blob.BoundsHeight}"))),
            IReadOnlyList<BlobRect> rects => CreateTextPreviewMessage(nodeId, "BlobRect[]", string.Join(Environment.NewLine, rects.Select((rect, index) =>
                $"#{index + 1} ({rect.X},{rect.Y}) {rect.Width}x{rect.Height}"))),
            IReadOnlyList<PolimagoClassifyResultItem> results => CreateTextPreviewMessage(nodeId, "Classification[]", string.Join(Environment.NewLine, results.Select(result =>
                $"{(result.BlobIndex >= 0 ? $"#{result.BlobIndex}" : "image")} {result.ClassName} q={result.Quality:F3} ({result.X:F0},{result.Y:F0})"))),
            IReadOnlyList<CodeReaderResultItem> results => CreateTextPreviewMessage(nodeId, "CodeReader[]", CodeReaderNode.FormatResultsForPreview(results, timeLimitReached: false)),
            null => CreateTextPreviewMessage(nodeId, "Empty", "No data"),
            _ => CreateTextPreviewMessage(nodeId, value.GetType().Name, value.ToString() ?? value.GetType().Name)
        };
    }

    private static ExecutionMessageDto CreateTextPreviewMessage(string nodeId, string typeDescription, string displayText)
        => new()
        {
            MessageType = ExecutionMessageTypeDto.TextPreview,
            TextPreview = new TextPreviewDto
            {
                NodeId = nodeId,
                TypeDescription = typeDescription,
                DisplayText = displayText,
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

    private static ImagePreviewDto? CreateImagePreview(string nodeId, Image? image, int previewImageMaxDimension)
    {
        if (image is null || image.IsDisposed)
            return null;

        var rawPreview = CreateBgra32Preview(nodeId, image, previewImageMaxDimension);
        if (rawPreview is not null)
            return rawPreview;

        // Fallback through CVB's image writer for formats the lightweight BGRA path cannot
        // represent. The temp file is less elegant, but it delegates all format details to CVB.
        var tempPath = Path.Combine(Path.GetTempPath(), $"cvn-preview-{Guid.NewGuid():N}.png");
        try
        {
            using var previewImage = CreateScaledPreviewImage(image, previewImageMaxDimension);
            (previewImage ?? image).Save(tempPath);
            var bytes = File.ReadAllBytes(tempPath);
            var bitsPerPixel = image.Planes.Count > 0 ? image.Planes[0].DataType.BitsPerPixel : 0;
            var pixelFormat = image.Planes.Count == 1
                ? $"Mono {bitsPerPixel}bpp"
                : $"{image.Planes.Count}ch {bitsPerPixel}bpp";

            return new ImagePreviewDto
            {
                NodeId = nodeId,
                BinaryData = bytes,
                Width = image.Width,
                Height = image.Height,
                PreviewWidth = previewImage?.Width ?? image.Width,
                PreviewHeight = previewImage?.Height ?? image.Height,
                Stride = 0,
                PixelFormat = pixelFormat,
                TimestampUtc = DateTimeOffset.UtcNow
            };
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Ignore preview temp-file cleanup failures.
            }
        }
    }

    private static ImagePreviewDto? CreateBgra32Preview(string nodeId, Image image, int previewImageMaxDimension)
    {
        if (!TryGetBgra32Source(image, out var source))
            return null;

        // Uno can display BGRA32 directly. Keeping the preview in this raw format avoids
        // a PNG encode/decode round-trip for the common mono and RGB preview cases.
        var previewSize = GetPreviewSize(image, previewImageMaxDimension);
        var stride = checked(previewSize.Width * BgraBytesPerPixel);
        var bytes = new byte[checked(stride * previewSize.Height)];

        CopyBgra32(image, source, bytes, stride, previewSize.Width, previewSize.Height);

        return new ImagePreviewDto
        {
            NodeId = nodeId,
            MediaType = "application/x-bgra32",
            Encoding = ImagePreviewEncodingDto.Bgra32,
            BinaryData = bytes,
            Width = image.Width,
            Height = image.Height,
            PreviewWidth = previewSize.Width,
            PreviewHeight = previewSize.Height,
            Stride = stride,
            PixelFormat = $"{source.PixelFormat} -> BGRA32",
            TimestampUtc = DateTimeOffset.UtcNow
        };
    }

    private static bool TryGetBgra32Source(Image image, out Bgra32Source source)
    {
        source = default;

        if (image.Planes.Count == 1)
        {
            var dataType = image.Planes[0].DataType;
            if (!IsSupportedPreviewDataType(dataType))
                return false;

            source = Bgra32Source.CreateMono(0, dataType.BitsPerPixel);
            return true;
        }

        if (image.Planes.Count is 3 or 4 &&
            image.ColorModel is ColorModel.RGB or ColorModel.RGBGuess)
        {
            for (var planeIndex = 0; planeIndex < image.Planes.Count; planeIndex++)
            {
                if (!IsSupportedPreviewDataType(image.Planes[planeIndex].DataType))
                    return false;
            }

            source = Bgra32Source.CreateRgb(image.Planes.Count == 4 ? 3 : -1, image.Planes[0].DataType.BitsPerPixel);
            return true;
        }

        return false;
    }

    private static bool IsSupportedPreviewDataType(DataType dataType)
        => dataType.IsUnsignedInteger &&
           dataType.BytesPerPixel is 1 or 2 &&
           dataType.BitsPerPixel is > 0 and <= 16;

    private static unsafe void CopyBgra32(Image image, Bgra32Source source, byte[] destination, int stride, int previewWidth, int previewHeight)
    {
        var red = new PreviewPlaneAccess(image.Planes[source.RedPlaneIndex]);
        var green = new PreviewPlaneAccess(image.Planes[source.GreenPlaneIndex]);
        var blue = new PreviewPlaneAccess(image.Planes[source.BluePlaneIndex]);
        var alpha = source.AlphaPlaneIndex >= 0
            ? new PreviewPlaneAccess(image.Planes[source.AlphaPlaneIndex])
            : default;

        if (previewWidth == image.Width && previewHeight == image.Height)
        {
            fixed (byte* destinationBase = destination)
                CopyNativeSizeBgra32(source, red, green, blue, alpha, destinationBase, stride, image.Width, image.Height);
            return;
        }

        fixed (byte* destinationBase = destination)
            CopyDownscaledBgra32(source, red, green, blue, alpha, destinationBase, stride, image.Width, image.Height, previewWidth, previewHeight);
    }

    private static unsafe void CopyNativeSizeBgra32(
        Bgra32Source source,
        PreviewPlaneAccess red,
        PreviewPlaneAccess green,
        PreviewPlaneAccess blue,
        PreviewPlaneAccess alpha,
        byte* destinationBase,
        int stride,
        int width,
        int height)
    {
        for (var y = 0; y < height; y++)
        {
            var destinationRow = (uint*)(destinationBase + y * stride);

            for (var x = 0; x < width; x++)
            {
                var redValue = red.ReadDisplayByte(x, y);

                if (source.IsMono)
                {
                    destinationRow[x] = ComposeBgra32(redValue, redValue, redValue, 255);
                    continue;
                }

                var greenValue = green.ReadDisplayByte(x, y);
                var blueValue = blue.ReadDisplayByte(x, y);
                var alphaValue = source.HasAlpha ? alpha.ReadDisplayByte(x, y) : (byte)255;
                destinationRow[x] = ComposeBgra32(blueValue, greenValue, redValue, alphaValue);
            }
        }
    }

    private static unsafe void CopyDownscaledBgra32(
        Bgra32Source source,
        PreviewPlaneAccess red,
        PreviewPlaneAccess green,
        PreviewPlaneAccess blue,
        PreviewPlaneAccess alpha,
        byte* destinationBase,
        int stride,
        int sourceWidth,
        int sourceHeight,
        int previewWidth,
        int previewHeight)
    {
        for (var targetY = 0; targetY < previewHeight; targetY++)
        {
            var sourceY0 = targetY * sourceHeight / previewHeight;
            var sourceY1 = Math.Max(sourceY0 + 1, (targetY + 1) * sourceHeight / previewHeight);
            var destinationRow = (uint*)(destinationBase + targetY * stride);

            for (var targetX = 0; targetX < previewWidth; targetX++)
            {
                var sourceX0 = targetX * sourceWidth / previewWidth;
                var sourceX1 = Math.Max(sourceX0 + 1, (targetX + 1) * sourceWidth / previewWidth);
                var redValue = red.ReadAverageDisplayByte(sourceX0, sourceX1, sourceY0, sourceY1);

                if (source.IsMono)
                {
                    destinationRow[targetX] = ComposeBgra32(redValue, redValue, redValue, 255);
                    continue;
                }

                var greenValue = green.ReadAverageDisplayByte(sourceX0, sourceX1, sourceY0, sourceY1);
                var blueValue = blue.ReadAverageDisplayByte(sourceX0, sourceX1, sourceY0, sourceY1);
                var alphaValue = source.HasAlpha ? alpha.ReadAverageDisplayByte(sourceX0, sourceX1, sourceY0, sourceY1) : (byte)255;
                destinationRow[targetX] = ComposeBgra32(blueValue, greenValue, redValue, alphaValue);
            }
        }
    }

    private static uint ComposeBgra32(byte blue, byte green, byte red, byte alpha)
        => (uint)(blue | (green << 8) | (red << 16) | (alpha << 24));

    private static Image? CreateScaledPreviewImage(Image image, int previewImageMaxDimension)
    {
        var previewSize = GetPreviewSize(image, previewImageMaxDimension);
        if (previewSize.Width == image.Width && previewSize.Height == image.Height)
            return null;

        var dataType = image.Planes[0].DataType;

        var scaledImage = new Image(new Size2D(previewSize.Width, previewSize.Height), image.Planes.Count, dataType);

        for (var planeIndex = 0; planeIndex < image.Planes.Count; planeIndex++)
        {
            var sourcePlane = image.Planes[planeIndex];
            var targetPlane = scaledImage.Planes[planeIndex];
            var bytesPerPixel = Math.Max(1, sourcePlane.DataType.BytesPerPixel);
            CopyDownscaledPlane(sourcePlane.GetLinearAccess(), targetPlane.GetLinearAccess(), image.Width, image.Height, previewSize.Width, previewSize.Height, bytesPerPixel);
        }

        return scaledImage;
    }

    private static Size2D GetPreviewSize(Image image, int previewImageMaxDimension)
    {
        if (previewImageMaxDimension <= 0 || image.Planes.Count == 0)
            return new Size2D(image.Width, image.Height);

        var longestEdge = Math.Max(image.Width, image.Height);
        if (longestEdge <= previewImageMaxDimension)
            return new Size2D(image.Width, image.Height);

        var scale = previewImageMaxDimension / (double)longestEdge;
        return new Size2D(
            Math.Max(1, (int)Math.Round(image.Width * scale)),
            Math.Max(1, (int)Math.Round(image.Height * scale)));
    }

    private static unsafe void CopyDownscaledPlane(
        LinearAccessData sourceAccess,
        LinearAccessData targetAccess,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        int bytesPerPixel)
    {
        byte* sourceBase = (byte*)sourceAccess.BasePtr;
        byte* targetBase = (byte*)targetAccess.BasePtr;
        long sourceYInc = sourceAccess.YInc.ToInt64();
        long sourceXInc = sourceAccess.XInc.ToInt64();
        long targetYInc = targetAccess.YInc.ToInt64();
        long targetXInc = targetAccess.XInc.ToInt64();

        if (bytesPerPixel == 1)
        {
            // Nearest-neighbor downscale makes high-frequency mono previews noisy. A tiny box
            // filter is still cheap and gives the UI a closer visual match to the source image.
            BoxFilterDownscaledPlane(sourceBase, targetBase, sourceWidth, sourceHeight, targetWidth, targetHeight, sourceXInc, sourceYInc, targetXInc, targetYInc);
            return;
        }

        for (var targetY = 0; targetY < targetHeight; targetY++)
        {
            var sourceY = MapTargetCoordinate(targetY, sourceHeight, targetHeight);
            var sourceRow = sourceBase + sourceY * sourceYInc;
            var targetRow = targetBase + targetY * targetYInc;

            for (var targetX = 0; targetX < targetWidth; targetX++)
            {
                var sourceX = MapTargetCoordinate(targetX, sourceWidth, targetWidth);
                var sourcePixel = sourceRow + sourceX * sourceXInc;
                var targetPixel = targetRow + targetX * targetXInc;
                Buffer.MemoryCopy(sourcePixel, targetPixel, bytesPerPixel, bytesPerPixel);
            }
        }
    }

    private static unsafe void BoxFilterDownscaledPlane(
        byte* sourceBase,
        byte* targetBase,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        long sourceXInc,
        long sourceYInc,
        long targetXInc,
        long targetYInc)
    {
        for (var targetY = 0; targetY < targetHeight; targetY++)
        {
            var sourceY0 = targetY * sourceHeight / targetHeight;
            var sourceY1 = Math.Max(sourceY0 + 1, (targetY + 1) * sourceHeight / targetHeight);
            var targetRow = targetBase + targetY * targetYInc;

            for (var targetX = 0; targetX < targetWidth; targetX++)
            {
                var sourceX0 = targetX * sourceWidth / targetWidth;
                var sourceX1 = Math.Max(sourceX0 + 1, (targetX + 1) * sourceWidth / targetWidth);

                var sum = 0L;
                var samples = 0;

                for (var sourceY = sourceY0; sourceY < sourceY1; sourceY++)
                {
                    var sourceRow = sourceBase + sourceY * sourceYInc;
                    for (var sourceX = sourceX0; sourceX < sourceX1; sourceX++)
                    {
                        sum += *(sourceRow + sourceX * sourceXInc);
                        samples++;
                    }
                }

                var targetPixel = targetRow + targetX * targetXInc;
                *targetPixel = samples > 0
                    ? (byte)Math.Clamp((int)Math.Round(sum / (double)samples), 0, 255)
                    : *(sourceBase + MapTargetCoordinate(targetY, sourceHeight, targetHeight) * sourceYInc + MapTargetCoordinate(targetX, sourceWidth, targetWidth) * sourceXInc);
            }
        }
    }

    private static int MapTargetCoordinate(int targetCoordinate, int sourceLength, int targetLength)
    {
        if (sourceLength <= 1 || targetLength <= 1)
            return 0;

        var mapped = ((targetCoordinate * 2L + 1L) * sourceLength) / (targetLength * 2L);
        return (int)Math.Clamp(mapped, 0L, sourceLength - 1L);
    }

    private readonly struct Bgra32Source
    {
        private Bgra32Source(
            int redPlaneIndex,
            int greenPlaneIndex,
            int bluePlaneIndex,
            int alphaPlaneIndex,
            bool isMono,
            string pixelFormat)
        {
            RedPlaneIndex = redPlaneIndex;
            GreenPlaneIndex = greenPlaneIndex;
            BluePlaneIndex = bluePlaneIndex;
            AlphaPlaneIndex = alphaPlaneIndex;
            IsMono = isMono;
            PixelFormat = pixelFormat;
        }

        public int RedPlaneIndex { get; }

        public int GreenPlaneIndex { get; }

        public int BluePlaneIndex { get; }

        public int AlphaPlaneIndex { get; }

        public bool IsMono { get; }

        public bool HasAlpha => AlphaPlaneIndex >= 0;

        public string PixelFormat { get; }

        public static Bgra32Source CreateMono(int planeIndex, int bitsPerPixel)
            => new(planeIndex, planeIndex, planeIndex, -1, true, $"Mono {bitsPerPixel}bpp");

        public static Bgra32Source CreateRgb(int alphaPlaneIndex, int bitsPerPixel)
            => new(0, 1, 2, alphaPlaneIndex, false, alphaPlaneIndex >= 0 ? $"RGBA {bitsPerPixel}bpp" : $"RGB {bitsPerPixel}bpp");
    }

    private readonly unsafe struct PreviewPlaneAccess
    {
        private readonly byte* _base;
        private readonly long _xInc;
        private readonly long _yInc;
        private readonly int _bytesPerPixel;
        private readonly int _bitsPerPixel;

        public PreviewPlaneAccess(ImagePlane plane)
        {
            var access = plane.GetLinearAccess();
            _base = (byte*)access.BasePtr;
            _xInc = access.XInc.ToInt64();
            _yInc = access.YInc.ToInt64();
            _bytesPerPixel = plane.DataType.BytesPerPixel;
            _bitsPerPixel = plane.DataType.BitsPerPixel;
        }

        public byte ReadDisplayByte(int x, int y)
            => ScaleRawToByte(ReadRaw(x, y));

        public byte ReadAverageDisplayByte(int sourceX0, int sourceX1, int sourceY0, int sourceY1)
        {
            var sum = 0L;
            var samples = 0;

            for (var y = sourceY0; y < sourceY1; y++)
            {
                for (var x = sourceX0; x < sourceX1; x++)
                {
                    sum += ReadRaw(x, y);
                    samples++;
                }
            }

            return samples > 0
                ? ScaleRawToByte((int)Math.Round(sum / (double)samples))
                : (byte)0;
        }

        private int ReadRaw(int x, int y)
        {
            var pixel = _base + y * _yInc + x * _xInc;
            return _bytesPerPixel == 1
                ? *pixel
                : pixel[0] | (pixel[1] << 8);
        }

        private byte ScaleRawToByte(int value)
        {
            if (_bitsPerPixel == 8)
                return (byte)value;

            var maxValue = (1 << _bitsPerPixel) - 1;
            return (byte)Math.Clamp((value * 255L + maxValue / 2L) / maxValue, 0L, 255L);
        }
    }
}
