# Runs pre-built benchmark executables directly, bypassing Nuke.
#
# Is called by running "bp-runner bp-runner.windows.yml" on the CI.
#
# This script exists because parallel bp-runner invocations would race to
# compile/load Nuke's _build.dll, causing file lock errors. By running the
# benchmark .exe directly, we avoid Nuke entirely for parallel runs.
#
# Additionally, BenchmarkDotNet generates code in a subfolder of the exe's
# directory. To avoid file lock conflicts between parallel runs, this script
# copies the benchmark binaries to an isolated directory per parallel run.
#
# This script mimics the setup from Build.cs RunBenchmarks target:
# - Sets required environment variables (DD_SERVICE, DD_ENV, DD_TRACER_HOME, etc.)
# - Constructs BenchmarkDotNet CLI arguments (-r, -f, --allCategories, etc.)
# - Runs the benchmark executable
#
# Required environment variables:
#   CODE_SRC - Path to the repository root
#   PARALLEL_ITEM - Benchmark filter pattern (e.g., "*SpanBenchmark*")
#   BENCHMARK_CATEGORY - Category to run (e.g., "prs", "master")
#   BENCHMARK_PROJECT - Which project to run (e.g., "Benchmarks.Trace")
#   PARALLEL_INDEX - Index for artifact directory isolation
#   BASELINE_OR_CANDIDATE - "candidate" or "baseline"
#   ARTIFACTS_DIR - Where to copy final results

param(
    [string]$Filter = $env:PARALLEL_ITEM,
    [string]$Category = $env:BENCHMARK_CATEGORY,
    [string]$Project = $env:BENCHMARK_PROJECT,
    [string]$ArtifactsIndex = $env:PARALLEL_INDEX
)

$ErrorActionPreference = "Stop"

# Validate all required environment variables
$missingEnvVars = @()

if (-not $env:CODE_SRC) {
    $missingEnvVars += "CODE_SRC"
}

if (-not $Filter) {
    $missingEnvVars += "PARALLEL_ITEM (or -Filter parameter)"
}

if (-not $Category) {
    $missingEnvVars += "BENCHMARK_CATEGORY (or -Category parameter)"
}

if (-not $Project) {
    $missingEnvVars += "BENCHMARK_PROJECT (or -Project parameter)"
}

if (-not $ArtifactsIndex) {
    $missingEnvVars += "PARALLEL_INDEX (or -ArtifactsIndex parameter)"
}

if (-not $env:BASELINE_OR_CANDIDATE) {
    $missingEnvVars += "BASELINE_OR_CANDIDATE"
}

if (-not $env:ARTIFACTS_DIR) {
    $missingEnvVars += "ARTIFACTS_DIR"
}

