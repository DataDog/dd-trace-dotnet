# Plan: Modernize dd-trace-dotnet Microbenchmarks with bp-runner

## Goal

Replace the current shell-script-based benchmark execution with bp-runner, enabling parallelization and better control from the dd-trace-dotnet repo.

## Status Summary

| Phase | Description | Status |
|-------|-------------|--------|
| Phase 1 | Minimal validation (single benchmark, dry run) | ✅ DONE |
| Phase 2 | Parallelization (3 benchmark categories) | ✅ DONE |
| Phase 2.5 | S3 upload & fetch | 🔄 Testing |
| Phase 3 | Results analysis | Pending |
| Phase 5 | PR comments & analysis | Pending |

## What's Working Now

- **bp-runner.windows.yml** runs 3 parallel benchmark categories with `--job dry` on net6.0
- **instance.yml** provisions Windows instance, clones repo, builds tracer once, runs bp-runner
- **microbenchmarks.yml** triggers via bp-infra with `--env-regex`
- Test AMI from benchmarking-platform has bp-runner pre-installed
- Parallelization: `*SpanBenchmark*`, `*AgentWriterBenchmark*`, `*AspNetCoreBenchmark*`

**Phase 2 success**: Commit `75b774f225` - parallelization working

## Files Created/Modified

| File | Status |
|------|--------|
| `.gitlab/benchmarks/bp-runner.windows.yml` | ✅ Created |
| `.gitlab/benchmarks/infrastructure/instance.yml` | ✅ Created |
| `.gitlab/benchmarks/microbenchmarks.yml` | ✅ Modified |

## Available Benchmark TFMs on AMI

The AMI has these .NET versions installed:
- .NET Core 3.1.32 ✓
- .NET 6.0.36 ✓ (currently used)
- .NET 10.0.0 ✓ (preview)

**Note**: .NET 8.0 is NOT installed, so we target net6.0.

## Phase 2: Run All Benchmarks

### Goal
Expand from single dry-run benchmark to full benchmark suite.

### Changes Required

1. **Update bp-runner.windows.yml** - remove filter, keep `--job dry` for fast iteration:
   ```yaml
   dotnet run -c Release -f net6.0 -- `
     --job dry `
     --exporters json
   ```

2. **Consider multi-TFM runs** - run benchmarks on net6.0 and netcoreapp3.1:
   ```yaml
   # Option A: Run all on net6.0 only
   # Option B: Run on multiple TFMs (requires build changes)
   ```

### Benchmark Categories to Run

From `Benchmarks.Trace.csproj`:
- Core: `SpanBenchmark`, `ActivityBenchmark`, `AgentWriterBenchmark`, `TraceProcessorBenchmark`
- Integrations: `AspNetCoreBenchmark`, `HttpClientBenchmark`, `DbCommandBenchmark`, `RedisBenchmark`, etc.
- Security: `AppSecWafBenchmark`, `AppSecBodyBenchmark`, `AppSecEncodeBenchmark`
- Logging: `Log4netBenchmark`, `NLogBenchmark`, `SerilogBenchmark`, `ILoggerBenchmark`
- IAST: `StringAspectsBenchmark`
- CI Visibility: `CIVisibilityProtocolWriterBenchmark`

### Ignored Benchmarks (from check-big-regressions)

These are marked as unstable and ignored in regression checks:
- `StringAspectsBenchmark`
- `CharSliceBenchmark`
- `AppSecWafBenchmark`
- `AppSecBodyBenchmark`
- `CIVisibilityProtocolWriterBenchmark`

## Phase 3: S3 Upload & Results

### Goal
Upload benchmark results to S3 for persistence and baseline comparison.

### Implementation Options

**Option A: bp-runner handles upload** (preferred)
- Add `upload_results` step in bp-runner config
- bp-runner already has S3 integration

**Option B: instance.yml handles upload**
- Add `upload_results` provision step
- Use AWS PowerShell cmdlets

### S3 Structure
```
s3://windows-benchmarking-results-us-east-1/
  dd-trace-dotnet/
    master/           # Latest master results (baseline)
    {branch}/         # Per-branch results
      {job_id}/       # Per-job results
        reports/
          *.json
```

## Phase 4: Parallelization

### Goal
Run benchmarks in parallel across multiple instances to reduce CI time.

### Strategy

Split by benchmark category:
1. **Core tracing** - SpanBenchmark, AgentWriterBenchmark, TraceProcessorBenchmark
2. **Integrations** - AspNetCore, HttpClient, DbCommand, Redis, etc.
3. **Security** - AppSec benchmarks
4. **Logging** - All logging benchmarks
5. **Other** - IAST, CI Visibility

### bp-runner Parallelization

```yaml
experiments:
  - name: dd-trace-dotnet-microbenchmarks
    steps:
      - name: run-microbenchmarks
        run: run_microbenchmarks
        parallelize:
          by: benchmark_category  # or by filter pattern
          instances: 5
```

## Phase 5: PR Comments & Analysis

### Goal
Post benchmark comparison results as PR comments.

### Components

1. **Download baseline** - Fetch master results from S3
2. **Compare results** - Use BenchmarkDotNet comparison or custom analysis
3. **Generate report** - Markdown summary of regressions/improvements
4. **Post comment** - Use GitHub API to post PR comment

### Regression Thresholds

From `check-big-regressions` job:
- Significant regression: >10% slowdown
- Major regression: >25% slowdown (blocks PR)

## Dependencies

- **bp-runner**: `augusto/run-microbenchmarks-parallelize-windows` branch
- **bp-infra**: For Windows instance provisioning
- **Test AMI**: `/windows-benchmarking/dd-trace-dotnet-microbenchmarks-ami-test-id`

## Migration Path

1. ✅ Phase 1 complete - basic validation working
2. Phase 2 - Expand to all benchmarks
3. Phase 3 - Add S3 persistence
4. Phase 4 - Add parallelization
5. Phase 5 - Add PR comments
6. **Merge bp-runner changes to main**
7. Update AMI to use main branch bp-runner
8. Update production AMI SSM parameter
9. Remove old benchmarking-platform scripts
