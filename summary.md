# dd-trace-dotnet Microbenchmarks Summary

## Benchmark Projects

There are **3 benchmark projects** in the repository:

| Project | Location | Description |
|---------|----------|-------------|
| `Benchmarks.Trace` | `tracer/test/benchmarks/Benchmarks.Trace/` | Main tracer microbenchmarks (spans, integrations, ASM, IAST) |
| `Benchmarks.OpenTelemetry.Api` | `tracer/test/benchmarks/Benchmarks.OpenTelemetry.Api/` | OpenTelemetry API benchmarks (baseline, no instrumentation) |
| `Benchmarks.OpenTelemetry.InstrumentedApi` | `tracer/test/benchmarks/Benchmarks.OpenTelemetry.InstrumentedApi/` | OpenTelemetry API with Datadog instrumentation |

## Entrypoint

All benchmark projects use `BenchmarkSwitcher.FromAssembly()` in their `Program.cs`, which allows selecting benchmarks via command-line arguments.

**Main entrypoint pattern** (`Program.cs`):

```csharp
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
```

## How Benchmarks Are Split

### By Category

Benchmarks are categorized using `[BenchmarkCategory]` attributes:

| Category | Constant | Purpose |
|----------|----------|---------|
| `tracer` | `Constants.TracerCategory` | Core tracing benchmarks |
| `appsec` | `Constants.AppSecCategory` | AppSec/IAST benchmarks |
| `prs` | `Constants.RunOnPrs` | Run on pull requests |
| `master` | `Constants.RunOnMaster` | Run on master branch |

### Benchmark Classes in `Benchmarks.Trace`

**Tracer Category:**

- `SpanBenchmark` - Span creation/finish
- `ActivityBenchmark` - Activity API
- `AspNetCoreBenchmark` - ASP.NET Core integration (2 classes)
- `AgentWriterBenchmark` - Agent writing
- `HttpClientBenchmark` - HTTP client instrumentation
- `GraphQLBenchmark` - GraphQL integration
- `ElasticsearchBenchmark` - Elasticsearch integration
- `RedisBenchmark` - Redis integration
- `DbCommandBenchmark` - Database commands
- `TraceAnnotationsBenchmark` - Trace annotations
- `ILoggerBenchmark` / `NLogBenchmark` / `Log4netBenchmark` / `SerilogBenchmark` - Logging integrations
- `CIVisibilityProtocolWriterBenchmark` - CI Visibility
- `CharSliceBenchmark` - Internal utilities
- `TraceProcessorBenchmark` - Trace processing

**AppSec Category:**

- `AppSecWafBenchmark` - WAF execution (uses `[IterationSetup]` with GC)
- `AppSecBodyBenchmark` - Body parsing
- `AppSecEncodeBenchmark` - Encoding
- `StringAspectsBenchmark` - IAST string aspects

## Running Benchmarks

### Via Nuke Build System (CI)

```bash
# From tracer directory
./build.sh RunBenchmarks
```

Build system command in `Build.cs` line ~636:

```csharp
.SetApplicationArguments($"-r {runtimes} -m -f {Filter ?? "*"} --allCategories {categories} --iterationTime 200")
```

### Directly via dotnet CLI

**Run all benchmarks:**

```bash
cd tracer/test/benchmarks/Benchmarks.Trace
dotnet run -c Release
```

**Run a single benchmark class:**

```bash
dotnet run -c Release -- --filter "*SpanBenchmark*"
```

**Run a single benchmark method:**

```bash
dotnet run -c Release -- --filter "*SpanBenchmark.StartFinishSpan"
```

**Run by category:**

```bash
dotnet run -c Release -- --allCategories tracer
dotnet run -c Release -- --allCategories prs
```

**Run on specific runtime:**

```bash
dotnet run -c Release -- --runtimes net6.0 net8.0
dotnet run -c Release -- -r net6.0
```

