# Set environment variables for run-benchmarks.ps1 testing
$env:BASELINE_OR_CANDIDATE = "candidate"
$env:CODE_SRC = "C:\app\candidate"
$env:PARALLEL_ITEM = "*AgentWriterBenchmarks*"
$env:PARALLEL_INDEX = "1"
$env:ARTIFACTS_DIR = "C:\artifacts"

Write-Output "BASELINE_OR_CANDIDATE: $env:BASELINE_OR_CANDIDATE"
Write-Output "CODE_SRC: $env:CODE_SRC"
Write-Output "PARALLEL_ITEM: $env:PARALLEL_ITEM"
Write-Output "PARALLEL_INDEX: $env:PARALLEL_INDEX"
Write-Output "ARTIFACTS_DIR: $env:ARTIFACTS_DIR"
