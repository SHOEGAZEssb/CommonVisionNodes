namespace CommonVisionNodes.Contracts;

/// <summary>
/// Type of native path picker requested from the local execution backend.
/// </summary>
public enum PathPickerModeDto
{
    OpenFile,
    OpenFolder,
    SaveFile
}

/// <summary>
/// Request to show a native path picker on the execution backend.
/// </summary>
public sealed class PathPickerRequestDto
{
    public PathPickerModeDto Mode { get; set; }

    public string? Title { get; set; }

    public string? InitialPath { get; set; }

    public string? SuggestedFileName { get; set; }

    public List<string> FileExtensions { get; set; } = [];
}

/// <summary>
/// Result from a native backend path picker.
/// </summary>
public sealed class PathPickerResultDto
{
    /// <summary>
    /// Selected absolute host path, or <see langword="null"/> when cancelled.
    /// </summary>
    public string? Path { get; set; }
}
