using System.Globalization;
using System.Text;

namespace CommonVisionNodes;

/// <summary>
/// Provides context for code generation, including variable tracking,
/// input resolution, and formatting utilities.
/// </summary>
public sealed class CodeEmitContext
{
    private readonly Dictionary<Port, string> _portVariables;
    private readonly Dictionary<string, int> _nameCounters;
    private readonly IReadOnlyList<Connection> _connections;

    public StringBuilder Builder { get; }

    internal CodeEmitContext(
        StringBuilder builder,
        IReadOnlyList<Connection> connections,
        Dictionary<Port, string> portVariables,
        Dictionary<string, int> nameCounters)
    {
        Builder = builder;
        _connections = connections;
        _portVariables = portVariables;
        _nameCounters = nameCounters;
    }

    public string GetUniqueVariable(string baseName)
    {
        if (!_nameCounters.TryGetValue(baseName, out int count))
        {
            _nameCounters[baseName] = 1;
            return baseName;
        }

        _nameCounters[baseName] = count + 1;
        return $"{baseName}{count + 1}";
    }

    public string? ResolveInput(Port input)
    {
        var connection = _connections.FirstOrDefault(c => c.Input == input);
        if (connection != null && _portVariables.TryGetValue(connection.Output, out var varName))
            return varName;
        return null;
    }

    public void RegisterOutput(Port output, string variableName)
    {
        _portVariables[output] = variableName;
    }

    public static string EscapeVerbatim(string s) => s.Replace("\"", "\"\"");

    public static string FormatDouble(double value)
    {
        var s = value.ToString("G", CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
            s += ".0";
        return s;
    }
}
