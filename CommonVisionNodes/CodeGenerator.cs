using System.Globalization;
using System.Text;

namespace CommonVisionNodes;

/// <summary>
/// Generates standalone C# code from a <see cref="NodeGraph"/> that uses the
/// Stemmer.Cvb SDK directly, without any dependency on CommonVisionNodes.
/// </summary>
public static class CodeGenerator
{
    /// <summary>
    /// Generates a C# code snippet that replicates the given node graph
    /// using only the Common Vision Blox SDK.
    /// </summary>
    public static string Generate(NodeGraph graph)
    {
        var sorted = TopologicalSort(graph);
        var portVariables = new Dictionary<Port, string>();
        var nameCounters = new Dictionary<string, int>();
        var requiredHelpers = new HashSet<string>();
        var needsDriver = false;
        var needsMarshal = false;

        foreach (var node in sorted)
        {
            switch (node)
            {
                case DeviceNode:
                    needsDriver = true;
                    break;
                case BinarizeNode:
                    needsMarshal = true;
                    requiredHelpers.Add("Binarize");
                    break;
                case SubImageNode:
                    needsMarshal = true;
                    requiredHelpers.Add("Crop");
                    break;
                case MatrixTransformNode:
                    needsMarshal = true;
                    requiredHelpers.Add("AffineTransform");
                    requiredHelpers.Add("SampleBilinear");
                    break;
            }
        }

        var sb = new StringBuilder();

        // Using directives
        if (needsMarshal)
            sb.AppendLine("using System.Runtime.InteropServices;");
        sb.AppendLine("using Stemmer.Cvb;");
        if (needsDriver)
            sb.AppendLine("using Stemmer.Cvb.Driver;");
        sb.AppendLine();

        // Pipeline code
        for (int i = 0; i < sorted.Count; i++)
        {
            EmitNode(sorted[i], graph.Connections, sb, portVariables, nameCounters);
            if (i < sorted.Count - 1)
                sb.AppendLine();
        }

        // Helper methods
        if (requiredHelpers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// --- Helper Methods ---");

            if (requiredHelpers.Contains("Binarize"))
            {
                sb.AppendLine();
                EmitBinarizeHelper(sb);
            }
            if (requiredHelpers.Contains("Crop"))
            {
                sb.AppendLine();
                EmitCropHelper(sb);
            }
            if (requiredHelpers.Contains("AffineTransform"))
            {
                sb.AppendLine();
                EmitAffineTransformHelper(sb);
            }
            if (requiredHelpers.Contains("SampleBilinear"))
            {
                sb.AppendLine();
                EmitSampleBilinearHelper(sb);
            }
        }

        return sb.ToString();
    }

    private static void EmitNode(
        Node node,
        IReadOnlyList<Connection> connections,
        StringBuilder sb,
        Dictionary<Port, string> portVariables,
        Dictionary<string, int> nameCounters)
    {
        switch (node)
        {
            case ImageNode n:
                EmitImageNode(n, sb, portVariables, nameCounters);
                break;
            case DeviceNode n:
                EmitDeviceNode(n, sb, portVariables, nameCounters);
                break;
            case BinarizeNode n:
                EmitBinarizeNode(n, connections, sb, portVariables, nameCounters);
                break;
            case SubImageNode n:
                EmitSubImageNode(n, connections, sb, portVariables, nameCounters);
                break;
            case MatrixTransformNode n:
                EmitMatrixTransformNode(n, connections, sb, portVariables, nameCounters);
                break;
            case SaveImageNode n:
                EmitSaveImageNode(n, connections, sb, portVariables);
                break;
        }
    }

    private static void EmitImageNode(
        ImageNode node, StringBuilder sb,
        Dictionary<Port, string> portVariables,
        Dictionary<string, int> nameCounters)
    {
        var varName = GetUniqueVariable("sourceImage", nameCounters);
        sb.AppendLine($"// Load image from file");
        sb.AppendLine($"using var {varName} = Image.FromFile(@\"{EscapeVerbatim(node.FilePath)}\");");
        portVariables[node.ImageOutput] = varName;
    }