## Fast Run Options

### BenchmarkDotNet Built-in Jobs

**ShortRunJob (fastest):**

```bash
dotnet run -c Release -- --job short --filter "*SpanBenchmark*"
```

**DryJob (verification only):**

```bash
dotnet run -c Release -- --job dry --filter "*SpanBenchmark*"
```

**MediumRunJob:**

```bash
dotnet run -c Release -- --job medium --filter "*SpanBenchmark*"
```

### Custom Fast Configuration (Code)

Add to `Program.cs` for a quick job:

```csharp
// Option 1: ShortRunJob attribute on benchmark class
[ShortRunJob]
public class MyBenchmark { ... }

// Option 2: SimpleJob with reduced iterations
[SimpleJob(launchCount: 1, warmupCount: 3, iterationCount: 5)]
public class MyBenchmark { ... }

// Option 3: ManualConfig in Program.cs
var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun)  // or Job.Dry for fastest
    .WithDatadog()
    .AddExporter(JsonExporter.FullCompressed);
```

### Command Line Fast Options

```bash
# Fastest: dry job (minimal iterations)
dotnet run -c Release -- --job dry --filter "*SpanBenchmark*"

# Short run job (reduced iterations)
dotnet run -c Release -- --job short --filter "*SpanBenchmark*"

# Custom iteration count
dotnet run -c Release -- --iterationCount 3 --warmupCount 1 --filter "*SpanBenchmark*"

# Custom iteration time (ms)
dotnet run -c Release -- --iterationTime 100 --filter "*SpanBenchmark*"
```

### Recommended Fast Iteration Command

```bash
# Single benchmark, dry run, single runtime
dotnet run -c Release -f net8.0 -- --job dry --filter "*SpanBenchmark.StartFinishSpan" -r net8.0
```

## CI Pipeline

The microbenchmarks CI is defined in `.gitlab/benchmarks/microbenchmarks.yml`:

1. **Build Stage**: `build-dd-trace-dotnet-microbenchmarks-ami` - Builds Windows AMI
2. **Benchmarks Stage**: `run-benchmarks` - Runs benchmarks on ephemeral Windows instances
3. **Gate Stage**: `check-big-regressions` - Checks for performance regressions

Key CI parameters:

- Results stored in: `windows-benchmarking-results-us-east-1` S3 bucket
- Branch: `BP_INFRA_BENCHMARKING_PLATFORM_BRANCH: "dd-trace-dotnet/micro"`
- Analysis uses: `bp-runner.fail-on-breach.yml`

## Ignored Benchmarks (Flaky)

From `microbenchmarks.yml`:

```
IGNORED_BENCHMARKS_REGEX: "Trace.Iast.StringAspectsBenchmark|Trace.CharSliceBenchmark|Trace.Asm.AppSecWafBenchmark|Trace.Asm.AppSecBodyBenchmark|Trace.CIVisibilityProtocolWriterBenchmark"
```

## Output Formats

- JSON results: `BenchmarkDotNet.Artifacts/results/` directory
- Uses `JsonExporter.FullCompressed` for CI
- Results are converted to `.converted.json` for analysis

## Key Files

| File | Purpose |
|------|---------|
| `tracer/test/benchmarks/Benchmarks.Trace/Program.cs` | Main entrypoint with CLI args parsing |
| `tracer/test/benchmarks/Benchmarks.Trace/Constants.cs` | Category constants |
| `tracer/build/_build/Build.cs` | Nuke build target `RunBenchmarks` |
| `.gitlab/benchmarks/microbenchmarks.yml` | CI pipeline definition |
| `.gitlab/benchmarks/bp-runner.fail-on-breach.yml` | SLO thresholds |
| `tracer/test/benchmarks/Benchmarks.Trace/README_GUIDELINES.md` | Best practices for writing benchmarks |

## Environment Variables Used

