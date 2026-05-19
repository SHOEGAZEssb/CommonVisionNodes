using CommonVisionNodes.Contracts;
using Stemmer.Cvb;

namespace CommonVisionNodes.Runtime.Execution;

/// <summary>
/// Creates execution preview messages from runtime node state.
/// </summary>
public sealed class RuntimePreviewFactory
{
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
                Base64Data = Convert.ToBase64String(bytes),
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
        if (image.Planes.Count != 1)
            return null;

        if (image.Planes[0].DataType.BytesPerPixel != 1)
            return null;

        // Uno can display BGRA32 directly; for mono 8-bit images this avoids a PNG round-trip
        // and keeps high-rate previews much cheaper.
        using var previewImage = CreateScaledPreviewImage(image, previewImageMaxDimension);
        var displayImage = previewImage ?? image;
        var stride = checked(displayImage.Width * 4);
        var bytes = new byte[checked(stride * displayImage.Height)];

        CopyBgra32(displayImage, bytes, stride);

        var sourceBitsPerPixel = image.Planes[0].DataType.BitsPerPixel;
        var sourcePixelFormat = $"Mono {sourceBitsPerPixel}bpp";

        return new ImagePreviewDto
        {
            NodeId = nodeId,
            MediaType = "application/x-bgra32",
            Encoding = ImagePreviewEncodingDto.Bgra32,
            Base64Data = Convert.ToBase64String(bytes),
            Width = image.Width,
            Height = image.Height,
            PreviewWidth = displayImage.Width,
            PreviewHeight = displayImage.Height,
            Stride = stride,
            PixelFormat = $"{sourcePixelFormat} -> BGRA32",
            TimestampUtc = DateTimeOffset.UtcNow
        };
    }

    private static unsafe void CopyBgra32(Image image, byte[] destination, int stride)
    {
        var bluePlane = image.Planes[0].GetLinearAccess();
        var greenPlane = bluePlane;
        var redPlane = bluePlane;

        byte* blueBase = (byte*)bluePlane.BasePtr;
        byte* greenBase = (byte*)greenPlane.BasePtr;
        byte* redBase = (byte*)redPlane.BasePtr;
        long blueYInc = bluePlane.YInc.ToInt64();
        long blueXInc = bluePlane.XInc.ToInt64();
        long greenYInc = greenPlane.YInc.ToInt64();
        long greenXInc = greenPlane.XInc.ToInt64();
        long redYInc = redPlane.YInc.ToInt64();
        long redXInc = redPlane.XInc.ToInt64();

        fixed (byte* destinationBase = destination)
        {
            for (var y = 0; y < image.Height; y++)
            {
                var blueRow = blueBase + y * blueYInc;
                var greenRow = greenBase + y * greenYInc;
                var redRow = redBase + y * redYInc;
                var destinationRow = destinationBase + y * stride;

                for (var x = 0; x < image.Width; x++)
                {
                    var pixel = destinationRow + x * 4;
                    pixel[0] = *(blueRow + x * blueXInc);
                    pixel[1] = *(greenRow + x * greenXInc);
                    pixel[2] = *(redRow + x * redXInc);
                    pixel[3] = 255;
                }
            }
        }
    }

    private static Image? CreateScaledPreviewImage(Image image, int previewImageMaxDimension)
    {
        if (previewImageMaxDimension <= 0)
            return null;

        if (image.Planes.Count == 0)
            return null;

        var longestEdge = Math.Max(image.Width, image.Height);
        if (longestEdge <= previewImageMaxDimension)
            return null;

        var scale = previewImageMaxDimension / (double)longestEdge;
        var targetWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
        var targetHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
        var dataType = image.Planes[0].DataType;

        var scaledImage = new Image(new Size2D(targetWidth, targetHeight), image.Planes.Count, dataType);

        for (var planeIndex = 0; planeIndex < image.Planes.Count; planeIndex++)
        {
            var sourcePlane = image.Planes[planeIndex];
            var targetPlane = scaledImage.Planes[planeIndex];
            var bytesPerPixel = Math.Max(1, sourcePlane.DataType.BytesPerPixel);
            CopyDownscaledPlane(sourcePlane.GetLinearAccess(), targetPlane.GetLinearAccess(), image.Width, image.Height, targetWidth, targetHeight, bytesPerPixel);
        }

        return scaledImage;
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
}
