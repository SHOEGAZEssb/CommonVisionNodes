[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts\CommonVisionNodes"),

    [string]$RuntimeIdentifier = "win-x64",

    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$repositoryArtifactsDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$deploymentSourceDirectory = [System.IO.Path]::GetFullPath($PSScriptRoot)
$outputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
$outputPath = [System.IO.Path]::GetFullPath($outputPath).TrimEnd([char[]]@("\", "/"))
$outputParent = Split-Path -Parent $outputPath
$outputName = Split-Path -Leaf $outputPath
$uniqueSuffix = "$PID.$([Guid]::NewGuid().ToString('N'))"
$stagingPath = Join-Path $outputParent "$outputName.staging.$uniqueSuffix"
$backupPath = Join-Path $outputParent "$outputName.previous.$uniqueSuffix"

function Assert-SafeOutputPath {
    $driveRoot = [System.IO.Path]::GetPathRoot($outputPath).TrimEnd([char[]]@("\", "/"))
    $directorySeparator = [System.IO.Path]::DirectorySeparatorChar
    $repositoryPrefix = "$repositoryRoot$directorySeparator"
    $artifactsPrefix = "$repositoryArtifactsDirectory$directorySeparator"
    $outputPrefix = "$outputPath$directorySeparator"

    if ([string]::IsNullOrWhiteSpace($outputName)) {
        throw "OutputDirectory must name a deployment folder."
    }

    if ($outputPath.Equals($driveRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputDirectory cannot be a drive root."
    }

    if ($outputPath.Equals($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputDirectory cannot be the repository root."
    }

    if ($repositoryRoot.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputDirectory cannot contain the repository."
    }

    if ($outputPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        -not $outputPath.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "An OutputDirectory inside the repository must be below '$repositoryArtifactsDirectory'."
    }

    if ($outputPath.Equals($deploymentSourceDirectory, [StringComparison]::OrdinalIgnoreCase)) {
        throw "OutputDirectory cannot overwrite the Deployment source directory."
    }

    if (Test-Path -LiteralPath $outputPath -PathType Leaf) {
        throw "OutputDirectory points to an existing file: '$outputPath'."
    }
}

function Invoke-Publish {
    param(
        [Parameter(Mandatory)]
        [string]$Description,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host ""
    Write-Host "Publishing $Description..."
    & dotnet publish @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing $Description failed with exit code $LASTEXITCODE."
    }
}

function Assert-DeploymentFile {
    param(
        [Parameter(Mandatory)]
        [string]$LiteralPath,

        [Parameter(Mandatory)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $LiteralPath -PathType Leaf)) {
        throw "$Description was not produced at '$LiteralPath'."
    }
}

Assert-SafeOutputPath
New-Item -ItemType Directory -Path $outputParent -Force | Out-Null

$commonArguments = @()
if ($NoRestore) {
    $commonArguments += "--no-restore"
}

Push-Location $repositoryRoot
try {
    New-Item -ItemType Directory -Path $stagingPath | Out-Null

    Invoke-Publish `
        -Description "launcher" `
        -Arguments (@(
            (Join-Path $repositoryRoot "CommonVisionNodes.Launcher\CommonVisionNodes.Launcher.csproj"),
            "-c", "Release",
            "-r", $RuntimeIdentifier,
            "--self-contained", "false",
            "-o", $stagingPath
        ) + $commonArguments)

    Invoke-Publish `
        -Description "backend" `
        -Arguments (@(
            (Join-Path $repositoryRoot "CommonVisionNodes.Server\CommonVisionNodes.Server.csproj"),
            "-c", "Release",
            "-r", $RuntimeIdentifier,
            "--self-contained", "false",
            "-o", (Join-Path $stagingPath "Server")
        ) + $commonArguments)

    Invoke-Publish `
        -Description "Uno desktop UI" `
        -Arguments (@(
            (Join-Path $repositoryRoot "CommonVisionNodesUI\CommonVisionNodesUI.csproj"),
            "-c", "Release",
            "-f", "net10.0-desktop",
            "-r", $RuntimeIdentifier,
            "--self-contained", "false",
            "-o", (Join-Path $stagingPath "Desktop")
        ) + $commonArguments)

    $webPublishPath = Join-Path $stagingPath "Web.publish"
    try {
        Invoke-Publish `
            -Description "Uno WebAssembly UI" `
            -Arguments (@(
                (Join-Path $repositoryRoot "CommonVisionNodesUI\CommonVisionNodesUI.csproj"),
                "-c", "Release",
                "-f", "net10.0-browserwasm",
                "-o", $webPublishPath
            ) + $commonArguments)
    }
    catch {
        throw "$($_.Exception.Message) If the build reports NETSDK1147, run 'dotnet workload restore CommonVisionNodesUI\CommonVisionNodesUI.csproj' on the publishing machine."
    }

    $publishedWebRootCandidates = @(
        (Join-Path $webPublishPath "wwwroot"),
        $webPublishPath
    )
    $publishedWebRoot = $publishedWebRootCandidates |
        Where-Object { Test-Path -LiteralPath (Join-Path $_ "index.html") -PathType Leaf } |
        Select-Object -First 1

    if ($null -eq $publishedWebRoot) {
        throw "The WebAssembly publish did not produce Web\wwwroot\index.html or Web\index.html."
    }

    & (Join-Path $repositoryRoot "CommonVisionNodesUI\Platforms\WebAssembly\Patch-UnoBootstrap.ps1") `
        -RootPath $publishedWebRoot

    $deployedWebRoot = Join-Path $stagingPath "Web"
    if ($publishedWebRoot.Equals($webPublishPath, [StringComparison]::OrdinalIgnoreCase)) {
        Move-Item -LiteralPath $webPublishPath -Destination $deployedWebRoot
    }
    else {
        Move-Item -LiteralPath $publishedWebRoot -Destination $deployedWebRoot
        Remove-Item -LiteralPath $webPublishPath -Recurse -Force
    }
    $publishedWebRoot = $deployedWebRoot

    # Release deployments do not need debugging symbols or CVB API documentation.
    $unnecessaryFiles = @(
        Get-ChildItem -LiteralPath (Join-Path $stagingPath "Server"), (Join-Path $stagingPath "Desktop") `
            -Recurse -File -Filter "*.pdb"
        Get-ChildItem -LiteralPath (Join-Path $stagingPath "Server") `
            -Recurse -File -Filter "Stemmer.Cvb*.xml"
    )

    foreach ($file in $unnecessaryFiles) {
        Remove-Item -LiteralPath $file.FullName -Force
    }

    Assert-DeploymentFile `
        -LiteralPath (Join-Path $stagingPath "Server\CommonVisionNodes.Server.exe") `
        -Description "The backend executable"
    Assert-DeploymentFile `
        -LiteralPath (Join-Path $stagingPath "Desktop\CommonVisionNodesUI.exe") `
        -Description "The desktop UI executable"

    Assert-DeploymentFile `
        -LiteralPath (Join-Path $stagingPath "CommonVisionNodes.Launcher.exe") `
        -Description "The launcher"
    Assert-DeploymentFile `
        -LiteralPath (Join-Path $publishedWebRoot "index.html") `
        -Description "The WebAssembly entry point"

    if (Test-Path -LiteralPath $outputPath -PathType Container) {
        Move-Item -LiteralPath $outputPath -Destination $backupPath
    }

    try {
        Move-Item -LiteralPath $stagingPath -Destination $outputPath
    }
    catch {
        if (Test-Path -LiteralPath $backupPath -PathType Container) {
            Move-Item -LiteralPath $backupPath -Destination $outputPath
        }
        throw
    }

    if (Test-Path -LiteralPath $backupPath -PathType Container) {
        try {
            Remove-Item -LiteralPath $backupPath -Recurse -Force
        }
        catch {
            Write-Warning "Deployment succeeded, but the previous deployment could not be removed: '$backupPath'."
        }
    }

    Write-Host ""
    Write-Host "Deployment ready: $outputPath"
    Write-Host "Launch the default Web UI with:"
    Write-Host "  & `"$outputPath\CommonVisionNodes.Launcher.exe`""
}
finally {
    Pop-Location

    if (Test-Path -LiteralPath $stagingPath -PathType Container) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
}
