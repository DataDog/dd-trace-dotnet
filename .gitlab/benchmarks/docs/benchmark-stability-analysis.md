# Benchmark Stability Analysis

This document analyzes the stability of dd-trace-dotnet microbenchmarks and identifies sources of variance.

## Summary

Based on stability testing (10-11 pipelines, 45-55 comparisons per benchmark), the overall **Significant Impact FP Rate** ranges from 9-12% depending on configuration. The target is <5% for reliable PR gating.

## Flaky Benchmarks Analysis

### CharSliceBenchmark (All runtimes: 18-71% FP)

**Code:**
```csharp
[IterationSetup]
public void Setup()
{
    GC.Collect();  // GC timing varies between iterations
    GC.WaitForPendingFinalizers();
    GC.Collect();
}

[Benchmark]
public void OriginalCharSlice()
{
    for (var i = 0; i < 10000; i++)
    {
        using var slice = new CharSliceV0("...");  // Marshal.AllocHGlobal
        Consume(slice);
    }
}
```

**Why flaky:**
1. `[IterationSetup]` with `GC.Collect()` - GC timing is inherently non-deterministic
2. 10,000 `Marshal.AllocHGlobal/FreeHGlobal` calls per invocation - unmanaged heap fragmentation varies
3. Measures memory allocation variance, not stable code paths
4. Execution time: 1-3.6ms per invocation (slow, fewer samples per iteration)

---

### CIVisibilityProtocolWriterBenchmark (All runtimes: 15-65% FP)

**Code:**
```csharp
private const int SpanCount = 1000;

[Benchmark]
public Task WriteAndFlushEnrichedTraces()
{
    _eventWriter.WriteTrace(_enrichedSpans);  // Writes 1000 spans
    return _eventWriter.FlushTracesAsync();   // Async flush
}
```

**Why flaky:**
1. Writes 1000 spans per invocation - heavy allocation (MessagePack serialization)
2. Async Task overhead and state machine allocation
3. Internal buffering/batching in the writer - buffer state varies between invocations
4. Execution time: 972us-2.2ms (slow)

---

### HttpClientBenchmark (netcoreapp3.1: 62% FP)

**Code:**
```csharp
[Benchmark]
public unsafe string SendAsync()
{
    CallTarget.Run<HttpClientHandlerIntegration, ...>(...).GetAwaiter().GetResult();
    return "OK";
}
```

**Why flaky:**
1. Async/await overhead - Task state machine allocation varies
2. `GetAwaiter().GetResult()` blocking pattern has scheduling variance
3. CallTarget integration creates spans with timing-dependent operations
4. netcoreapp3.1 JIT variance compounds the issue

---

### AspNetCoreBenchmark / SingleSpanAspNetCoreBenchmark (netcoreapp3.1: 60% FP)

**Code:**
```csharp
[Benchmark]
public string SendRequest()
{
    return _client.GetStringAsync("/Home").GetAwaiter().GetResult();
}
```

**Why flaky:**
1. Full ASP.NET Core pipeline execution (routing, MVC, controller)
2. TestServer has internal buffering and connection pooling
3. DiagnosticSource observers add timing variance
4. Multiple async operations chained together
5. Security + IAST subsystems initialized

---

### NLogBenchmark / Log4netBenchmark (netcoreapp3.1: 36-58% FP)

**Code (NLog):**
```csharp
[Benchmark]
public void EnrichedLog()
{
    using (Tracer.Instance.StartActive("Test"))
    {
        using (Tracer.Instance.StartActive("Child"))
        {
            var callTargetState = LoggerImplWriteIntegrationV5.OnMethodBegin(...);
            _logger.Info("Hello");
            LoggerImplWriteIntegrationV5.OnMethodEnd(...);
        }
    }
}
```

**Why flaky:**
1. Logging framework internal buffering (TextWriter targets)
2. Two nested spans created per invocation
3. MDC/MDLC context manipulation
4. String formatting and layout rendering

---

### ElasticsearchBenchmark (netcoreapp3.1: 60% FP)

**Code:**
```csharp
[Benchmark]
public unsafe object CallElasticsearch()
{
    return CallTarget.Run<RequestPipeline_CallElasticsearch_Integration, ...>(...);
}

[Benchmark]
public unsafe int CallElasticsearchAsync()
{
    return CallTarget.Run<...>(...).GetAwaiter().GetResult();
}
```

**Why flaky:**
1. Duck typing proxy generation at runtime
2. Async version has Task overhead
3. CallTarget integration span creation

---

### ActivityBenchmark (netcoreapp3.1: 42% FP)

