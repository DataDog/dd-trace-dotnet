param(
    [string]$Configuration = "Release",
    [string]$Input = ""
)

$ErrorActionPreference = "Stop"
$sampleRoot = $PSScriptRoot
$viewerRoot = Join-Path (Split-Path -Parent $sampleRoot) "Samples.LiveDebuggerPoc.Viewer"

if ([string]::IsNullOrWhiteSpace($Input)) {
    $captureRoot = Join-Path $env:TEMP "datadog-live-debugger-poc"
    $Input = Get-ChildItem -LiteralPath $captureRoot -Filter "flow-events-*.dflp" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

dotnet run --project (Join-Path $viewerRoot "Samples.LiveDebuggerPoc.Viewer.csproj") -c $Configuration -f net10.0 -- $Input
