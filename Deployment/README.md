# Local folder deployment

The local deployment contains framework-dependent Windows executables for the
single-file launcher, backend, and Uno desktop UI, plus the published Uno
WebAssembly files. The target computer needs the .NET 10 ASP.NET Core Runtime,
which also provides the base .NET runtime used by the launcher and Uno Skia
desktop UI. Installing the .NET 10 SDK also provides these runtimes.

No separate web server is required. Common Vision Blox, its required drivers,
and licensing must still be available.

To verify the runtime installation, run:

```powershell
dotnet --list-runtimes
```

The output must contain both `Microsoft.NETCore.App 10.0.x` and
`Microsoft.AspNetCore.App 10.0.x` for x64. Backend exit code `-2147450730`
(`0x80008096`) means that a required .NET framework was not found; when the
launcher itself starts, the missing framework is normally the .NET 10 ASP.NET
Core Runtime.

## Publish

The publishing machine needs the .NET 10 SDK, Uno tooling, and a matching
`wasm-tools` workload. If WebAssembly publishing reports `NETSDK1147`, run:

```powershell
dotnet workload restore CommonVisionNodesUI\CommonVisionNodesUI.csproj
```

Then deploy from the repository root:

```powershell
.\Deployment\Deploy-CommonVisionNodes.ps1
```

The default output is `artifacts\CommonVisionNodes`. To deploy elsewhere:

```powershell
.\Deployment\Deploy-CommonVisionNodes.ps1 `
  -OutputDirectory "D:\Deployments\CommonVisionNodes"
```

This produces:

```text
CommonVisionNodes/
|-- CommonVisionNodes.Launcher.exe
|-- Server/
|-- Desktop/
`-- Web/
```

The deployment omits debugging symbols, CVB XML API documentation, Web publish
metadata outside the served site, and precompressed Web sidecars that the local
static-file server does not use.

## Start

Start only the desktop UI:

```powershell
.\CommonVisionNodes.Launcher.exe --mode Desktop
```

Start the web UI in the default browser:

```powershell
.\CommonVisionNodes.Launcher.exe --mode Web
```

Web mode is the default, so this is equivalent:

```powershell
.\CommonVisionNodes.Launcher.exe
```

For web mode without opening a browser automatically:

```powershell
.\CommonVisionNodes.Launcher.exe --mode Web --no-browser
```

Desktop mode stops the backend when the desktop window closes. Web mode keeps
the launcher open; press Enter in its console window to stop all processes
started by the launcher.

Web mode first opens a local browser-reset page. It unregisters service workers
and clears WebAssembly caches left by older deployments before redirecting to
the UI. PWA registration is disabled in the deployed Uno configuration because
the folder deployment does not require offline caching.
