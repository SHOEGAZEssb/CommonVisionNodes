param(
    [string] $RootPath
)

if (-not $RootPath -or -not (Test-Path $RootPath)) {
    exit 0
}

$replacement = 'bootstrapper._runMain = dotnetRuntime.runMain ?? ((main, args) => dotnetRuntime.runMainAndExit(main, args));'
$files = Get-ChildItem $RootPath -Recurse -Filter 'uno-bootstrap.js' -ErrorAction SilentlyContinue

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $updated = $content.Replace('bootstrapper._runMain = dotnetRuntime.runMain;', $replacement)

    if ($updated -ne $content) {
        Set-Content $file.FullName $updated
    }
}
