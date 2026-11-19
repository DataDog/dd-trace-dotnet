# Benchmark Guidelines

## Best Practices for Writing Stable Benchmarks

### 1. Use `[GlobalSetup]` with Instance Fields (Not Static Constructors)

**❌ Don't do this:**
```csharp
public class MyBenchmark
{
    private static readonly SomeData _data;
    private static readonly MyClient _client;

    static MyBenchmark()  // BAD: Static constructor for any runtime initialization
    {
        _data = CreateExpensiveData();
        _client = new MyClient();
    }
}
```

**✅ Do this:**
```csharp
public class MyBenchmark
{
    private MyClient _client;     // Instance field
    private SomeData _data;       // Instance field

    [GlobalSetup]
    public void GlobalSetup()  // GOOD: BenchmarkDotNet controls timing
    {
        _client = new MyClient();
        _data = CreateExpensiveData();
    }
}
```

**Why?**
- `[GlobalSetup]` runs **once per benchmark method**, not once per class
- Using instance fields ensures proper isolation between benchmark methods
- Static constructors and static field initializers run at CLR-controlled times, potentially during measurement
- BenchmarkDotNet explicitly excludes `[GlobalSetup]` from timing
- Consistent pattern: all benchmark state is instance fields initialized in GlobalSetup
- See: https://github.com/dotnet/BenchmarkDotNet/issues/1304

### 2. Use Instance Fields for All Benchmark State

**Use instance fields for:**
- ✅ **Everything** initialized in `[GlobalSetup]`: clients, services, test data, lambdas, mocks
- ✅ Ensures consistent pattern with no ambiguity about static vs instance
- ✅ Avoids any static initialization timing issues

**Exception - `static readonly` with static constructor ONLY for:**
- ✅ Pure reflection metadata: `RuntimeMethodHandle`, `RuntimeTypeHandle`
- These are immutable handles that must be initialized via reflection before GlobalSetup runs

**Example:**
```csharp
public class MyBenchmark
{
    // Instance fields: ALL benchmark state
    private MyClient _client;
    private MyService _service;
    private List<MyData> _testData;
    private Func<int> _simpleFunc;
    private byte[][] _rawCommands;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var settings = TracerSettings.Create(new() { ... });
        Tracer.UnsafeSetTracerInstance(new Tracer(settings, ...));

        // Initialize ALL fields in GlobalSetup
        _client = new MyClient();
        _service = new MyService();
        _testData = CreateTestData();
        _simpleFunc = () => 42;
        _rawCommands = new[] { "cmd", "arg1" }
            .Select(Encoding.UTF8.GetBytes)
            .ToArray();
    }
}
```

### 3. Never Use `[IterationSetup]` (or `[IterationCleanup]`)

**❌ Don't do this:**
```csharp
[IterationSetup]
public void Setup()
{
    // Force GC to reduce variance from native memory interactions
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
}
```

**Why?**
- `[IterationSetup]` forces one invocation per iteration and skips BenchmarkDotNet's pilot, the component that sizes how many operations an iteration runs. Every measured iteration becomes a single call: full process, GC and timer noise, nothing averaged out.
- `[IterationCleanup]` has the same effect.
- The forced GC is redundant: BenchmarkDotNet already forces a full GC between iterations by default (`GcMode.Force`).
- Microsoft's guideline: under 100 ms per invocation, ["your benchmark will be entirely invalid"](https://github.com/dotnet/performance/blob/main/docs/microbenchmark-design-guidelines.md#iterationsetup).
- Full writeup: [IterationSetup breaks BenchmarkDotNet microbenchmarks](https://datadoghq.atlassian.net/wiki/x/2ACEtQE).

**Do this instead:**
- One-time setup goes in `[GlobalSetup]`, including for unmanaged memory benchmarks (WAF, profiler, `Marshal.AllocHGlobal`).
- Add `[GcForce(true)]` explicitly to the class. It is the default, but the artifacts do not record GC settings, so an explicit attribute is the only way to verify the intent later.
- Let the pilot size iterations. That is what it is for.

### 4. Pre-allocate Test Data in GlobalSetup

**✅ Good pattern:**
```csharp
private ArraySegment<Span> _enrichedSpans;

[GlobalSetup]
public void GlobalSetup()
{
    // Allocate test data once in GlobalSetup
    var spans = new Span[1000];
    for (int i = 0; i < 1000; i++)
    {
        spans[i] = new Span(...);
        spans[i].SetTag("key", "value");
    }
    _enrichedSpans = new ArraySegment<Span>(spans);
}
```

### 5. Don't Override Iteration Counts Unless Necessary

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

## Quick Reference: DO vs DON'T

### DO:
- ✅ Use `[GlobalSetup]` for **all** initialization
- ✅ Use **instance fields** for all benchmark state
- ✅ Use `[GcForce(true)]` explicitly on every benchmark class
- ✅ Check `Operations > 1` and per-launch agreement after adding or changing a benchmark (see "Validating a new benchmark" below)
- ✅ Use `static readonly` with static constructor ONLY for pure reflection metadata (MethodHandle, TypeHandle)
- ✅ Trust BenchmarkDotNet's warmup algorithm (it handles JIT compilation automatically)

### DON'T:
- ❌ Use static constructors for any runtime initialization
- ❌ Use `static` or `static readonly` fields for test data, mocks, or runtime objects
- ❌ Add manual warmup calls in GlobalSetup (BenchmarkDotNet does this more carefully)

## Common Causes of Flaky Benchmarks

1. **Static constructor/initializer timing** → Use `[GlobalSetup]` with instance fields for everything
2. **Shared state between benchmark methods** → Use instance fields, not static
3. **Unpredictable GC** → Nothing to do: `GcForce` (the default) already forces a full GC between iterations
4. **Manual warmup interference** → Trust BenchmarkDotNet's automatic warmup (don't add manual calls)
5. **Async timing variance** → Ensure fake implementations complete synchronously; never let a fake run background threads (flush, heartbeat), they add uncontrolled timing noise