    private static void EmitDeviceNode(
        DeviceNode node, StringBuilder sb,
        Dictionary<Port, string> portVariables,
        Dictionary<string, int> nameCounters)
    {
        var deviceVar = GetUniqueVariable("device", nameCounters);
        var streamVar = GetUniqueVariable("stream", nameCounters);
        var waitVar = GetUniqueVariable("streamResult", nameCounters);
        var imageVar = GetUniqueVariable("acquiredImage", nameCounters);

        sb.AppendLine($"// Acquire image from device");
        sb.AppendLine($"using var {deviceVar} = DeviceFactory.Open(@\"{EscapeVerbatim(node.AccessToken)}\", AcquisitionStack.GenTL) as GenICamDevice;");
        sb.AppendLine($"using var {streamVar} = {deviceVar}!.GetStream<ImageStream>(0);");
        sb.AppendLine($"{streamVar}.Start();");
        sb.AppendLine($"using var {waitVar} = {streamVar}.WaitFor(TimeSpan.FromSeconds(3));");
        sb.AppendLine($"using var {imageVar} = {waitVar}.Clone();");
        sb.AppendLine($"{streamVar}.TryStop();");
        portVariables[node.ImageOutput] = imageVar;
    }

    private static void EmitBinarizeNode(
        BinarizeNode node, IReadOnlyList<Connection> connections,
        StringBuilder sb, Dictionary<Port, string> portVariables,
        Dictionary<string, int> nameCounters)
    {
        var inputVar = ResolveInput(node.ImageInput, connections, portVariables);
        if (inputVar == null) return;

        var varName = GetUniqueVariable("binarized", nameCounters);
        sb.AppendLine($"// Binarize (threshold: {node.Threshold})");
        sb.AppendLine($"using var {varName} = Binarize({inputVar}, {node.Threshold});");
        portVariables[node.ImageOutput] = varName;
    }

    private static void EmitSubImageNode(
        SubImageNode node, IReadOnlyList<Connection> connections,
        StringBuilder sb, Dictionary<Port, string> portVariables,
        Dictionary<string, int> nameCounters)
    {
        var inputVar = ResolveInput(node.ImageInput, connections, portVariables);
        if (inputVar == null) return;

        var varName = GetUniqueVariable("cropped", nameCounters);
        sb.AppendLine($"// Crop image (x: {node.AreaX}, y: {node.AreaY}, w: {node.AreaWidth}, h: {node.AreaHeight})");
        sb.AppendLine($"using var {varName} = Crop({inputVar}, {node.AreaX}, {node.AreaY}, {node.AreaWidth}, {node.AreaHeight});");
        portVariables[node.ImageOutput] = varName;
    }

    private static void EmitMatrixTransformNode(
        MatrixTransformNode node, IReadOnlyList<Connection> connections,
        StringBuilder sb, Dictionary<Port, string> portVariables,
        Dictionary<string, int> nameCounters)
    {
        var inputVar = ResolveInput(node.ImageInput, connections, portVariables);
        if (inputVar == null) return;

        var varName = GetUniqueVariable("transformed", nameCounters);
        sb.AppendLine($"// Affine transform (angle: {Fmt(node.Angle)}, scale: {Fmt(node.ScaleX)}x{Fmt(node.ScaleY)}, translate: {Fmt(node.TranslateX)},{Fmt(node.TranslateY)})");
        sb.AppendLine($"using var {varName} = AffineTransform({inputVar}, {Fmt(node.Angle)}, {Fmt(node.ScaleX)}, {Fmt(node.ScaleY)}, {Fmt(node.TranslateX)}, {Fmt(node.TranslateY)});");
        portVariables[node.ImageOutput] = varName;
    }

    private static void EmitSaveImageNode(
        SaveImageNode node, IReadOnlyList<Connection> connections,
        StringBuilder sb, Dictionary<Port, string> portVariables)
    {
        var inputVar = ResolveInput(node.ImageInput, connections, portVariables);
        if (inputVar == null) return;

        sb.AppendLine($"// Save image to file");
        sb.AppendLine($"{inputVar}.Save(@\"{EscapeVerbatim(node.FilePath)}\");");
    }

    // --- Helper method emitters ---

