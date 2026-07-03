param(
    [string]$Configuration = "Release",
    [Alias("Input")]
    [string]$CapturePath = "",
    [switch]$Html,
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$sampleRoot = $PSScriptRoot
$viewerRoot = Join-Path (Split-Path -Parent $sampleRoot) "Samples.LiveDebuggerPoc.Viewer"

if ([string]::IsNullOrWhiteSpace($CapturePath)) {
    $captureRoot = Join-Path $env:TEMP "datadog-live-debugger-poc"
    $CapturePath = Get-ChildItem -LiteralPath $captureRoot -Filter "flow-events-*.dflp" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

$viewerArgs = @($CapturePath)
if ($Html) {
    $viewerArgs += "--html"
    if (-not [string]::IsNullOrWhiteSpace($Output)) {
        $viewerArgs += $Output
    }
}

dotnet run --project (Join-Path $viewerRoot "Samples.LiveDebuggerPoc.Viewer.csproj") -c $Configuration -f net10.0 -- @viewerArgs
