using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Execution;
using CommonVisionNodes.Server.Services;
using CommonVisionNodes.Runtime.Definitions;

var builder = WebApplication.CreateBuilder(args);
var urls = builder.Configuration["Urls"] ?? "http://localhost:5077";
ServerBindingValidator.EnsureLoopbackUrls(urls);
builder.WebHost.UseUrls(urls);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("uno-client", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;

                return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
            });
    });
});

builder.Services.AddSingleton<RuntimeNodeCatalog>();
builder.Services.AddSingleton<RuntimeGraphFactory>();
builder.Services.AddSingleton<RuntimePreviewFactory>();
builder.Services.AddSingleton<RuntimeCodeGenerationService>();
builder.Services.AddSingleton<ExecutionClientManager>();
builder.Services.AddSingleton<WindowsNativePathPicker>();

var app = builder.Build();

app.UseCors("uno-client");
app.UseWebSockets();

PhysicalFileProvider? webFileProvider = null;
string? webIndexPath = null;
var configuredWebRoot = builder.Configuration["WebRoot"];
if (!string.IsNullOrWhiteSpace(configuredWebRoot))
{
    var webRoot = Path.GetFullPath(configuredWebRoot);
    if (!Directory.Exists(webRoot))
        throw new DirectoryNotFoundException($"The configured web UI directory does not exist: '{webRoot}'.");

    var indexPath = Path.Combine(webRoot, "index.html");
    if (!File.Exists(indexPath))
        throw new FileNotFoundException($"The configured web UI directory does not contain index.html: '{webRoot}'.", indexPath);

    webIndexPath = indexPath;
    webFileProvider = new PhysicalFileProvider(webRoot);
    app.Lifetime.ApplicationStopped.Register(webFileProvider.Dispose);

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = webFileProvider
    });

    var contentTypeProvider = new FileExtensionContentTypeProvider();
    contentTypeProvider.Mappings[".dat"] = "application/octet-stream";

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = webFileProvider,
        ContentTypeProvider = contentTypeProvider
    });
}

if (webIndexPath is null)
{
    app.MapGet("/", () => Results.Ok(new
    {
        service = "CommonVisionNodes.Server",
        status = "ok"
    }));
}
else
{
    app.MapGet("/", () => Results.File(webIndexPath, "text/html; charset=utf-8"));
}

app.MapGet("/api/health", () => Results.Ok(new
{
    service = "CommonVisionNodes.Server",
    status = "ok",
    webUiEnabled = webFileProvider is not null
}));

app.MapGet("/browser-reset", async context =>
{
    context.Response.Headers["Clear-Site-Data"] = "\"cache\"";
    context.Response.Headers.CacheControl = "no-store";
    context.Response.ContentType = "text/html; charset=utf-8";

    await context.Response.WriteAsync(
        """
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8">
          <title>Starting CommonVisionNodes</title>
        </head>
        <body>
          <p>Starting CommonVisionNodes...</p>
          <script>
            (async () => {
              if ("serviceWorker" in navigator) {
                const registrations = await navigator.serviceWorker.getRegistrations();
                await Promise.all(registrations.map(registration => registration.unregister()));
              }

              if ("caches" in window) {
                const cacheNames = await caches.keys();
                await Promise.all(cacheNames.map(cacheName => caches.delete(cacheName)));
              }

              location.replace("/?launch=" + Date.now());
            })().catch(error => {
              document.body.textContent = "Browser reset failed: " + error;
            });
          </script>
        </body>
        </html>
        """);
});

app.MapGet("/api/nodes/definitions", (RuntimeNodeCatalog catalog) => Results.Ok(catalog.GetDefinitions()));

app.MapPost("/api/path-picker", async (PathPickerRequestDto request, WindowsNativePathPicker picker) =>
{
    if (!OperatingSystem.IsWindows())
        return Results.Problem("Native path pickers are only available on Windows.", statusCode: StatusCodes.Status501NotImplemented);

    try
    {
        var path = await picker.PickAsync(request);
        return Results.Ok(new PathPickerResultDto { Path = path });
    }
    catch (Exception exception)
    {
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/graph/execute", async (ExecutionRequestDto request, ExecutionClientManager manager, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ClientId))
        return Results.BadRequest("clientId is required.");

    var accepted = await manager.StartExecutionAsync(request, cancellationToken);
    return Results.Ok(accepted);
});

app.MapPost("/api/graph/stop", async (StopExecutionRequestDto request, ExecutionClientManager manager) =>
{
    if (string.IsNullOrWhiteSpace(request.ClientId))
        return Results.BadRequest("clientId is required.");

    await manager.StopExecutionAsync(request.ClientId);
    return Results.Ok();
});

app.MapPost("/api/graph/settings", (UpdateExecutionSettingsRequestDto request, ExecutionClientManager manager) =>
{
    if (string.IsNullOrWhiteSpace(request.ClientId))
        return Results.BadRequest("clientId is required.");

    return manager.UpdateExecutionSettings(request)
        ? Results.Ok()
        : Results.NotFound();
});

app.MapPost("/api/graph/trigger", (TriggerNodeRequestDto request, ExecutionClientManager manager) =>
{
    if (string.IsNullOrWhiteSpace(request.ClientId))
        return Results.BadRequest("clientId is required.");

    if (string.IsNullOrWhiteSpace(request.NodeId))
        return Results.BadRequest("nodeId is required.");

    return manager.TriggerManualNode(request)
        ? Results.Ok()
        : Results.NotFound();
});

app.MapPost("/api/graph/node-properties", (UpdateNodePropertiesRequestDto request, ExecutionClientManager manager) =>
{
    if (string.IsNullOrWhiteSpace(request.ClientId))
        return Results.BadRequest("clientId is required.");

    if (string.IsNullOrWhiteSpace(request.NodeId))
        return Results.BadRequest("nodeId is required.");

    return manager.UpdateNodeProperties(request)
        ? Results.Ok()
        : Results.NotFound();
});

app.MapPost("/api/graph/codegen", (GraphDto graph, RuntimeCodeGenerationService codeGenerationService) =>
    Results.Text(codeGenerationService.GenerateCode(graph), "text/plain"));

app.Map("/ws/execution", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket connection expected.");
        return;
    }

    var clientId = context.Request.Query["clientId"].ToString();
    if (string.IsNullOrWhiteSpace(clientId))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("clientId query parameter is required.");
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var manager = context.RequestServices.GetRequiredService<ExecutionClientManager>();
    await manager.AttachSocketAsync(clientId, socket, context.RequestAborted);
});

app.Run();