    private static void EmitBinarizeHelper(StringBuilder sb)
    {
        sb.AppendLine("static Image Binarize(Image source, int threshold)");
        sb.AppendLine("{");
        sb.AppendLine("    var result = new Image(source.Size, source.Planes.Count);");
        sb.AppendLine("    for (int p = 0; p < source.Planes.Count; p++)");
        sb.AppendLine("    {");
        sb.AppendLine("        var srcAccess = source.Planes[p].GetLinearAccess();");
        sb.AppendLine("        var dstAccess = result.Planes[p].GetLinearAccess();");
        sb.AppendLine("        for (int y = 0; y < source.Height; y++)");
        sb.AppendLine("        {");
        sb.AppendLine("            for (int x = 0; x < source.Width; x++)");
        sb.AppendLine("            {");
        sb.AppendLine("                var srcPtr = srcAccess.BasePtr + (nint)(y * srcAccess.YInc + x * srcAccess.XInc);");
        sb.AppendLine("                var dstPtr = dstAccess.BasePtr + (nint)(y * dstAccess.YInc + x * dstAccess.XInc);");
        sb.AppendLine("                byte val = Marshal.ReadByte(srcPtr);");
        sb.AppendLine("                Marshal.WriteByte(dstPtr, val >= threshold ? (byte)255 : (byte)0);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    return result;");
        sb.AppendLine("}");
    }

    private static void EmitCropHelper(StringBuilder sb)
    {
        sb.AppendLine("static Image Crop(Image source, int areaX, int areaY, int areaWidth, int areaHeight)");
        sb.AppendLine("{");
        sb.AppendLine("    int x = Math.Clamp(areaX, 0, Math.Max(0, source.Width - 1));");
        sb.AppendLine("    int y = Math.Clamp(areaY, 0, Math.Max(0, source.Height - 1));");
        sb.AppendLine("    int w = Math.Clamp(areaWidth, 1, source.Width - x);");
        sb.AppendLine("    int h = Math.Clamp(areaHeight, 1, source.Height - y);");
        sb.AppendLine("    var result = new Image(new Size2D(w, h), source.Planes.Count);");
        sb.AppendLine("    for (int p = 0; p < source.Planes.Count; p++)");
        sb.AppendLine("    {");
        sb.AppendLine("        var srcAccess = source.Planes[p].GetLinearAccess();");
        sb.AppendLine("        var dstAccess = result.Planes[p].GetLinearAccess();");
        sb.AppendLine("        for (int dy = 0; dy < h; dy++)");
        sb.AppendLine("        {");
        sb.AppendLine("            for (int dx = 0; dx < w; dx++)");
        sb.AppendLine("            {");
        sb.AppendLine("                var srcPtr = srcAccess.BasePtr + (nint)((y + dy) * srcAccess.YInc + (x + dx) * srcAccess.XInc);");
        sb.AppendLine("                var dstPtr = dstAccess.BasePtr + (nint)(dy * dstAccess.YInc + dx * dstAccess.XInc);");
        sb.AppendLine("                Marshal.WriteByte(dstPtr, Marshal.ReadByte(srcPtr));");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    return result;");
        sb.AppendLine("}");
    }

    private static void EmitAffineTransformHelper(StringBuilder sb)
    {
        sb.AppendLine("static Image AffineTransform(Image source, double angle, double scaleX, double scaleY, double translateX, double translateY)");
        sb.AppendLine("{");
        sb.AppendLine("    int srcW = source.Width;");
        sb.AppendLine("    int srcH = source.Height;");
        sb.AppendLine("    var result = new Image(source.Size, source.Planes.Count);");
        sb.AppendLine("    double rad = angle * Math.PI / 180.0;");
        sb.AppendLine("    double cos = Math.Cos(rad);");
        sb.AppendLine("    double sin = Math.Sin(rad);");
        sb.AppendLine("    double cx = srcW / 2.0;");
        sb.AppendLine("    double cy = srcH / 2.0;");
        sb.AppendLine("    double invSx = scaleX == 0 ? 0 : 1.0 / scaleX;");
        sb.AppendLine("    double invSy = scaleY == 0 ? 0 : 1.0 / scaleY;");
        sb.AppendLine("    for (int p = 0; p < source.Planes.Count; p++)");
        sb.AppendLine("    {");
        sb.AppendLine("        var srcAccess = source.Planes[p].GetLinearAccess();");
        sb.AppendLine("        var dstAccess = result.Planes[p].GetLinearAccess();");
        sb.AppendLine("        for (int dy = 0; dy < srcH; dy++)");
        sb.AppendLine("        {");
        sb.AppendLine("            for (int dx = 0; dx < srcW; dx++)");
        sb.AppendLine("            {");
        sb.AppendLine("                double rx = (dx - translateX - cx) * invSx;");
        sb.AppendLine("                double ry = (dy - translateY - cy) * invSy;");
        sb.AppendLine("                double sx = rx * cos + ry * sin + cx;");
        sb.AppendLine("                double sy = -rx * sin + ry * cos + cy;");
        sb.AppendLine("                byte val = SampleBilinear(srcAccess, sx, sy, srcW, srcH);");
        sb.AppendLine("                var dstPtr = dstAccess.BasePtr + (nint)(dy * dstAccess.YInc + dx * dstAccess.XInc);");
        sb.AppendLine("                Marshal.WriteByte(dstPtr, val);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    return result;");
        sb.AppendLine("}");
    }

