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

# Run the benchmark, retrying if BenchmarkDotNet failed to build.
#
# csc.exe intermittently dies with STATUS_HEAP_CORRUPTION (exit -1073740940) during BenchmarkDotNet's
# build phase. BenchmarkDotNet still exits 0 in that case: it simply records the affected benchmarks as
# having no results. Left unchecked that either produces an empty results file (which fails the
# downstream converter and reds the pipeline) or a partial one that is silently published as real data.
# So treat a build failure as a failure regardless of exit code.
#
# The backoff matters. Observed failure windows last two to three minutes and affect every build
# partition of a job while they last, so an immediate retry lands in the same window.
$resultsDir = "$localArtifactsDir\results"
$maxAttempts = 3
$attempt = 1

while ($true) {
    if ($attempt -gt 1) {
        $backoffSeconds = 60 * ($attempt - 1)
        Write-Warning "Retrying benchmark run in $backoffSeconds seconds (attempt $attempt of $maxAttempts)"
        Start-Sleep -Seconds $backoffSeconds
    }

    if (Test-Path $resultsDir) {
        Remove-Item -Recurse -Force $resultsDir
    }

    $runLog = "$localArtifactsDir\benchmark-run-attempt-$attempt.log"
    & $benchmarkExe @arguments 2>&1 | Tee-Object -FilePath $runLog
    $exitCode = $LASTEXITCODE

    $buildFailed = [bool](Select-String -Path $runLog -SimpleMatch "failed to build the auto-generated boilerplate code" -Quiet)

    # Count benchmarks that actually produced measurements, and report files that produced none.
    # BenchmarkDotNet writes an entry into Benchmarks[] for every case it attempted, including ones
    # that threw in setup, so the entry count says nothing about whether there is any data. The
    # downstream converter fails with "Failed to collect even one benchmark results" when a report
    # has no measurements, so a per-file check is what matches its behaviour - summing across files
    # would let a wholly empty report ride along with a healthy one.
    $measured = 0
    $emptyReports = @()
    if (Test-Path $resultsDir) {
        foreach ($report in Get-ChildItem -Path $resultsDir -Filter "*-report-full-compressed.json" -Recurse) {
            $withData = @((Get-Content $report.FullName -Raw | ConvertFrom-Json).Benchmarks |
                          Where-Object { $_.Measurements -and @($_.Measurements).Count -gt 0 })
            if ($withData.Count -eq 0) {
                $emptyReports += $report.Name
            }
            $measured += $withData.Count
        }
    }

    if ($exitCode -eq 0 -and -not $buildFailed -and $measured -gt 0 -and $emptyReports.Count -eq 0) {
        Write-Output "Benchmark run succeeded on attempt $attempt ($measured measured benchmark(s))"
        break
    }

    Write-Warning "Benchmark run attempt $attempt failed (exit code: $exitCode, build failure: $buildFailed, measured: $measured, empty reports: $($emptyReports -join ', '))"

    if ($attempt -ge $maxAttempts) {
        Write-Error "Benchmark run failed after $maxAttempts attempts. Refusing to publish incomplete results."
        exit 1
    }

    $attempt++
}

# Copy results to ARTIFACTS_DIR with naming convention
# Format: candidate.Trace.SpanBenchmark.json
if (Test-Path $resultsDir) {
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
