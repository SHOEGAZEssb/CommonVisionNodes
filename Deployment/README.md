# Local folder deployment

The local deployment contains framework-dependent Windows executables for the
single-file launcher, backend, and Uno desktop UI, plus the published Uno
WebAssembly files. Web mode requires the .NET 10 ASP.NET Core Runtime. Desktop
mode hosts CVB directly in the Uno application and requires only the base .NET
10 runtime used by the launcher and Uno Skia desktop UI. Installing the .NET 10
SDK provides both runtimes.

No separate web server is required. Common Vision Blox, its required drivers,
and licensing must still be available.

To verify the runtime installation, run:

```powershell
dotnet --list-runtimes
```

Desktop mode requires `Microsoft.NETCore.App 10.0.x` for x64. Web mode also
requires `Microsoft.AspNetCore.App 10.0.x`. Backend exit code `-2147450730`
(`0x80008096`) means that a required .NET framework was not found.

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

The deployment omits debugging symbols, CVB XML API documentation, and Web
publish metadata outside the served site. Brotli and gzip WebAssembly sidecars
remain in the Web folder; the local backend negotiates them for browsers that
support compression. The two Uno scripts patched after publish (`uno-bootstrap.js`
and `uno-config.js`) intentionally fall back to their raw copies, so no browser
can receive a stale compressed version.

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

Normal Web launches open the application directly and reuse fingerprinted
WebAssembly assets from the browser cache. If an older deployment or service
worker leaves the browser in a bad state, repair that browser profile explicitly:

```powershell
.\CommonVisionNodes.Launcher.exe --mode Web --reset-browser-cache
```

Desktop mode starts only the desktop UI and exits when its window closes. Web
mode keeps the launcher open; press Enter in its console window to stop all
processes started by the launcher.

The reset option opens a local repair page that unregisters legacy service
workers and clears cached WebAssembly assets before redirecting to the UI. PWA
registration remains disabled in the deployed Uno configuration because the
folder deployment does not require offline caching.
