# Benchmark Guidelines

## Best Practices for Writing Stable Benchmarks

### 1. Use `[GlobalSetup]` Instead of Static Constructors

**❌ Don't do this:**
```csharp
public class MyBenchmark
{
    private static readonly SomeData _data;

    static MyBenchmark()  // BAD: Unpredictable timing
    {
        _data = CreateExpensiveData();
    }
}
```

**✅ Do this:**
```csharp
public class MyBenchmark
{
    private static SomeData _data;

    [GlobalSetup]
    public void GlobalSetup()  // GOOD: BenchmarkDotNet controls timing
    {
        _data = CreateExpensiveData();
    }
}
```

**Why?**
- Static constructors run at CLR-controlled times, potentially during measurement
- Can cause GC side effects that increase variance
- BenchmarkDotNet explicitly excludes `[GlobalSetup]` from timing
- See: https://github.com/dotnet/BenchmarkDotNet/issues/1304

### 2. Use `[IterationSetup]` with GC.Collect() for Unmanaged Memory

**When to use:**
- Benchmarks that allocate/free unmanaged memory (Marshal.AllocHGlobal)
- High allocation benchmarks with CV > 10%
- When you observe GC-related variance

**Example:**
```csharp
[IterationSetup]
public void Setup()
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
}
```

**When NOT to use:**
- Low-allocation benchmarks (adds unnecessary overhead)
- Benchmarks that reuse pre-allocated data
- CV < 10% (BenchmarkDotNet's adaptive algorithm handles it)

### 3. Pre-allocate Test Data in GlobalSetup

**✅ Good pattern:**
```csharp
private static ArraySegment<Span> _enrichedSpans;

[GlobalSetup]
public void GlobalSetup()
{
    // Allocate test data once
    var spans = new Span[1000];
    // ... initialize spans ...
    _enrichedSpans = new ArraySegment<Span>(spans);

    // Warmup to ensure JIT compilation
    MyBenchmarkMethod();
}
```

### 4. Don't Override Iteration Counts Unless Necessary

**❌ Avoid:**
```csharp
[WarmupCount(10)]
[IterationCount(25)]
```

**✅ Trust the adaptive algorithm:**
```csharp
// No attributes - let BenchmarkDotNet decide
```

BenchmarkDotNet has a smart algorithm to choose optimal iteration counts based on observed variance.

## Common Causes of Flaky Benchmarks

1. **Static constructor timing** → Use `[GlobalSetup]`
2. **Unpredictable GC** → Use `[IterationSetup]` with GC.Collect() (only for unmanaged memory)
3. **Shared mutable state** → Use instance fields, reset in IterationSetup if needed
4. **First-call JIT effects** → Call benchmark method in GlobalSetup warmup
5. **Async timing variance** → Ensure fake implementations complete synchronously

## References

- [BenchmarkDotNet Setup and Cleanup](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html)
- [BenchmarkDotNet Jobs Configuration](https://benchmarkdotnet.org/articles/configs/jobs.html)
- [Constructor Timing Issue #1304](https://github.com/dotnet/BenchmarkDotNet/issues/1304)
