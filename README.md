# CommonVisionNodes

CommonVisionNodes is a .NET 10 visual image-processing playground for Common Vision Blox (CVB). It lets you build an image-processing pipeline as a typed node graph, execute it through a local backend, inspect live previews, and generate standalone CVB SDK C# code from the same graph.

## Solution Layout

- `CommonVisionNodes.Contracts` - shared DTOs for graph definitions, execution requests, execution state, and preview messages.
- `CommonVisionNodes.Runtime` - runtime node graph, CVB processing nodes, graph execution, previews, and C# code generation.
- `CommonVisionNodes.Server` - ASP.NET Core backend that exposes node definitions, graph execution, stop, code generation, and WebSocket execution updates.
- `CommonVisionNodesUI` - Uno Platform UI for editing node graphs, saving/loading graph files, running pipelines, and viewing previews.
- `Tests/CommonVisionNodes.Test` - NUnit tests for graph behavior, image nodes, code generation, runtime graph building, and execution messages.

## Features

- Visual node graph for CVB image workflows.
- Input, processing, analysis, and output nodes:
  - image file, camera device, generated test pattern
  - binarize, crop, transform, filter, morphology, normalize, C# script
  - histogram, blob detection, Polimago classification, generic visualizer
  - save image
- Single-frame and continuous execution modes.
- WebSocket status updates with per-node execution timing.
- Image, histogram, blob, classification, and text previews.
- Preview throttling and preview image downscaling.
- Graph save/load as `.cvbgraph`.
- Standalone CVB SDK code generation.

## Prerequisites

- .NET SDK 10.
- Uno Platform workload/support matching the solution SDK configuration.
- Common Vision Blox installed locally.
- `CVB` environment variable pointing at the CVB installation directory. The runtime and tests reference:

```text
$(CVB)\Lib\Net\Stemmer.Cvb.dll
$(CVB)\Lib\Net\Stemmer.Cvb.Foundation.dll
$(CVB)\Lib\Net\Stemmer.Cvb.Polimago.dll
```

The solution currently uses `Uno.Sdk` version `6.5.33` from `global.json`.

## Build And Test

Restore packages:

```powershell
dotnet restore CommonVisionNodes.slnx
```

Build the full solution:

```powershell
dotnet build CommonVisionNodes.slnx
```

Run all tests:

```powershell
dotnet test CommonVisionNodes.slnx --no-restore
```

Run only the test project:

```powershell
dotnet test Tests\CommonVisionNodes.Test\CommonVisionNodes.Test.csproj
```

## Running The App

Start the backend first:

```powershell
dotnet run --project CommonVisionNodes.Server\CommonVisionNodes.Server.csproj
```

By default the backend listens on:

```text
http://localhost:5077
```

Run the Uno UI in desktop mode:

```powershell
dotnet run --project CommonVisionNodesUI\CommonVisionNodesUI.csproj -f net10.0-desktop
```

Run the Uno UI in WebAssembly mode:

```powershell
dotnet run --project CommonVisionNodesUI\CommonVisionNodesUI.csproj -f net10.0-browserwasm
```

The UI reads the backend URL from `CommonVisionNodesUI/appsettings.json`:

```json
{
  "AppConfig": {
    "BackendBaseUrl": "http://localhost:5077"
  }
}
```

## Local Folder Deployment

To publish a self-contained folder with desktop and browser launch modes, see
[`Deployment/README.md`](Deployment/README.md). The deployed launcher supports:

```powershell
.\Deployment\Deploy-CommonVisionNodes.ps1

.\CommonVisionNodes.Launcher.exe --mode Desktop
.\CommonVisionNodes.Launcher.exe --mode Web
.\CommonVisionNodes.Launcher.exe
```

## Backend API

The server exposes these main endpoints:

- `GET /` - health/status response.
- `GET /api/nodes/definitions` - available node types, ports, properties, defaults, and preview metadata.
- `POST /api/graph/execute` - start a single or continuous graph execution.
- `POST /api/graph/stop` - stop the current execution for a client.
- `POST /api/graph/codegen` - generate standalone CVB SDK C# code for a graph.
- `GET /ws/execution?clientId=...` - WebSocket stream for execution state, node updates, and previews.

## Graph Model

Graphs are transferred as DTOs from `CommonVisionNodes.Contracts`:

- `GraphDto` contains `Nodes` and `Connections`.
- `NodeDto` stores an id, node type, canvas position, and serialized properties.
- `ConnectionDto` links an output port on one node to an input port on another node.
- `NodePropertyDto` stores property values as strings so the UI and backend can share one wire format.

At runtime, `RuntimeGraphFactory` converts the DTO graph into a `NodeGraph`, creates concrete nodes from `RuntimeNodeCatalog`, applies properties, and wires typed ports.

## Code Generation

The runtime can turn a graph into standalone C# that uses the CVB SDK directly:

```powershell
# Through the UI: click "Generate Code"
# Through the backend: POST a GraphDto to /api/graph/codegen
```

Generated code includes only graph-relevant nodes: connected pipeline nodes plus standalone source nodes. Unconnected sink-only nodes are omitted.

## Development Notes

- Keep `CommonVisionNodes.Contracts` free of CVB runtime dependencies where possible; it is the shared boundary between UI and backend.
- Add new runtime nodes in three places:
  - runtime node implementation in `CommonVisionNodes.Runtime`
  - node definition/factory entry in `RuntimeNodeCatalog`
  - matching view model/template in `CommonVisionNodesUI`
- Prefer adding focused tests when changing graph wiring, code generation, runtime execution, or preview behavior.
- The `CSharpNode` compiles user-provided code in-process. Treat it as trusted-local functionality unless it is moved into a sandboxed execution model.
