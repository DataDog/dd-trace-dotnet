# Benchmarks

Benchmark pipelines for dd-trace-dotnet. Triggered from GitLab CI.

## Macrobenchmarks (`macrobenchmarks.yml`)

Throughput tests on Linux. Uses shared k8s infrastructure from benchmarking-platform.

Flow:
1. Azure DevOps builds the tracer artifacts
2. GitLab waits for build, downloads via S3
3. Runs throughput tests on k8s pods
4. Results compared against baseline, SLO breach check

## Microbenchmarks (`microbenchmarks.yml`)

BenchmarkDotNet tests on Windows ephemeral instances.

Flow:
1. GitLab spins up Windows EC2 instance from pre-built AMI
2. `bp-runner` clones repo, builds tracer, runs benchmarks
3. Results uploaded to S3, compared baseline vs candidate
4. Instance terminated after run

### Ephemeral Infrastructure

- **AMI**: Pre-built Windows image with .NET SDKs, Visual Studio, bp-runner
- **bp-runner**: Orchestrates parallel benchmark execution on isolated CPU sets
- **S3**: Passes artifacts between GitLab runner and ephemeral instances

Config files:
- `bp-runner.windows.yml` - Benchmark execution config (batches, filters, scripts)
- `infrastructure/instance.yml` - EC2 instance provisioning

## Key Files

| File | Purpose |
|------|---------|
| `macrobenchmarks.yml` | Linux throughput pipeline |
| `microbenchmarks.yml` | Windows microbenchmark pipeline |
| `bp-runner.windows.yml` | bp-runner config for micros |
| `scripts/run-benchmarks.ps1` | Runs BenchmarkDotNet on Windows |
| `steps/` | Shared pipeline step scripts |
