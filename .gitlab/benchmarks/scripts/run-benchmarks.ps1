# run-benchmarks.ps1
#
# Runs pre-built benchmark executables directly, bypassing Nuke.
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

if (-not $env:CODE_SRC) {
    Write-Error "CODE_SRC environment variable is not set"
    exit 1
}

if (-not $Filter) {
    Write-Error "PARALLEL_ITEM environment variable (or -Filter parameter) is not set"
    exit 1
}

if (-not $ArtifactsIndex) {
    Write-Error "PARALLEL_INDEX environment variable (or -ArtifactsIndex parameter) is not set"
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

# Source bin folder (built by Nuke in how_to_fetch_release)
$sourceBinDir = "$benchmarkProjectDir\bin\Release\$hostFramework"

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

# Build BenchmarkDotNet arguments
$arguments = @("-r") + $runtimes + @(
    "-m",
    "-f", $Filter,
    "--allCategories", $Category,
    "--iterationTime", "200",
    "--launchCount", "5",
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
& $benchmarkExe @arguments

if ($LASTEXITCODE -ne 0) {
    Write-Error "Benchmark execution failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Copy results to ARTIFACTS_DIR with naming convention
# Format: candidate.Trace.SpanBenchmark.json
$resultsDir = "$localArtifactsDir\results"
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
