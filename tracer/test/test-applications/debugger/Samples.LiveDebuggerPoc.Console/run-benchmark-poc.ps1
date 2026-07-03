param(
    [int]$Iterations = 100000,
    [int]$Warmup = 5000,
    [int]$ProbeInstallDelayMs = 5000,
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "",
    [switch]$SkipBuild,
    [switch]$IncludeDiagnosticVariants,
    [switch]$TraceDebugLogs
)

$ErrorActionPreference = "Stop"
$sampleRoot = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $sampleRoot "..\..\..\..\..")
$project = Join-Path $sampleRoot "Samples.LiveDebuggerPoc.Console.csproj"
$targetFramework = "net10.0"
$sampleDll = Join-Path $repoRoot "artifacts\bin\Samples.LiveDebuggerPoc.Console\release_net10.0\Samples.LiveDebuggerPoc.Console.dll"
$monitoringHome = Join-Path $repoRoot "artifacts\monitoring-home"
$nativeProfiler = Join-Path $monitoringHome "win-x64\Datadog.Trace.ClrProfiler.Native.dll"
$profilerGuid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
$benchmarkTypeName = "Samples.LiveDebuggerPoc.Console.Program"
$benchmarkValueMethods = @(
    "BenchmarkCheckoutRequestAsync",
    "BuildBenchmarkCart",
    "BenchmarkCalculateSubtotal",
    "BenchmarkDiscountAsync",
    "BenchmarkShippingAsync",
    "BenchmarkAuthorize",
    "BenchmarkReceiptAsync"
)

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\tmp\live-debugger-poc\benchmark"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$probeFilePath = Join-Path $OutputDirectory "benchmark-method-probes.json"

if (-not $SkipBuild) {
    dotnet build $project -c $Configuration -f $targetFramework
}

if (-not (Test-Path -LiteralPath $sampleDll)) {
    throw "Sample DLL not found: $sampleDll"
}

function Clear-ProfilerEnv {
    $env:CORECLR_ENABLE_PROFILING = $null
    $env:CORECLR_PROFILER = $null
    $env:CORECLR_PROFILER_PATH = $null
    $env:CORECLR_PROFILER_PATH_64 = $null
    $env:COR_ENABLE_PROFILING = $null
    $env:COR_PROFILER = $null
    $env:COR_PROFILER_PATH = $null
    $env:COR_PROFILER_PATH_64 = $null
    $env:DD_DOTNET_TRACER_HOME = $null
}

function Set-ProfilerEnv {
    if (-not (Test-Path -LiteralPath $nativeProfiler)) {
        throw "Native profiler not found: $nativeProfiler. Build the monitoring home first."
    }

    $env:CORECLR_ENABLE_PROFILING = "1"
    $env:CORECLR_PROFILER = $profilerGuid
    $env:CORECLR_PROFILER_PATH = $nativeProfiler
    $env:CORECLR_PROFILER_PATH_64 = $nativeProfiler
    $env:COR_ENABLE_PROFILING = "1"
    $env:COR_PROFILER = $profilerGuid
    $env:COR_PROFILER_PATH = $nativeProfiler
    $env:COR_PROFILER_PATH_64 = $nativeProfiler
    $env:DD_DOTNET_TRACER_HOME = $monitoringHome
}

function Clear-PocEnv {
    $env:DD_TRACE_LOG_DIRECTORY = $null
    $env:DD_DYNAMIC_INSTRUMENTATION_ENABLED = $null
    $env:DD_DYNAMIC_INSTRUMENTATION_PROBE_FILE = $null
    $env:DD_DYNAMIC_INSTRUMENTATION_UPLOAD_FLUSH_INTERVAL = $null
    $env:DD_DYNAMIC_INSTRUMENTATION_UPLOAD_BATCH_SIZE = $null
    $env:DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_OUTPUT_PATH = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUES = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUE_METHODS = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_EXCLUDE_METHODS = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_BUFFER_SIZE = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_VALUE_BUFFER_SIZE = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_EVENT_ENQUEUE = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_TRACE_CORRELATION = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_DISABLE_FLOW_CONTEXT = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_METHOD_REGISTRATION = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ALLOW_RECORDING_WITHOUT_OPERATION = $null
    $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_REWRITE_MODE = $null
}