## Pattern Examples

### Simple Benchmark (No Complex Setup)
```csharp
[MemoryDiagnoser]
[BenchmarkCategory(Constants.TracerCategory)]
public class SimpleBenchmark
{
    private string _testString;
    private MyClient _client;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var settings = TracerSettings.Create(new() { ... });
        Tracer.UnsafeSetTracerInstance(new Tracer(settings, ...));

        _testString = "Hello, World!";
        _client = new MyClient();
    }

    [Benchmark]
    public void MyBenchmarkMethod()
    {
        _client.Process(_testString);
    }
}
```

### Native/Unmanaged Code Benchmark
```csharp
[MemoryDiagnoser]
[BenchmarkCategory(Constants.AppSecCategory)]
public class NativeBenchmark
{
    private Dictionary<string, object> _testData;
    private Waf _waf;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var settings = new IastSettings(...);
        _waf = Waf.Create(...);
        _testData = CreateTestData();
    }

    [Benchmark]
    public void RunWaf()
    {
        var context = _waf.CreateContext();
        context.Run(_testData, 1_000_000);
        context.Dispose();
    }
}
```

### Reflection Metadata Benchmark
```csharp
[MemoryDiagnoser]
[BenchmarkCategory(Constants.TracerCategory)]
public class ReflectionBenchmark
{
    private static readonly RuntimeMethodHandle MethodHandle;
    private static readonly RuntimeTypeHandle TypeHandle;

    static ReflectionBenchmark()  // OK: Pure reflection metadata
    {
        var method = typeof(ReflectionBenchmark).GetMethod("TargetMethod");
        MethodHandle = method.MethodHandle;
        TypeHandle = method.DeclaringType.TypeHandle;
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var settings = TracerSettings.Create(new() { ... });
        Tracer.UnsafeSetTracerInstance(new Tracer(settings, ...));
    }

    [Benchmark]
    public void MyBenchmarkMethod()
    {
        // Use MethodHandle and TypeHandle
    }

    public void TargetMethod() { }
}
```

## Validating a new benchmark

BenchmarkDotNet's stability machinery (pilot, adaptive warmup and iteration counts, forced GC between iterations) only works when nothing defeats it. After adding or changing a benchmark, check the artifacts of one CI run:

1. **`Operations > 1`** in the BDN logs or JSON artifacts for every measurement. `Operations == 1` means the pilot was skipped: look for `[IterationSetup]`, `[IterationCleanup]` or `RunOncePerIteration`.
2. **Per-launch agreement**: the reported values of the launches of one benchmark should agree within a few percent. Wide launch-to-launch spread means process-level noise dominates; find and remove the noise source instead of raising the launch count.

Other rules that keep the suite measurable:

- **Never commit TFM stubs**: a benchmark whose body compiles to nothing on a target framework (for example `#if NET6_0_OR_GREATER` around the real body) measures BenchmarkDotNet loop overhead and code alignment, not a scenario. Filter it out in `Program.cs` (see `IsRealAspNetCoreBenchmark`).
- **CI repetition budget**: each pipeline gives a benchmark 3 launches of about a minute each, with iterations of 500 ms. Keep one invocation cheap enough for the pilot to scale operations into the thousands; if a single call takes ~100 ms or more, the benchmark belongs to the macrobenchmark suite.

## References

- [BenchmarkDotNet Setup and Cleanup](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html)
- [BenchmarkDotNet Jobs Configuration](https://benchmarkdotnet.org/articles/configs/jobs.html)
- [Constructor Timing Issue #1304](https://github.com/dotnet/BenchmarkDotNet/issues/1304)
- [GlobalSetup runs once per benchmark method](https://github.com/dotnet/BenchmarkDotNet/blob/master/docs/articles/features/setup-and-cleanup.md)
- [IterationSetup breaks BenchmarkDotNet microbenchmarks](https://datadoghq.atlassian.net/wiki/x/2ACEtQE)