**Code:**
```csharp
[Benchmark]
public void StartStopWithChild()
{
    using var parent = CreateActivity();
    using var child = CreateActivity(parent);
    var parentMock = parent.DuckAs<IActivity6>()!;  // Duck typing
    var childMock = child.DuckAs<IActivity6>()!;
    _handler.ActivityStarted(SourceName, parentMock);
    _handler.ActivityStarted(SourceName, childMock);
    // ...
}
```

**Why flaky:**
1. Activity context propagation has thread-local state
2. Duck typing proxy creation
3. ActivityListener callback overhead
4. Two activities created/stopped per invocation

---

### StringAspectsBenchmark (netcoreapp3.1: 36% FP)

**Code:**
```csharp
[IterationSetup(Target = nameof(StringConcatAspectBenchmark))]
public void InitTaintedContextWhenTrue()
{
    _initTaintedContextTrue = InitTaintedContext(10, true);  // Creates IAST context
}

[Benchmark]
public void StringConcatAspectBenchmark()
{
    for (int x = 0; x < Iterations; x++)
    {
        var txt = StringAspects.Concat(arr);  // IAST taint tracking
        // ...
    }
}
```

**Why flaky:**
1. `[IterationSetup]` creates fresh IAST context each iteration
2. Taint tracking with TaintedObjects dictionary operations
3. 100 iterations of string concatenation with taint propagation
4. Global state manipulation (Tracer.Instance, Iast.Instance)

---

### GraphQLBenchmark (netcoreapp3.1: 31% FP)

**Code:**
```csharp
[Benchmark]
public unsafe int ExecuteAsync()
{
    var task = CallTarget.Run<ExecuteAsyncIntegration, ...>(...);
    return task.GetAwaiter().GetResult().Value;
}
```

**Why flaky:**
1. Async Task overhead
2. Duck typing for ExecutionContext properties
3. Span creation with multiple tag extractions

---

## Runtime-Specific Observations

**netcoreapp3.1** shows consistently higher variance than net6.0 and net472:
- Tiered compilation behavior differs
- JIT decisions less deterministic
- Older async state machine implementation

**net472** is generally more stable due to:
- No tiered compilation
- Mature, deterministic JIT
- Simpler async implementation

**net6.0** is moderately stable:
- Better tiered compilation heuristics
- Improved async state machines
- More aggressive inlining

---

## Recommendations

### Benchmarks to exclude from PR gate:
These are already in `IGNORED_BENCHMARKS_REGEX`:
- `Trace.Iast.StringAspectsBenchmark` - IAST taint tracking variance
- `Trace.CharSliceBenchmark` - Memory allocation variance
- `Trace.Asm.AppSecWafBenchmark` - Native WAF interop variance
- `Trace.Asm.AppSecBodyBenchmark` - Complex body parsing
- `Trace.CIVisibilityProtocolWriterBenchmark` - Heavy serialization

### Consider adding to exclusion list:
- `HttpClientBenchmark` on netcoreapp3.1 only
- `SingleSpanAspNetCoreBenchmark` on netcoreapp3.1 only
- `ActivityBenchmark` on netcoreapp3.1 only

### Benchmark improvements (if desired):
1. Remove `[IterationSetup]` GC calls from CharSliceBenchmark
2. Reduce SpanCount in CIVisibilityProtocolWriterBenchmark
3. Add `[IterationCleanup]` to reset state in logging benchmarks
4. Consider dropping netcoreapp3.1 from benchmarks entirely

---

## Configuration Impact

| Configuration | FP Rate | Max Bound | p95 Bound |
|---------------|---------|-----------|-----------|
| Baseline (sequential) | ~5% | ~55% | - |
| 1 CPU, 10 launches, 100ms | 18.2% | 97.89% | 39.09% |
| 2 CPUs, 10 launches, 100ms | 12.0% | 32.95% | 14.22% |
| 2 CPUs, 20 launches, 100ms | 9.2% | 25.36% | 13.41% |
| 2 CPUs, 10 launches, 200ms | 9.4% | 28.73% | 14.83% |
| 2 CPUs, 10 launches, 500ms | 8.6% | 18.93% | 10.54% |
| 2 CPUs, 5 launches, 500ms | 8.6% | 29.30% | 11.14% |
| **2 CPUs, 10L, 500ms, fixed counts** | **3.7%** | 21.56% | **3.84%** |
| 2 CPUs, 10L, 200ms, fixed counts | 6.3% | 17.12% | 5.44% |

**Best stability**: 2 CPUs, 10 launches, 500ms iteration time, with fixed warmup=20 and iteration=10 counts (3.7% FP, 3.84% p95 bound).

**Trade-off**: 200ms iteration time with fixed counts achieves 6.3% FP rate (vs 3.7% for 500ms) but runs 2.5x faster (20s vs 50s measurement time per benchmark).