    private static void EmitSampleBilinearHelper(StringBuilder sb)
    {
        sb.AppendLine("static byte SampleBilinear(LinearAccessData access, double x, double y, int w, int h)");
        sb.AppendLine("{");
        sb.AppendLine("    if (x < 0 || y < 0 || x >= w - 1 || y >= h - 1)");
        sb.AppendLine("        return 0;");
        sb.AppendLine("    int x0 = (int)x;");
        sb.AppendLine("    int y0 = (int)y;");
        sb.AppendLine("    int x1 = x0 + 1;");
        sb.AppendLine("    int y1 = y0 + 1;");
        sb.AppendLine("    double fx = x - x0;");
        sb.AppendLine("    double fy = y - y0;");
        sb.AppendLine("    byte v00 = Marshal.ReadByte(access.BasePtr + (nint)(y0 * access.YInc + x0 * access.XInc));");
        sb.AppendLine("    byte v10 = Marshal.ReadByte(access.BasePtr + (nint)(y0 * access.YInc + x1 * access.XInc));");
        sb.AppendLine("    byte v01 = Marshal.ReadByte(access.BasePtr + (nint)(y1 * access.YInc + x0 * access.XInc));");
        sb.AppendLine("    byte v11 = Marshal.ReadByte(access.BasePtr + (nint)(y1 * access.YInc + x1 * access.XInc));");
        sb.AppendLine("    double val = v00 * (1 - fx) * (1 - fy)");
        sb.AppendLine("               + v10 * fx * (1 - fy)");
        sb.AppendLine("               + v01 * (1 - fx) * fy");
        sb.AppendLine("               + v11 * fx * fy;");
        sb.AppendLine("    return (byte)Math.Clamp(val, 0, 255);");
        sb.AppendLine("}");
    }

    // --- Utilities ---

    private static string GetUniqueVariable(string baseName, Dictionary<string, int> counters)
    {
        if (!counters.TryGetValue(baseName, out int count))
        {
            counters[baseName] = 1;
            return baseName;
        }

        counters[baseName] = count + 1;
        return $"{baseName}{count + 1}";
    }

    private static string? ResolveInput(
        Port input, IReadOnlyList<Connection> connections,
        Dictionary<Port, string> portVariables)
    {
        var connection = connections.FirstOrDefault(c => c.Input == input);
        if (connection != null && portVariables.TryGetValue(connection.Output, out var varName))
            return varName;
        return null;
    }

    private static string EscapeVerbatim(string s) => s.Replace("\"", "\"\"");

    private static string Fmt(double value)
    {
        var s = value.ToString("G", CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
            s += ".0";
        return s;
    }

    private static List<Node> TopologicalSort(NodeGraph graph)
    {
        var nodes = graph.Nodes;
        var connections = graph.Connections;

        var inDegree = new Dictionary<Node, int>();
        var adjacency = new Dictionary<Node, List<Node>>();

        foreach (var node in nodes)
        {
            inDegree[node] = 0;
            adjacency[node] = [];
        }

        foreach (var connection in connections)
        {
            var from = connection.Output.Node;
            var to = connection.Input.Node;
            adjacency[from].Add(to);
            inDegree[to]++;
        }

        var queue = new Queue<Node>();
        foreach (var node in nodes)
        {
            if (inDegree[node] == 0)
                queue.Enqueue(node);
        }

        var sorted = new List<Node>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            foreach (var neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        if (sorted.Count != nodes.Count)
            throw new InvalidOperationException("Graph contains a cycle");

        return sorted;
    }
}