function Set-CommonEnv {
    $env:DD_TRACE_ENABLED = "true"
    $env:DD_TRACE_AGENT_URL = "http://127.0.0.1:9"
    $env:DD_SERVICE = "live-debugger-flow-recorder-poc-benchmark"
    $env:DD_ENV = "local-poc"
    $env:DD_VERSION = "flow-recorder-poc"
    $env:DD_TRACE_DEBUG = if ($TraceDebugLogs) { "true" } else { "false" }
}

function New-BenchmarkMethodProbeFile {
    param(
        [string]$Path
    )

    $probes = for ($i = 0; $i -lt $benchmarkValueMethods.Count; $i++) {
        [ordered]@{
            type = "LOG_PROBE"
            language = "dotnet"
            id = "benchmark-method-probe-$i"
            version = 0
            captureSnapshot = $true
            evaluateAt = "Exit"
            where = [ordered]@{
                typeName = $benchmarkTypeName
                methodName = $benchmarkValueMethods[$i]
            }
            capture = [ordered]@{
                maxReferenceDepth = 1
                maxCollectionSize = 3
                maxLength = 256
                maxFieldCount = 20
            }
            sampling = [ordered]@{
                snapshotsPerSecond = 1000000
            }
        }
    }

    $probes | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path
    Write-Host "Wrote benchmark DI probe file: $Path"
}

function Get-BenchmarkValueMethodFilter {
    ($benchmarkValueMethods | ForEach-Object { "$benchmarkTypeName.$_" }) -join ";"
}

function Run-Variant {
    param(
        [string]$Name,
        [bool]$Profiler,
        [bool]$DynamicInstrumentation,
        [bool]$Recorder,
        [string]$CaptureMode = "off",
        [bool]$ProbeFile = $false,
        [bool]$SkipEventEnqueue = $false,
        [string]$BaselineName = "tracer-only"
    )

    Clear-ProfilerEnv
    Clear-PocEnv
    Set-CommonEnv

    if ($Profiler) {
        Set-ProfilerEnv
    }

    if ($DynamicInstrumentation) {
        $env:DD_DYNAMIC_INSTRUMENTATION_ENABLED = "true"
        $env:DD_DYNAMIC_INSTRUMENTATION_UPLOAD_FLUSH_INTERVAL = "1000"
        $env:DD_DYNAMIC_INSTRUMENTATION_UPLOAD_BATCH_SIZE = "1000"
    } else {
        $env:DD_DYNAMIC_INSTRUMENTATION_ENABLED = "false"
    }

    if ($ProbeFile) {
        $env:DD_DYNAMIC_INSTRUMENTATION_PROBE_FILE = $probeFilePath
    }

    $capturePath = Join-Path $OutputDirectory "flow-events-$Name.dflp"
    if ($Recorder) {
        $env:DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL = "true"
        $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED = "true"
        $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_OUTPUT_PATH = $capturePath
        $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_BUFFER_SIZE = "5000000"
        $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_VALUE_BUFFER_SIZE = "1000000"
    }

    if ($CaptureMode -ne "off") {
        $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUES = $CaptureMode
        $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_CAPTURE_VALUE_METHODS = Get-BenchmarkValueMethodFilter
    }

    if ($SkipEventEnqueue) {
        $env:DD_INTERNAL_DEBUGGER_FLOW_RECORDER_SKIP_EVENT_ENQUEUE = "true"
    }

    $outputPath = Join-Path $OutputDirectory "$Name.txt"
    $logDirectory = Join-Path $OutputDirectory "logs-$Name"
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
    $env:DD_TRACE_LOG_DIRECTORY = $logDirectory

    $args = @(
        $sampleDll,
        "--scenario", "benchmark",
        "--recording", "native",
        "--output", $capturePath,
        "--iterations", $Iterations.ToString(),
        "--warmup", $Warmup.ToString(),
        "--probe-install-delay-ms", $(if ($ProbeFile) { $ProbeInstallDelayMs.ToString() } else { "0" })
    )

    Write-Host "Running $Name..."
    if ($ProbeFile) {
        Write-Host "  Using DI probe file: $probeFilePath"
    }

    $output = & dotnet @args 2>&1
    $output | Set-Content -LiteralPath $outputPath
    $output | ForEach-Object { Write-Host $_ }

    $metrics = [ordered]@{
        Name = $Name
        BaselineName = $BaselineName
        OutputPath = $outputPath
        CapturePath = $capturePath
        CaptureBytes = if (Test-Path -LiteralPath $capturePath) { (Get-Item -LiteralPath $capturePath).Length } else { 0 }
        LogDirectory = $logDirectory
        ProbeFile = $ProbeFile
        SkipEventEnqueue = $SkipEventEnqueue
    }

    foreach ($line in $output) {
        if ($line -match '^Wrote\s+(\d+)\s+flow events to\s+(.+)$') {
            $metrics["EventCount"] = $matches[1]
            $metrics["CapturePath"] = $matches[2]
        } elseif ($line -match '^Dropped events:\s*(\d+)$') {
            $metrics["DroppedEvents"] = $matches[1]
        } elseif ($line -match '^([^:]+):\s*(.+)$') {
            $metrics[$matches[1]] = $matches[2]
        }
    }

    [pscustomobject]$metrics
}

