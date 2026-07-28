using System.Net;

namespace CommonVisionNodes.Server.Services;

internal static class ServerBindingValidator
{
    public static void EnsureLoopbackUrls(string urls)
    {
        var values = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0)
            throw new InvalidOperationException("At least one loopback listen URL is required.");

        foreach (var value in values)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                 IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address)))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"CommonVisionNodes.Server only accepts loopback URLs because C# nodes execute trusted code in-process. Invalid URL: '{value}'.");
        }
    }
}
