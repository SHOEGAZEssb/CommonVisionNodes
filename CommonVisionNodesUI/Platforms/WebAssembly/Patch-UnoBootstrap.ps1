param(
    [string] $RootPath
)

if (-not $RootPath -or -not (Test-Path $RootPath)) {
    return
}

function Remove-StaleCompressedSidecars {
    param(
        [Parameter(Mandatory)]
        [System.IO.FileInfo]$SourceFile
    )

    # These generated scripts may have been patched after the SDK generated its sidecars.
    # Do not let content negotiation serve a compressed copy containing pre-patch JavaScript.
    foreach ($extension in @("br", "gz")) {
        $sidecarPath = "$($SourceFile.FullName).$extension"
        if (Test-Path -LiteralPath $sidecarPath -PathType Leaf) {
            Remove-Item -LiteralPath $sidecarPath -Force -ErrorAction Stop
        }
    }
}

$replacement = 'bootstrapper._runMain = dotnetRuntime.runMain ?? ((main, args) => dotnetRuntime.runMainAndExit(main, args));'
$files = Get-ChildItem $RootPath -Recurse -Filter 'uno-bootstrap.js' -ErrorAction SilentlyContinue

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $updated = $content.Replace('bootstrapper._runMain = dotnetRuntime.runMain;', $replacement)

    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($file.FullName, $updated, [System.Text.UTF8Encoding]::new($false))
    }

    Remove-StaleCompressedSidecars -SourceFile $file
}

$pwaSettingFound = $false
$configFiles = Get-ChildItem $RootPath -Recurse -Filter 'uno-config.js' -ErrorAction SilentlyContinue

foreach ($file in $configFiles) {
    $content = Get-Content $file.FullName -Raw
    if ($content.Contains('config.enable_pwa = true;') -or
        $content.Contains('config.enable_pwa = false;')) {
        $pwaSettingFound = $true
    }

    $updated = $content.Replace('config.enable_pwa = true;', 'config.enable_pwa = false;')

    if ($updated -ne $content) {
        [System.IO.File]::WriteAllText($file.FullName, $updated, [System.Text.UTF8Encoding]::new($false))
    }

    Remove-StaleCompressedSidecars -SourceFile $file
}

if (-not $pwaSettingFound) {
    throw "Uno's generated PWA setting was not found below '$RootPath'."
}
