param(
    [ValidateSet("checkout", "presentation", "multi-span", "slow", "exception", "async")]
    [string]$Scenario = "presentation",
    [ValidateSet("manual", "native")]
    [string]$Recording = "manual",
    [ValidateSet("logical", "traced", "none")]
    [string]$RootMode = "logical",
    [string]$TriggerReason = "",
    [string]$Root = "",
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
if (-not [string]::IsNullOrWhiteSpace($TriggerReason)) {
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_TRIGGER_REASON = $TriggerReason
}

if (-not [string]::IsNullOrWhiteSpace($Root)) {
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ROOT = $Root
}

$env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ALLOW_RECORDING_WITHOUT_OPERATION = $null
if ([string]::IsNullOrWhiteSpace($env:DD_TRACE_ENABLED)) {
    $env:DD_TRACE_ENABLED = "true"
}

if ([string]::IsNullOrWhiteSpace($env:DD_TRACE_AGENT_URL)) {
    $env:DD_TRACE_AGENT_URL = "http://127.0.0.1:8126"
}

if ([string]::IsNullOrWhiteSpace($env:DD_SERVICE)) {
    $env:DD_SERVICE = "live-debugger-flow-recorder-poc"
}

if ([string]::IsNullOrWhiteSpace($env:DD_ENV)) {
    $env:DD_ENV = "local-poc"
}

if ([string]::IsNullOrWhiteSpace($env:DD_VERSION)) {
    $env:DD_VERSION = "flow-recorder-poc"
}

dotnet run --project (Join-Path $sampleRoot "Samples.LiveDebuggerPoc.Console.csproj") -c $Configuration -f net10.0 -- --scenario $Scenario --recording $Recording --root-mode $RootMode --output $Output
