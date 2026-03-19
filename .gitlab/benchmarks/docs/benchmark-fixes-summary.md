# Benchmark Stability Fixes Summary

## Current Configuration

After extensive testing, the optimal configuration for parallel benchmark execution is:
- **2 CPUs** per benchmark job
- **10 launches**
- **500ms iteration time**
- **Fixed counts**: 20 warmup, 10 iteration (no autopilot)

This achieves **3.2% FP rate** vs 5.7% baseline (sequential, all CPUs).

## Configurations Tested

| Configuration | FP Rate | Max Bound | p95 Bound | Notes |
|--------------|---------|-----------|-----------|-------|
| Baseline (seq, all CPUs, autopilot) | 5.7% | 17.8% | 5.5% | Current production |
| 2 CPU, 10L, 200ms, no autopilot | 3.7% | 14.3% | 4.3% | |
| 2 CPU, 10L, 500ms, no autopilot | **3.2%** | **10.5%** | **3.8%** | **Recommended** |
| 2 CPU, 5L, 500ms, no autopilot | 8.0% | 22.6% | 7.0% | Too few launches |

## Flaky Benchmarks

The following benchmarks consistently show high FP rates (>10%) across all configurations:

### High-Variance Benchmarks (Consider Optimization)

| Benchmark | Metric | FP Rate | Root Cause |
|-----------|--------|---------|------------|
| ElasticsearchBenchmark.CallElasticsearch | throughput | 57.8% | Mock object variance |
| AspNetCoreBenchmark.SendRequest | throughput | 55.6% | TestServer overhead |
| SingleSpanAspNetCoreBenchmark.SingleSpanAspNetCore | throughput | 55.6% | TestServer overhead |
| CIVisibilityProtocolWriterBenchmark.WriteAndFlushEnrichedTraces | throughput | 40.0% | I/O variance |
| StringAspectsBenchmark.StringConcatAspectBenchmark | execution_time | 40.0% | String allocation |

## Potential Per-Benchmark Optimizations

### Option 1: OperationsPerInvoke

For benchmarks with very fast operations, use `[OperationsPerInvoke(N)]` to batch operations and reduce measurement overhead:

```csharp
[Benchmark]
[OperationsPerInvoke(100)]
public void FastOperation()
{
    for (int i = 0; i < 100; i++)
    {
        DoFastThing();
    }
}
```

**Candidates:**
- SpanBenchmark methods (very fast)
- CharSliceBenchmark methods
- StringAspectsBenchmark methods

### Option 2: Custom Iteration Counts

For slow benchmarks (I/O-bound), reduce iteration counts to avoid timeout issues:

```csharp
[Benchmark]
[WarmupCount(5)]
[IterationCount(5)]
public void SlowIOOperation()
{
    // ...
}
```

**Candidates:**
- AspNetCoreBenchmark.SendRequest
- HttpClientBenchmark.SendAsync
- AgentWriterBenchmark.WriteAndFlushEnrichedTraces

### Option 3: Reduce Benchmark Scope

Some benchmarks test too much at once. Consider splitting:

- `AspNetCoreBenchmark` - Tests full pipeline; could isolate middleware
- `CIVisibilityProtocolWriterBenchmark` - Tests I/O; mock the writer

## Recommended Actions

1. **Keep 10 launches** - 5 launches showed 2.5x worse FP rate
2. **Keep 500ms iteration time** - 200ms showed higher variance
3. **Keep fixed counts (20W/10I)** - Autopilot adds variance
4. **Consider per-benchmark tuning** for the worst offenders
5. **Accept some flakiness** - I/O-bound benchmarks will always have variance

## Implementation

### Global Configuration
- Default iteration time: **200ms** (in `run-benchmarks.ps1`)
- Launch count: **10**
- Fixed counts: **20 warmup, 10 iteration**

### Per-Benchmark Overrides
Added `[IterationTime(500)]` to flaky benchmark classes:

| File | Class |
|------|-------|
| `AspNetCoreBenchmark.cs` | `AspNetCoreBenchmark`, `SingleSpanAspNetCoreBenchmark` |
| `ElasticsearchBenchmark.cs` | `ElasticsearchBenchmark` |
| `CharSliceBenchmark.cs` | `CharSliceBenchmark` |
| `CIVisibilityProtocolWriterBenchmark.cs` | `CIVisibilityProtocolWriterBenchmark` |
| `GraphQLBenchmark.cs` | `GraphQLBenchmark` |
| `Iast/StringAspectsBenchmark.cs` | `StringAspectsBenchmark` |
| `Log4netBenchmark.cs` | `Log4netBenchmark` |
| `SerilogBenchmark.cs` | `SerilogBenchmark` |
| `Asm/AppSecEncodeBenchmark.cs` | `AppSecEncoderBenchmark` |

## Files Changed

- `.gitlab/benchmarks/scripts/run-benchmarks.ps1` - Global benchmark configuration (200ms default)
- `tracer/test/benchmarks/Benchmarks.Trace/*.cs` - Per-benchmark iteration time overrides
