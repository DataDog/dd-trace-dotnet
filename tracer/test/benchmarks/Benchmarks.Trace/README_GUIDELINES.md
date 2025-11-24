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

        // Warmup to ensure JIT compilation
        MyBenchmarkMethod();
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

        // Warmup
        MyBenchmarkMethod();
    }
}
```

### 3. Use `[IterationSetup]` with GC.Collect() for Unmanaged Memory

**When to use:**
- Benchmarks that allocate/free unmanaged memory (Marshal.AllocHGlobal)
- Benchmarks with native code interactions (WAF, profiler)
- High allocation benchmarks with CV > 10%
- When you observe GC-related variance

**Example:**
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

**When NOT to use:**
- Low-allocation benchmarks (adds unnecessary overhead)
- Benchmarks that reuse pre-allocated data
- CV < 10% (BenchmarkDotNet's adaptive algorithm handles it)

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

    // Warmup to ensure JIT compilation
    MyBenchmarkMethod();
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
- ✅ Add warmup calls in GlobalSetup
- ✅ Use `[IterationSetup]` with GC for native/unmanaged code benchmarks
- ✅ Use `static readonly` with static constructor ONLY for pure reflection metadata (MethodHandle, TypeHandle)

### DON'T:
- ❌ Use static constructors for any runtime initialization
- ❌ Use `static` or `static readonly` fields for test data, mocks, or runtime objects
- ❌ Skip warmup for native code or complex initialization

## Common Causes of Flaky Benchmarks

1. **Static constructor/initializer timing** → Use `[GlobalSetup]` with instance fields for everything
2. **Shared state between benchmark methods** → Use instance fields, not static
3. **Unpredictable GC** → Use `[IterationSetup]` with GC.Collect() (only for unmanaged memory)
4. **First-call JIT effects** → Call benchmark method in GlobalSetup warmup
5. **Async timing variance** → Ensure fake implementations complete synchronously

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

        // Warmup
        MyBenchmarkMethod();
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

        // Aggressive warmup for native code
        for (int i = 0; i < 10; i++)
        {
            RunWaf();
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Force GC to reduce variance from native memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
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

        // Warmup
        MyBenchmarkMethod();
    }

    [Benchmark]
    public void MyBenchmarkMethod()
    {
        // Use MethodHandle and TypeHandle
    }

    public void TargetMethod() { }
}
```

## References

- [BenchmarkDotNet Setup and Cleanup](https://benchmarkdotnet.org/articles/features/setup-and-cleanup.html)
- [BenchmarkDotNet Jobs Configuration](https://benchmarkdotnet.org/articles/configs/jobs.html)
- [Constructor Timing Issue #1304](https://github.com/dotnet/BenchmarkDotNet/issues/1304)
- [GlobalSetup runs once per benchmark method](https://github.com/dotnet/BenchmarkDotNet/blob/master/docs/articles/features/setup-and-cleanup.md)
