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
    /// <param name="graph">The node graph to generate code for.</param>
    /// <returns>A complete C# code snippet as a string.</returns>
    public static string Generate(NodeGraph graph)
    {
        var connectedPorts = new HashSet<Port>();
        foreach (var c in graph.Connections)
        {
            connectedPorts.Add(c.Output);
            connectedPorts.Add(c.Input);
        }

        var sorted = TopologicalSort(graph)
            .Where(n => n.Inputs.Any(connectedPorts.Contains) || n.Outputs.Any(connectedPorts.Contains))
            .ToList();

        // Collect required usings from nodes
        var usings = new HashSet<string> { "Stemmer.Cvb" };
        foreach (var node in sorted)
            foreach (var u in node.RequiredUsings)
                usings.Add(u);

        var sb = new StringBuilder();

        // Using directives
        foreach (var u in usings.OrderBy(x => x))
            sb.AppendLine($"using {u};");
        sb.AppendLine();

        // Pipeline code
        var context = new CodeEmitContext(
            sb,
            graph.Connections,
            new Dictionary<Port, string>(),
            new Dictionary<string, int>());

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].EmitCode(context);
            if (i < sorted.Count - 1)
                sb.AppendLine();
        }

        // Helper methods (one set per node type)
        var helperSb = new StringBuilder();
        var emittedTypes = new HashSet<Type>();
        foreach (var node in sorted)
        {
            if (emittedTypes.Add(node.GetType()))
            {
                var before = helperSb.Length;
                node.EmitHelperMethods(helperSb);
                if (helperSb.Length > before)
                    helperSb.AppendLine();
            }
        }

        if (helperSb.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("// --- Helper Methods ---");
            sb.AppendLine();
            sb.Append(helperSb);
        }

        return sb.ToString();
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
