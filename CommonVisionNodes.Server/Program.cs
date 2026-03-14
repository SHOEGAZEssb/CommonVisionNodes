using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;
using CommonVisionNodes.Contracts;
using CommonVisionNodes.Runtime;
using CommonVisionNodes.Runtime.Execution;
using CommonVisionNodes.Server.Services;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

app.UseCors("uno-client");
app.UseWebSockets();

app.MapGet("/", () => Results.Ok(new
{
    service = "CommonVisionNodes.Server",
    status = "ok"
}));

app.MapGet("/api/nodes/definitions", (RuntimeNodeCatalog catalog) => Results.Ok(catalog.GetDefinitions()));

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