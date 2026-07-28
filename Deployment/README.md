# Local folder deployment

The local deployment contains self-contained Windows executables for the backend
and Uno desktop UI, plus the published Uno WebAssembly files. No .NET installation
or separate web server is required on the target computer. Common Vision Blox,
its required drivers, and licensing must still be available.

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
the launcher open; press Enter in its PowerShell window to stop all processes
started by the launcher.

Web mode first opens a local browser-reset page. It unregisters service workers
and clears WebAssembly caches left by older deployments before redirecting to
the UI. PWA registration is disabled in the deployed Uno configuration because
the folder deployment does not require offline caching.
