param(
    [ValidateSet("checkout", "slow", "exception")]
    [string]$Scenario = "checkout",
    [string]$Configuration = "Release",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$sampleRoot = $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($Output)) {
    $captureRoot = Join-Path $env:TEMP "datadog-live-debugger-poc"
    New-Item -ItemType Directory -Force -Path $captureRoot | Out-Null
    $Output = Join-Path $captureRoot "flow-events-$Scenario.dflp"
}

$env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED = "true"
$env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_OUTPUT_PATH = $Output

dotnet run --project (Join-Path $sampleRoot "Samples.LiveDebuggerPoc.Console.csproj") -c $Configuration -f net10.0 -- --scenario $Scenario --output $Output