New-BenchmarkMethodProbeFile -Path $probeFilePath

$variants = @(
    @{ Name = "baseline-no-profiler"; Profiler = $false; DynamicInstrumentation = $false; Recorder = $false; CaptureMode = "off"; ProbeFile = $false; SkipEventEnqueue = $false; BaselineName = "baseline-no-profiler" },
    @{ Name = "tracer-only"; Profiler = $true; DynamicInstrumentation = $false; Recorder = $false; CaptureMode = "off"; ProbeFile = $false; SkipEventEnqueue = $false; BaselineName = "tracer-only" },
    @{ Name = "di-method-probes-values"; Profiler = $true; DynamicInstrumentation = $true; Recorder = $false; CaptureMode = "off"; ProbeFile = $true; SkipEventEnqueue = $false; BaselineName = "tracer-only" },
    @{ Name = "flow-recorder-values"; Profiler = $true; DynamicInstrumentation = $true; Recorder = $true; CaptureMode = "all"; ProbeFile = $false; SkipEventEnqueue = $false; BaselineName = "tracer-only" }
)

if ($IncludeDiagnosticVariants) {
    $variants += @(
        @{ Name = "flow-recorder-values-skip-enqueue"; Profiler = $true; DynamicInstrumentation = $true; Recorder = $true; CaptureMode = "all"; ProbeFile = $false; SkipEventEnqueue = $true; BaselineName = "tracer-only" }
    )
}

$results = foreach ($variant in $variants) {
    Run-Variant @variant
}

$noProfilerBaseline = $results | Where-Object { $_.Name -eq "baseline-no-profiler" } | Select-Object -First 1
$tracerBaseline = $results | Where-Object { $_.Name -eq "tracer-only" } | Select-Object -First 1
$noProfilerThroughput = if ($noProfilerBaseline) { [double]$noProfilerBaseline.ThroughputPerSecond } else { 0.0 }
$tracerThroughput = if ($tracerBaseline) { [double]$tracerBaseline.ThroughputPerSecond } else { 0.0 }
$summary = $results | ForEach-Object {
    $throughput = [double]$_.ThroughputPerSecond
    $overheadVsNoProfiler = if ($noProfilerThroughput -gt 0 -and $throughput -gt 0) {
        (($noProfilerThroughput / $throughput) - 1.0) * 100.0
    } else {
        0.0
    }

    $overheadVsTracer = if ($tracerThroughput -gt 0 -and $throughput -gt 0) {
        (($tracerThroughput / $throughput) - 1.0) * 100.0
    } else {
        0.0
    }

    [pscustomobject]@{
        Variant = $_.Name
        ThroughputPerSecond = [math]::Round($throughput, 3)
        OverheadVsNoProfilerPercent = [math]::Round($overheadVsNoProfiler, 2)
        OverheadVsTracerOnlyPercent = [math]::Round($overheadVsTracer, 2)
        P50Us = $_.P50Us
        P95Us = $_.P95Us
        P99Us = $_.P99Us
        AllocatedBytesPerRequest = $_.AllocatedBytesPerRequest
        Gen0 = $_.Gen0Collections
        Events = $_.EventCount
        DroppedEvents = $_.DroppedEvents
        FlushElapsedMs = $_.FlushElapsedMs
        CaptureBytes = $_.CaptureBytes
        LogDirectory = $_.LogDirectory
        ProbeFile = $_.ProbeFile
        SkipEventEnqueue = $_.SkipEventEnqueue
    }
}

$summaryPath = Join-Path $OutputDirectory "summary.csv"
$summary | Export-Csv -NoTypeInformation -Path $summaryPath
$summary | Format-Table -AutoSize

Write-Host "Benchmark output directory: $OutputDirectory"
Write-Host "Benchmark summary: $summaryPath"
Clear-ProfilerEnv
Clear-PocEnv
