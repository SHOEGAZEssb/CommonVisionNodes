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

    /// <summary>
    /// The string builder used to append generated code.
    /// </summary>
    public StringBuilder Builder { get; }

    /// <summary>
    /// Creates a new code emission context.
    /// </summary>
    /// <param name="builder">Builder to append generated code to.</param>
    /// <param name="connections">Graph connections used to resolve inputs.</param>
    /// <param name="portVariables">Mapping of output ports to their variable names.</param>
    /// <param name="nameCounters">Counters for generating unique variable names.</param>
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

    /// <summary>
    /// Returns a unique variable name based on the given base name.
    /// Appends a numeric suffix when the name has been used before.
    /// </summary>
    /// <param name="baseName">Desired variable name.</param>
    /// <returns>A unique variable name.</returns>
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

    /// <summary>
    /// Finds the variable name of the output port connected to the given input port.
    /// </summary>
    /// <param name="input">The input port to resolve.</param>
    /// <returns>The variable name, or <c>null</c> if the input is not connected.</returns>
    public string? ResolveInput(Port input)
    {
        var connection = _connections.FirstOrDefault(c => c.Input == input);
        if (connection != null && _portVariables.TryGetValue(connection.Output, out var varName))
            return varName;
        return null;
    }

    /// <summary>
    /// Associates an output port with its generated variable name.
    /// </summary>
    /// <param name="output">The output port.</param>
    /// <param name="variableName">The variable name in generated code.</param>
    public void RegisterOutput(Port output, string variableName)
    {
        _portVariables[output] = variableName;
    }

    /// <summary>
    /// Escapes a string for use inside a C# verbatim string literal.
    /// </summary>
    /// <param name="s">The string to escape.</param>
    /// <returns>The escaped string.</returns>
    public static string EscapeVerbatim(string s) => s.Replace("\"", "\"\"");

    /// <summary>
    /// Formats a double value as a C# literal with invariant culture.
    /// Ensures the result always contains a decimal point.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>A string suitable for C# source code.</returns>
    public static string FormatDouble(double value)
    {
        var s = value.ToString("G", CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
            s += ".0";
        return s;
    }
}
