namespace CommonVisionNodesUI.Models;

/// <summary>
/// Application configuration loaded by the Uno host.
/// </summary>
public record AppConfig
{
    /// <summary>
    /// Optional host environment name.
    /// </summary>
    public string? Environment { get; init; }

    /// <summary>
    /// Base URL for the CommonVisionNodes backend service.
    /// </summary>
    public string BackendBaseUrl { get; init; } = "http://127.0.0.1:5077";
}
