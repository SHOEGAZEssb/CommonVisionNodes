namespace CommonVisionNodesUI.Models;

public record AppConfig
{
    public string? Environment { get; init; }

    public string BackendBaseUrl { get; init; } = "http://127.0.0.1:5077";
}
