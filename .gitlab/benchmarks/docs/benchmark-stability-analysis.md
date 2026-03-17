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

| Configuration | Significant Impact FP Rate |
|--------------|---------------------------|
| Sequential, 1 CPU | 6.67% |
| Parallel, 2 CPUs, 10 launches | 12.0% |
| Parallel, 2 CPUs, 20 launches | 9.2% |
| Parallel, 2 CPUs, 5 launches, 500ms iteration | TBD |