if ($missingEnvVars.Count -gt 0) {
    Write-Error "Missing required environment variables:`n  - $($missingEnvVars -join "`n  - ")"
    exit 1
}

# Paths
$tracerRoot = "$env:CODE_SRC\tracer"
$monitoringHome = "$tracerRoot\bin\monitoring-home"
$benchmarkProjectDir = "$tracerRoot\test\benchmarks\$Project"
$localArtifactsDir = "$tracerRoot\artifacts\benchmarks\$ArtifactsIndex"

# Framework to run the host process (the benchmark exe)
# Must match what was built in how_to_fetch_release
$hostFramework = "net6.0"

# Target runtimes for BenchmarkDotNet to benchmark against
# Must use full TFM format with dots for BenchmarkDotNet CLI
# Matches Build.cs: on Windows, benchmarks run against net472, netcoreapp3.1, net6.0
$runtimes = @("net472", "netcoreapp3.1", "net6.0")

# Source bin folder (built by Nuke in how_to_fetch_release).
# Under UseArtifactsOutput (set in tracer/Directory.Build.props) the per-project
# bin output lives at <repo>/artifacts/bin/<Project>/<config>_<tfm>/.
$sourceBinDir = "$env:CODE_SRC\artifacts\bin\$Project\release_$hostFramework"

if (-not (Test-Path "$sourceBinDir\$Project.exe")) {
    Write-Error "Benchmark executable not found at: $sourceBinDir\$Project.exe"
    Write-Error "Make sure BuildBenchmarks was run in how_to_fetch_release"
    exit 1
}

# Copy bin folder to unique location per parallel run to avoid BenchmarkDotNet
# code generation conflicts. Each parallel run generates code in a subfolder
# of the exe's directory, so they must be isolated.
$runDir = "$tracerRoot\benchmarks-run\$ArtifactsIndex"
Write-Output "Copying benchmark binaries to isolated run directory: $runDir"
if (Test-Path $runDir) {
    Remove-Item -Recurse -Force $runDir
}
Copy-Item -Path $sourceBinDir -Destination $runDir -Recurse -Force

$benchmarkExe = "$runDir\$Project.exe"

# Ensure artifacts directory exists
New-Item -ItemType Directory -Path $localArtifactsDir -Force | Out-Null

$diagnosticsEnabled = $env:BENCHMARK_DIAGNOSTICS -eq "true" -and $Filter -like "*TraceAnnotationsBenchmark*"
$diagnosticsJob = $null
$diagnosticsDir = "$localArtifactsDir\diagnostics"

if ($diagnosticsEnabled) {
    $hostTraceDir = "$diagnosticsDir\host-traces"
    New-Item -ItemType Directory -Path $hostTraceDir -Force | Out-Null

    $env:DOTNET_HOST_TRACE = "1"
    $env:DOTNET_HOST_TRACE_VERBOSITY = "4"
    $env:DOTNET_HOST_TRACEFILE = $hostTraceDir
    $env:COREHOST_TRACE = "1"
    $env:COREHOST_TRACE_VERBOSITY = "4"
    $env:COREHOST_TRACEFILE = "$diagnosticsDir\corehost.log"

    $diagnosticLabel = if ($env:BENCHMARK_DIAGNOSTIC_LABEL) {
        $env:BENCHMARK_DIAGNOSTIC_LABEL
    } else {
        $ArtifactsIndex
    }

    $context = [ordered]@{
        label = $diagnosticLabel
        timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
        filter = $Filter
        artifacts_index = $ArtifactsIndex
        requested_cpus = $env:PARALLEL_CPUS
        numa_node = $env:BENCHMARK_DIAGNOSTIC_NODE
        attempt = $env:BENCHMARK_DIAGNOSTIC_ATTEMPT
        order = $env:BENCHMARK_DIAGNOSTIC_ORDER
        processor_affinity = (Get-Process -Id $PID).ProcessorAffinity.ToInt64()
        computer_system = $null
        operating_system = $null
        page_files = $null
        cim_error = $null
    }

    try {
        $context.computer_system = Get-CimInstance Win32_ComputerSystem |
            Select-Object TotalPhysicalMemory, NumberOfLogicalProcessors, NumberOfProcessors
        $context.operating_system = Get-CimInstance Win32_OperatingSystem |
            Select-Object Caption, Version, BuildNumber, FreePhysicalMemory, FreeVirtualMemory, TotalVirtualMemorySize
        $context.page_files = Get-CimInstance Win32_PageFileUsage |
            Select-Object Name, AllocatedBaseSize, CurrentUsage, PeakUsage
    } catch {
        $context.cim_error = $_.Exception.Message
    }

    $context | ConvertTo-Json -Depth 4 | Set-Content "$diagnosticsDir\context.json"

    $samplesPath = "$diagnosticsDir\memory-samples.jsonl"
    $diagnosticsJob = Start-Job -ArgumentList $samplesPath -ScriptBlock {
        param($outputPath)

        $processFilter = "Name = 'csc.exe' OR Name = 'dotnet.exe' OR Name = 'msbuild.exe' OR Name = 'vbcscompiler.exe' OR Name LIKE '%Benchmark%'"

        while ($true) {
            $sample = [ordered]@{
                timestamp_utc = (Get-Date).ToUniversalTime().ToString("o")
                counters = $null
                processes = @()
                error = $null
            }

            try {
                $cimProcesses = @{}
                Get-CimInstance Win32_Process -Filter $processFilter -ErrorAction Stop |
                    ForEach-Object {
                        $cimProcesses[[int]$_.ProcessId] = $_
                    }

                $counterSet = Get-Counter -Counter @(
                    "\Memory\Committed Bytes",
                    "\Memory\Commit Limit",
                    "\Memory\Available Bytes",
                    "\Paging File(_Total)\% Usage",
                    "\Paging File(_Total)\% Usage Peak"
                ) -ErrorAction Stop

                $counterValues = [ordered]@{}
                foreach ($counter in $counterSet.CounterSamples) {
                    $counterValues[$counter.Path] = $counter.CookedValue
                }
                $sample.counters = $counterValues

                $sample.processes = @(Get-Process -ErrorAction SilentlyContinue |
                    Where-Object {
                        $_.ProcessName -match "^(csc|dotnet|msbuild|vbcscompiler)$" -or
                        $_.ProcessName -like "*Benchmark*"
                    } |
                    ForEach-Object {
                        $processAffinity = $null
                        $cimProcess = $cimProcesses[$_.Id]
                        try {
                            $processAffinity = $_.ProcessorAffinity.ToInt64()
                        } catch {
                            $processAffinity = "unavailable: $($_.Exception.Message)"
                        }

                        [ordered]@{
                            id = $_.Id
                            name = $_.ProcessName
                            parent_process_id = $cimProcess.ParentProcessId
                            processor_affinity = $processAffinity
                            private_bytes = $_.PrivateMemorySize64
                            virtual_bytes = $_.VirtualMemorySize64
                            working_set_bytes = $_.WorkingSet64
                        }
                    })
            } catch {
                $sample.error = $_.Exception.Message
            }

            $sample | ConvertTo-Json -Compress -Depth 4 | Add-Content $outputPath
            Start-Sleep -Seconds 1
        }
    }

    Write-Output "Diagnostics enabled: $diagnosticLabel"
    Write-Output "Host traces: $hostTraceDir"
    Write-Output "Memory samples: $samplesPath"
}

# Set environment variables (mimics Build.cs)
$env:DD_SERVICE = "dd-trace-dotnet"
$env:DD_ENV = "CI"
$env:DD_DOTNET_TRACER_HOME = $monitoringHome
$env:DD_TRACER_HOME = $monitoringHome

# CI Visibility ships benchmark results to Datadog via the in-process tracer.
# The ephemeral benchmarking VM does not run a Datadog Agent, so route directly
# to intake via agentless mode. DD_API_KEY is forwarded from the GitLab job.
if ($env:DD_API_KEY) {
    $env:DD_CIVISIBILITY_AGENTLESS_ENABLED = "1"
    if (-not $env:DD_SITE) {
        $env:DD_SITE = "datadoghq.com"
    }
    Write-Output "CI Visibility agentless mode enabled (site: $env:DD_SITE)"
} else {
    Write-Warning "DD_API_KEY not set; CI Visibility data will not be sent to Datadog"
}

# Build BenchmarkDotNet arguments
$arguments = @("-r") + $runtimes + @(
    "-m",
    "-f", $Filter,
    "--allCategories", $Category,
    # We change this manually on benchmark methods from 200 ms to 500 ms on 
    # less stable benchmarks with "[IterationTime(500)]"
    "--iterationTime", "200",
    "--launchCount", "5",
    "--warmupCount", "10",
    "--iterationCount", "10",
    "--buildTimeout", "3600",
    "--keepFiles",
    "--artifacts", $localArtifactsDir
)

Write-Output "=== Running benchmarks ==="
Write-Output "Project: $Project"
Write-Output "Filter: $Filter"
Write-Output "Category: $Category"
Write-Output "Runtimes: $($runtimes -join ' ')"
Write-Output "Executable: $benchmarkExe"
Write-Output "Artifacts: $localArtifactsDir"
Write-Output "Arguments: $($arguments -join ' ')"
Write-Output ""

# Run the benchmark
$benchmarkExitCode = 0
$benchmarkError = $null
try {
    & $benchmarkExe @arguments
    $benchmarkExitCode = $LASTEXITCODE
} catch {
    $benchmarkError = $_
    $benchmarkExitCode = 1
} finally {
    if ($diagnosticsJob) {
        Stop-Job $diagnosticsJob -ErrorAction SilentlyContinue
        Wait-Job $diagnosticsJob -ErrorAction SilentlyContinue | Out-Null
        $jobOutput = Receive-Job $diagnosticsJob -ErrorAction SilentlyContinue
        if ($jobOutput) {
            $jobOutput | Out-String | Set-Content "$diagnosticsDir\sampler-job.log"
        }
        Remove-Job $diagnosticsJob -Force -ErrorAction SilentlyContinue

        Set-Content "$localArtifactsDir\diagnostic-exit-code.txt" $benchmarkExitCode

        $diagnosticsDestination = "$env:ARTIFACTS_DIR\diagnostics-$ArtifactsIndex"
        if (Test-Path $diagnosticsDestination) {
            Remove-Item -Recurse -Force $diagnosticsDestination
        }
        Copy-Item $localArtifactsDir $diagnosticsDestination -Recurse -Force
        Write-Output "Copied diagnostics to $diagnosticsDestination"
    }
}

if ($env:BENCHMARK_DIAGNOSTIC_ONLY -eq "true") {
    Set-Content "$env:ARTIFACTS_DIR\diagnostic-exit-$ArtifactsIndex.txt" $benchmarkExitCode
}

if ($benchmarkExitCode -ne 0) {
    $failureMessage = "$ArtifactsIndex exited with code $benchmarkExitCode"
    Set-Content "$env:ARTIFACTS_DIR\benchmark-failure-$ArtifactsIndex.txt" $failureMessage

    if ($env:BENCHMARK_DIAGNOSTIC_ONLY -eq "true" -or $env:BENCHMARK_CONTINUE_ON_FAILURE -eq "true") {
        Write-Warning "Benchmark failed with exit code $benchmarkExitCode; continuing with remaining controls"
    } elseif ($benchmarkError) {
        throw $benchmarkError
    } else {
        Write-Error "Benchmark execution failed with exit code $benchmarkExitCode"
        exit $benchmarkExitCode
    }
}

# Copy results to ARTIFACTS_DIR with naming convention
# Format: candidate.Trace.SpanBenchmark.json
$resultsDir = "$localArtifactsDir\results"
if ($env:BENCHMARK_DIAGNOSTIC_ONLY -eq "true") {
    Write-Output "Diagnostic-only run: retaining the normal parallel result as the candidate result"
} elseif (Test-Path $resultsDir) {
    $jsonFiles = Get-ChildItem -Path $resultsDir -Filter "*.json" -Recurse
    foreach ($file in $jsonFiles) {
        # Extract benchmark name: Benchmarks.Trace.SpanBenchmark-report-full-compressed.json -> Trace.SpanBenchmark
        $benchmarkName = $file.BaseName -replace '^Benchmarks\.', '' -replace '-report(-full)?(-compressed)?$', ''
        $destName = "$env:BASELINE_OR_CANDIDATE.$benchmarkName.json"
        $destPath = "$env:ARTIFACTS_DIR\$destName"
        Write-Output "Copying $($file.Name) -> $destName"
        Copy-Item $file.FullName -Destination $destPath -Force
    }
} else {
    Write-Warning "No results directory found at $resultsDir"
}

Write-Output "=== Benchmarks completed ==="
