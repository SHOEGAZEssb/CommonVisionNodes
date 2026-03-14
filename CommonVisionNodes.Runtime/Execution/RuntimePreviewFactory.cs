using System.Text;
using CommonVisionNodes.Contracts;
using Stemmer.Cvb;

namespace CommonVisionNodes.Runtime.Execution;

public sealed class RuntimePreviewFactory
{
    public ExecutionMessageDto? CreatePreviewMessage(string nodeId, Node node)
    {
        return node switch
        {
            ImageNode imageNode => CreateImagePreviewMessage(nodeId, imageNode.CachedImage),
            SaveImageNode saveImageNode => CreateImagePreviewMessage(nodeId, saveImageNode.ImageInput.Value as Image),
            DeviceNode deviceNode => CreateImagePreviewMessage(nodeId, deviceNode.ImageOutput.Value as Image),
            BinarizeNode binarizeNode => CreateImagePreviewMessage(nodeId, binarizeNode.ImageOutput.Value as Image),
            SubImageNode subImageNode => CreateImagePreviewMessage(nodeId, subImageNode.ImageOutput.Value as Image),
            MatrixTransformNode transformNode => CreateImagePreviewMessage(nodeId, transformNode.ImageOutput.Value as Image),
            ImageGeneratorNode generatorNode => CreateImagePreviewMessage(nodeId, generatorNode.ImageOutput.Value as Image),
            FilterNode filterNode => CreateImagePreviewMessage(nodeId, filterNode.ImageOutput.Value as Image),
            MorphologyNode morphologyNode => CreateImagePreviewMessage(nodeId, morphologyNode.ImageOutput.Value as Image),
            NormalizeNode normalizeNode => CreateImagePreviewMessage(nodeId, normalizeNode.ImageOutput.Value as Image),
            CSharpNode csharpNode => CreateImagePreviewMessage(nodeId, csharpNode.ImageOutput.Value as Image),
            HistogramNode histogramNode => CreateHistogramPreviewMessage(nodeId, histogramNode),
            BlobNode blobNode => CreateBlobPreviewMessage(nodeId, blobNode),
            PolimagoClassifyNode classifyNode => CreateClassificationPreviewMessage(nodeId, classifyNode),
            GenericVisualizerNode genericVisualizerNode => CreateGenericPreviewMessage(nodeId, genericVisualizerNode.LastValue),
            _ => null
        };
    }

    private ExecutionMessageDto? CreateImagePreviewMessage(string nodeId, Image? image)
    {
        var preview = CreateImagePreview(nodeId, image);
        return preview is null
            ? null
            : new ExecutionMessageDto
            {
                MessageType = ExecutionMessageTypeDto.ImagePreview,
                ImagePreview = preview
            };
    }

    private ExecutionMessageDto CreateHistogramPreviewMessage(string nodeId, HistogramNode node)
        => new()
        {
            MessageType = ExecutionMessageTypeDto.HistogramPreview,
            HistogramPreview = new HistogramPreviewDto
            {
                NodeId = nodeId,
                Bins = node.Bins.ToList(),
                Mean = node.Mean,
                StdDev = node.StdDev,
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

    private ExecutionMessageDto CreateBlobPreviewMessage(string nodeId, BlobNode node)
        => new()
        {
            MessageType = ExecutionMessageTypeDto.BlobPreview,
            BlobPreview = new BlobPreviewDto
            {
                NodeId = nodeId,
                Image = CreateImagePreview(nodeId, node.ImageOutput.Value as Image),
                Blobs = node.Blobs.Select(blob => new BlobInfoDto
                {
                    Label = blob.Label,
                    Area = blob.Area,
                    CentroidX = blob.CentroidX,
                    CentroidY = blob.CentroidY,
                    BoundsX = blob.BoundsX,
                    BoundsY = blob.BoundsY,
                    BoundsWidth = blob.BoundsWidth,
                    BoundsHeight = blob.BoundsHeight
                }).ToList(),
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

    private ExecutionMessageDto CreateClassificationPreviewMessage(string nodeId, PolimagoClassifyNode node)
        => new()
        {
            MessageType = ExecutionMessageTypeDto.ClassificationPreview,
            ClassificationPreview = new ClassificationPreviewDto
            {
                NodeId = nodeId,
                Image = CreateImagePreview(nodeId, node.ImageOutput.Value as Image),
                Results = node.Results.Select(result => new ClassificationResultDto
                {
                    BlobIndex = result.BlobIndex,
                    ClassName = result.ClassName,
                    Quality = result.Quality,
                    X = result.X,
                    Y = result.Y
                }).ToList(),
                TimestampUtc = DateTimeOffset.UtcNow
            }
        };

    private ExecutionMessageDto? CreateGenericPreviewMessage(string nodeId, object? value)
    {
        return value switch
        {
            Image image => CreateImagePreviewMessage(nodeId, image),
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

    private static ImagePreviewDto? CreateImagePreview(string nodeId, Image? image)
    {
        if (image is null || image.IsDisposed)
            return null;

        var tempPath = Path.Combine(Path.GetTempPath(), $"cvn-preview-{Guid.NewGuid():N}.png");
        try
        {
            image.Save(tempPath);
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
}
