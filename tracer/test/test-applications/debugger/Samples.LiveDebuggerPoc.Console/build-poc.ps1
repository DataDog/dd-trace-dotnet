param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$sampleRoot = $PSScriptRoot
$viewerRoot = Join-Path (Split-Path -Parent $sampleRoot) "Samples.LiveDebuggerPoc.Viewer"

dotnet build (Join-Path $sampleRoot "Samples.LiveDebuggerPoc.Console.csproj") -c $Configuration -f net10.0
dotnet build (Join-Path $viewerRoot "Samples.LiveDebuggerPoc.Viewer.csproj") -c $Configuration -f net10.0