---

## BenchmarkDotNet Configuration Details

### Iteration vs Operations

BenchmarkDotNet distinguishes between:
- **Operation**: A single call to the benchmark method
- **Iteration**: A measurement period (configurable via `--iterationTime`, default 500ms) containing many operations

With `--iterationTime 500`, a single iteration runs the benchmark method repeatedly for 500ms and reports one averaged measurement. Fast benchmarks may run millions of operations per iteration:

| Benchmark | Avg Ops/Iteration (500ms) |
|-----------|---------------------------|
| SingleSpanAspNetCoreBenchmark | 71,057,488 |
| AspNetCoreBenchmark.SendRequest | 32,248,954 |
| ObjectExtractorSimpleBody | 1,583,894 |
| SpanBenchmark.StartFinishSpan | 598,871 |
| TraceAnnotationsBenchmark | 407,605 |
| ElasticsearchBenchmark | 253,218 |
| DbCommandBenchmark | 222,200 |
| ILoggerBenchmark | 154,545 |
| SerilogBenchmark | 96,562 |
| ActivityBenchmark | 52,793 |
| AgentWriterBenchmark | 358 |
| AppSecWafBenchmark | 1 |

Benchmarks with `[IterationSetup]` (e.g., CharSliceBenchmark, StringAspectsBenchmark, AppSecWafBenchmark) report 1 op/iteration because setup runs between each invocation, but the benchmark method itself may contain loops doing thousands of operations.

**Important**: `--iterationTime` is effectively ignored for benchmarks with `[IterationSetup]`. The setup forces BenchmarkDotNet into a "run setup → 1 invocation → record" cycle regardless of the target iteration time. For example, AppSecWafBenchmark shows identical behavior (1 op, ~0.5ms) whether `--iterationTime` is 200ms or 500ms.

### Auto-Pilot vs Fixed Counts

By default, BenchmarkDotNet uses **auto-pilot mode**:
- Runs warmup iterations until measurements stabilize
- Runs actual iterations until achieving target precision (1% margin at 99.9% confidence)
- Results in variable iteration counts (7-120+ per launch) depending on benchmark noise

For parallel execution, this causes problems:
1. Noisy benchmarks run more iterations trying to achieve unreachable precision
2. Total datapoints vary wildly (150-1200 per benchmark)
3. Upload timeouts when too many datapoints

**Fixed counts** (`--warmupCount 20 --iterationCount 10`) provide:
- Consistent 100 datapoints per benchmark (10 launches × 10 iterations)
- 50 seconds of measurement time per benchmark (10 × 10 × 500ms)
- Millions of operations averaged into each datapoint
- Predictable job runtime and upload size

---

## Memory Allocation Stability

Analyzed `BytesAllocatedPerOperation` across 82 pipelines from all stability experiments to understand if memory allocation variance correlates with timing instability.

### Summary

- **73** benchmarks with valid data
- **Median CV**: 0.0%
- **Mean CV**: 5.7%
- **Benchmarks with CV < 1%**: 65 (89%)
- **Benchmarks with CV > 10%**: 6

### High Variance Benchmarks (CV > 5%)

| Benchmark | Runtime | CV% | Min | Max | Mean |
|-----------|---------|-----|-----|-----|------|
| WriteAndFlushEnrichedTraces | net472 | 89.8% | 3,200 | 62,208 | 31,154 |
| WriteAndFlushEnrichedTraces | netcoreapp3.1 | 88.8% | 2,696 | 44,657 | 23,288 |
| WriteAndFlushEnrichedTraces | net6.0 | 88.0% | 2,696 | 41,129 | 21,908 |
| EnrichedLog | net472 | 45.2% | 1,600 | 4,516 | 2,546 |
| EnrichedLog | net6.0 | 44.8% | 1,584 | 4,312 | 2,457 |
| EnrichedLog | netcoreapp3.1 | 44.1% | 1,632 | 4,312 | 2,474 |

**Insight**: `WriteAndFlushEnrichedTraces` (CIVisibilityProtocolWriterBenchmark and AgentWriterBenchmark) has ~90% CV in memory allocation - ranging from 2.7KB to 62KB per operation. This correlates with its high timing variance (15-65% FP rate). The logging benchmarks (`EnrichedLog`) also show ~45% memory CV, explaining their timing instability.

### Stable Benchmarks

47 benchmarks have **perfectly consistent** memory allocation (CV = 0%) across all 82 pipelines. These include core span operations (`StartFinishSpan`, `StartFinishScope`, `StartFinishTwoScopes`), which also show the best timing stability.

Full data available in `analysis/bytes_allocated_stability.csv`.