| Variable | Description |
|----------|-------------|
| `DD_SERVICE` | Service name (set to `dd-trace-dotnet` in CI) |
| `DD_ENV` | Environment (set to `CI`) |
| `DD_DOTNET_TRACER_HOME` | Path to monitoring home |
| `DD_TRACE_OTEL_ENABLED` | Enable OTel support (for instrumented benchmarks) |
| `DD_INSTRUMENTATION_TELEMETRY_ENABLED` | Telemetry toggle |
| `PR_NUMBER` | PR number for category selection |

## Target Frameworks

From `Benchmarks.Trace.csproj`:

```xml
<TargetFrameworks>net10.0;net9.0;net8.0;net7.0;net6.0;netcoreapp3.1;netcoreapp3.0;netcoreapp2.1;net472</TargetFrameworks>
```

## Quick Reference: Run Single Fast Benchmark

```bash
# Navigate to benchmark project
cd tracer/test/benchmarks/Benchmarks.Trace

# Build first
dotnet build -c Release -f net8.0

# Run single benchmark, fast mode
dotnet run -c Release -f net8.0 --no-build -- \
  --job dry \
  --filter "*SpanBenchmark.StartFinishSpan" \
  -r net8.0
```

## References

- [BenchmarkDotNet How to Run](https://benchmarkdotnet.org/articles/guides/how-to-run.html)
- [BenchmarkDotNet Jobs Configuration](https://benchmarkdotnet.org/articles/configs/jobs.html)
- [BenchmarkDotNet FAQ](https://benchmarkdotnet.org/articles/faq.html)

---

# Benchmark Analysis for Parallelization

## Benchmark Counts

| Metric | Count |
|--------|-------|
| **Total [Benchmark] methods found** | 134 |
| **DuckTyping (excluded via csproj)** | -72 |
| **README examples (not real)** | -3 |
| **Commented out** | -1 |
| **Actual benchmarks in Benchmarks.Trace** | **58** |
| **Benchmarks.OpenTelemetry.Api** | 17 |
| **Benchmarks.OpenTelemetry.InstrumentedApi** | (shares code with Api) |

## Benchmarks.Trace - By Category

### Tracer Category (`Constants.TracerCategory`) - 24 benchmarks

| Class | Benchmarks | Has Category Attr | Notes |
|-------|------------|-------------------|-------|
| `SpanBenchmark` | 3 | ✅ prs, master | Core span ops |
| `ActivityBenchmark` | 2 | ✅ prs, master | Activity API |
| `AspNetCoreBenchmark` (2 classes) | 5 | ✅ prs, master | HTTP pipeline |
| `AgentWriterBenchmark` | 2 | ✅ prs, master | Agent serialization |
| `HttpClientBenchmark` | 1 | ✅ prs, master | HTTP client instrumentation |
| `GraphQLBenchmark` | 1 | ✅ prs, master | GraphQL integration |
| `ElasticsearchBenchmark` | 2 | ✅ prs, master | Elasticsearch integration |
| `RedisBenchmark` | 1 | ✅ prs, master | Redis integration |
| `DbCommandBenchmark` | 1 | ✅ prs, master | Database commands |
| `TraceAnnotationsBenchmark` | 1 | ✅ prs, master | Annotations |
| `ILoggerBenchmark` | 1 | ✅ prs, master | Logging |
| `NLogBenchmark` | 1 | ✅ prs, master | Logging |
| `Log4netBenchmark` | 1 | ✅ prs, master | Logging |
| `SerilogBenchmark` | 1 | ✅ prs, master | Logging |
| `CIVisibilityProtocolWriterBenchmark` | 1 | ✅ prs, master | CI Visibility |
| `CharSliceBenchmark` | 3 | ✅ prs, master | String utilities |

### AppSec Category (`Constants.AppSecCategory`) - 8 benchmarks

| Class | Benchmarks | Has Category Attr | Notes |
|-------|------------|-------------------|-------|
| `AppSecWafBenchmark` | 2 | ✅ prs, master | WAF execution (native) |
| `AppSecBodyBenchmark` | 4 | ✅ prs, master | Body parsing |
| `AppSecEncodeBenchmark` | 2 | ✅ prs, master | Encoding |
| `StringAspectsBenchmark` | 2 | ✅ prs, master | IAST aspects |

### No Category (not run in CI) - 4 benchmarks

| Class | Benchmarks | Notes |
|-------|------------|-------|
| `TraceProcessorBenchmark` | 4 | Disabled, no category |

## Benchmarks.OpenTelemetry.Api - 17 benchmarks

| Class | Benchmarks | Category |
|-------|------------|----------|
| `TracerBenchmark` | 5 | tracer, prs, master |
| `TelemetrySpanBenchmark` | 6 | tracer, prs, master |
| `ActivityBenchmark` | 6 | tracer, prs, master |

## Time Estimates

Based on BenchmarkDotNet defaults and the CI config (`--iterationTime 200`):

### Per-Benchmark Time Components

- **Warmup**: ~6-50 iterations (adaptive), ~200ms each = ~1.2-10s
- **Target**: ~15-100 iterations (adaptive), ~200ms each = ~3-20s
- **Overhead**: Process launch, JIT, setup = ~5-10s
- **Per-runtime multiplier**: Each runtime (net472, netcoreapp3.1, net6.0) runs separately

### Estimated Time Per Class (single runtime, default config)

| Class | Benchmarks | Est. Time (1 runtime) | With 3 Runtimes |
|-------|------------|----------------------|-----------------|
| `SpanBenchmark` | 3 | ~1-2 min | ~3-6 min |
| `AspNetCoreBenchmark` | 5 | ~2-3 min | ~6-9 min |
| `AgentWriterBenchmark` | 2 | ~1-2 min | ~3-6 min |
| `AppSecWafBenchmark` | 2 | ~2-4 min (native) | ~6-12 min |
| `AppSecBodyBenchmark` | 4 | ~2-3 min | ~6-9 min |
| `StringAspectsBenchmark` | 2 | ~2-3 min (complex setup) | ~6-9 min |
| Logging (4 classes) | 4 | ~2-3 min | ~6-9 min |
| Integration (5 classes) | 6 | ~3-4 min | ~9-12 min |
| **Total Benchmarks.Trace** | 32 | ~15-25 min | **~45-75 min** |

### Fast Run Estimates (`--job short`)

| Config | Time Reduction | Est. Total (3 runtimes) |
|--------|---------------|-------------------------|
| Default | baseline | 45-75 min |
| `--job short` | ~70% reduction | 15-25 min |
| `--job dry` | ~90% reduction | 5-10 min |

## Parallelization Strategy

### Option 1: Split by Domain/Category (2-3 jobs)

```
Job 1: tracer category
  - SpanBenchmark, ActivityBenchmark, AspNetCoreBenchmark
  - AgentWriterBenchmark, logging benchmarks
  - ~20 benchmarks, ~30-45 min

Job 2: appsec category
  - AppSecWafBenchmark, AppSecBodyBenchmark
  - AppSecEncodeBenchmark, StringAspectsBenchmark
  - ~8 benchmarks, ~20-30 min

Job 3: otel benchmarks (separate project)
  - Benchmarks.OpenTelemetry.Api
  - Benchmarks.OpenTelemetry.InstrumentedApi
  - ~17 benchmarks per project
```

### Option 2: Split by Complexity/Time (balanced jobs)

```
Job 1: Fast benchmarks (~simple setup)
  - SpanBenchmark (3)
  - ActivityBenchmark (2)
  - CharSliceBenchmark (3)
  - TraceAnnotationsBenchmark (1)
  - Logging benchmarks (4)
  - ~13 benchmarks

Job 2: Integration benchmarks
  - AspNetCoreBenchmark (5)
  - HttpClientBenchmark (1)
  - GraphQLBenchmark (1)
  - ElasticsearchBenchmark (2)
  - RedisBenchmark (1)
  - DbCommandBenchmark (1)
  - AgentWriterBenchmark (2)
  - CIVisibilityProtocolWriterBenchmark (1)
  - ~14 benchmarks

Job 3: AppSec/Native benchmarks (slowest)
  - AppSecWafBenchmark (2) - native code, GC setup
  - AppSecBodyBenchmark (4)
  - AppSecEncodeBenchmark (2)
  - StringAspectsBenchmark (2)
  - ~10 benchmarks
```

### Option 3: Split by Runtime (max parallelism)

Run each runtime in parallel:

```
Job 1: net472 - all benchmarks
Job 2: netcoreapp3.1 - all benchmarks
Job 3: net6.0 - all benchmarks
```

### Option 4: Fine-grained (per-class parallelism)

Each benchmark class as a separate job:

- **Pros**: Maximum parallelism, easy failure isolation
- **Cons**: High CI overhead (job startup), many result files to aggregate
- **Classes**: ~18 active classes = 18 jobs

## Recommended Strategy

**Hybrid approach combining runtime + domain splits:**

```yaml
# 6 parallel jobs for ~10-15 min each

# Tracer benchmarks
tracer-net6:
  filter: "*Benchmark*"
  categories: tracer
  runtime: net6.0

tracer-netcoreapp31:
  filter: "*Benchmark*"
  categories: tracer
  runtime: netcoreapp3.1

tracer-net472:
  filter: "*Benchmark*"
  categories: tracer
  runtime: net472

# AppSec benchmarks (often flaky, isolate)
appsec-net6:
  filter: "*AppSec*|*StringAspects*"
  categories: appsec
  runtime: net6.0

appsec-net472:
  filter: "*AppSec*|*StringAspects*"
  categories: appsec
  runtime: net472

# OTel benchmarks (separate project)
otel:
  project: Benchmarks.OpenTelemetry.InstrumentedApi
  runtime: net6.0
```

## Command Examples for Parallelization

### Run single category, single runtime

```bash
dotnet run -c Release -f net6.0 -- \
  --allCategories tracer \
  -r net6.0 \
  --job short
```

### Run single class

```bash
dotnet run -c Release -f net6.0 -- \
  --filter "*SpanBenchmark*" \
  -r net6.0 \
  --job short
```

### Run single method (fastest)

```bash
dotnet run -c Release -f net6.0 -- \
  --filter "*SpanBenchmark.StartFinishSpan" \
  -r net6.0 \
  --job dry
```

### Run AppSec only

```bash
dotnet run -c Release -f net6.0 -- \
  --allCategories appsec \
  -r net6.0 \
  --job short
```

## Flaky Benchmarks (Currently Ignored in CI)

Per `.gitlab/benchmarks/microbenchmarks.yml`:

```
IGNORED_BENCHMARKS_REGEX: "Trace.Iast.StringAspectsBenchmark|Trace.CharSliceBenchmark|Trace.Asm.AppSecWafBenchmark|Trace.Asm.AppSecBodyBenchmark|Trace.CIVisibilityProtocolWriterBenchmark"
```

These 5 classes (8+ benchmarks) are currently considered unstable.

## Key Files to Modify for Parallelization

1. **Entrypoint**: `tracer/test/benchmarks/Benchmarks.Trace/Program.cs`
   - Add category/filter CLI handling (already uses BenchmarkSwitcher)

2. **Build system**: `tracer/build/_build/Build.cs`
   - `RunBenchmarks` target at line ~570

3. **CI Pipeline**: `.gitlab/benchmarks/microbenchmarks.yml`
   - Add matrix jobs for parallel execution

4. **Results aggregation**: New bp-runner.yml needed
   - Combine results from parallel jobs
   - Generate unified report
