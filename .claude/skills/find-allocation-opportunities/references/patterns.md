# Allocation-Avoidance Pattern Catalog

This catalog documents proven patterns used throughout `tracer/src/Datadog.Trace/` to minimize
heap allocations. Each pattern includes what allocation it avoids, when to use it, concrete
examples from the codebase, and caveats.

---

## Category 1: Stack Allocation

### 1.1 `stackalloc` with `Span<T>`

**Avoids**: Heap-allocated `byte[]` or `char[]` for small temporary buffers.

**When to use**: Small, fixed-size buffers (typically under 256-512 bytes) whose lifetime
doesn't escape the method.

**Examples**:
- `Util/FnvHash64.cs:65` — `Span<byte> bytes = stackalloc byte[MaxStackLimit]`
- `Propagators/W3CBaggagePropagator.cs:125` — `Span<byte> stackBuffer = stackalloc byte[maxByteCount]`
- `Agent/DiscoveryService/DiscoveryService.cs:334` — `Span<byte> buffer = stackalloc byte[10]`
- `Util/HexString.cs:90` — `var bytesPtr = stackalloc byte[8]`

**Pattern**: Use a size threshold and fall back to `ArrayPool` for large inputs. Track the
rented array separately so it can be returned:
```csharp
const int MaxStackLimit = 256;
byte[]? rented = inputSize > MaxStackLimit ? ArrayPool<byte>.Shared.Rent(inputSize) : null;
Span<byte> buffer = rented ?? stackalloc byte[MaxStackLimit];
try
{
    // use buffer[..actualSize]
}
finally
{
    if (rented is not null)
        ArrayPool<byte>.Shared.Return(rented);
}
```

**Caveats**:
- Requires `NETCOREAPP` — use `#if` guards for .NET Framework support
- Never stackalloc large or variable-size buffers (risk of stack overflow)
- Use a constant for the stackalloc size, not a variable (JIT optimization)

### 1.2 `ValueStringBuilder`

**Avoids**: `StringBuilder` heap allocation for string building operations.

**When to use**: Building strings from multiple parts where the result is typically under ~512
chars. Especially useful in formatting resource names, URLs, and header values.

**Implementation**: `Util/ValueStringBuilder.cs` — a `ref struct` backed by `Span<char>` from
`stackalloc`, falling back to `ArrayPool<char>` if it grows.

**Examples**:
- `DiagnosticListeners/AspNetCoreResourceNameHelper.cs:32`:
  ```csharp
  var sb = totalLength <= 512
      ? new ValueStringBuilder(stackalloc char[512])
      : new ValueStringBuilder(); // falls back to ArrayPool
  ```

**Caveats**: Only available on `NETCOREAPP` (it's a `ref struct` using `Span<T>`).

### 1.3 `[SkipLocalsInit]`

**Avoids**: The JIT's default zero-initialization of local variables, saving a `memset` on
`stackalloc` buffers.

**When to use**: Methods with `stackalloc` where the buffer will be fully written before reading.

**Implementation**: `Util/System.Runtime.CompilerServices.Attributes.cs:47` — polyfill for
older TFMs.

**Examples**:
- `Util/FnvHash64.cs:52` — `[System.Runtime.CompilerServices.SkipLocalsInit]`
- `DataStreamsMonitoring/Hashes/HashHelper.cs:65`

**Caveats**: Only skip init when the buffer is guaranteed to be written before read. Incorrect
use leads to reading uninitialized memory.

---

## Category 2: Pooling and Caching

### 2.1 `ArrayPool<T>.Shared`

**Avoids**: Repeated `new T[]` allocations for temporary arrays.

**When to use**: Temporary arrays that are too large for `stackalloc` or when the code must
support .NET Framework.

**Examples**:
- `Debugger/Symbols/Utf8CountingPooledTextWriter.cs:42` — `ArrayPool<char>.Shared.Rent(initialCapacity)`
- `Debugger/Symbols/SymbolExtractor.cs:189` — `ArrayPool<Model.Scope>.Shared.Rent(scopesBufferLength)`
- `Debugger/SpanCodeOrigin/SpanCodeOrigin.cs:256` — `ArrayPool<FrameInfo>.Shared.Rent(...)`
- `OpenTelemetry/Metrics/TagSet.cs:51` — `ArrayPool<KeyValuePair<string, object?>>.Shared.Rent(len)`

**Pattern**: Always return in a `finally` block:
```csharp
var rented = ArrayPool<T>.Shared.Rent(size);
try
{
    // use rented[0..actualSize]
}
finally
{
    ArrayPool<T>.Shared.Return(rented, clearArray: containsSensitiveData);
}
```

### 2.2 `FixedSizeArrayPool<T>`

**Avoids**: Repeated small array allocations (1-5 elements) — lighter weight than `ArrayPool`
for tiny arrays.

**Implementation**: `Util/FixedSizeArrayPool.cs` — fast `Interlocked.Exchange` path with
`ConcurrentStack` overflow. Returns a `ref struct ArrayPoolItem` for RAII disposal.

**Examples**:
- `Logging/Internal/DatadogSerilogLogger.cs:219-284` — used by generic log overloads to
  avoid `params object[]` allocation:
  ```csharp
  using var array = FixedSizeArrayPool<object?>.TwoItemPool.Get();
  array.Array[0] = property0;
  array.Array[1] = property1;
  ```

**When to use**: High-frequency code that needs 1-5 element arrays (e.g., logging arguments).

### 2.3 `StringBuilderCache`

**Avoids**: `new StringBuilder()` allocation per string-building operation.

**Implementation**: `Util/StringBuilderCache.cs` — `[ThreadStatic]` cached `StringBuilder`
with acquire/release pattern. Max capacity: 360 chars.

**Examples**: Used in 57+ files across the codebase.

**Pattern**:
```csharp
var sb = StringBuilderCache.Acquire();
sb.Append("prefix");
sb.Append(value);
return StringBuilderCache.GetStringAndRelease(sb);
```

**Caveats**: Not nestable on the same thread — if the called method also uses
`StringBuilderCache`, one of them will allocate a new `StringBuilder`.

### 2.4 `JsonArrayPool`

**Avoids**: Newtonsoft.Json's internal `char[]` allocations during JSON read/write.

**Implementation**: `Util/Json/JsonArrayPool.cs` — wraps `ArrayPool<char>.Shared` as
Newtonsoft's `IArrayPool<char>`.

**Examples** (24 files):
```csharp
using var jsonReader = new JsonTextReader(reader) { ArrayPool = JsonArrayPool.Shared };
using var jsonWriter = new JsonTextWriter(writer) { ArrayPool = JsonArrayPool.Shared };
```

**When to use**: Every `JsonTextReader` and `JsonTextWriter` creation.

### 2.5 `UnmanagedMemoryPool`

**Avoids**: GC pressure entirely by pooling native memory blocks via `Marshal.AllocCoTaskMem`.

**Implementation**: `Util/UnmanagedMemoryPool.cs` — not thread-safe, designed for use with
`[ThreadStatic]`.

**Examples**:
- `AppSec/WafEncoding/Encoder.cs:32` — `[ThreadStatic]` unmanaged memory pool for WAF encoding

**When to use**: High-frequency native interop where GC pauses are unacceptable.

### 2.6 `[ThreadStatic]` Caching

**Avoids**: Both allocation and lock contention by keeping per-thread cached instances.

**Examples**:
- `Util/StringBuilderCache.cs:23` — cached `StringBuilder`
- `Agent/MessagePack/MessagePackStringCache.cs:23-37` — cached serialized byte arrays
- `AppSec/WafEncoding/Encoder.cs:32` — cached `UnmanagedMemoryPool`
- `Util/ThreadSafeRandom.cs:19` — cached `Random` instance

**Caveats**: Memory scales linearly with thread count. Best for small, frequently-reused objects.

### 2.7 `MessagePackStringCache`

**Avoids**: Repeated MessagePack encoding of semi-static strings (env, version, service name).

**Implementation**: `Agent/MessagePack/MessagePackStringCache.cs` — `readonly struct CachedBytes`
keyed by string value, cached per-thread via `[ThreadStatic]`.

**When to use**: Any string that is serialized on every span but rarely changes.

---

## Category 3: Value Types

### 3.1 `readonly struct`

**Avoids**: Heap allocation for small, short-lived data containers.

**Examples**:
- `Agent/StatsAggregationKey.cs:10` — `readonly struct StatsAggregationKey`
- `Iast/Vulnerability.cs:12` — `readonly struct Vulnerability`
- `ClrProfiler/CallTarget/CallTargetState.cs:19` — `readonly struct CallTargetState`
- `Agent/MessagePack/SpanModel.cs:13` — `readonly struct SpanModel`
- `Agent/MessagePack/TraceChunkModel.cs:21` — `readonly struct TraceChunkModel`

**When to use**: Small data carriers (under ~64 bytes), keys, intermediate results, return
types from hot methods. The `readonly` modifier prevents defensive copies.

### 3.2 `ref struct`

**Avoids**: Any heap allocation — `ref struct` cannot escape to the heap.

**Examples**:
- `Tagging/TagItem.cs:10` — `readonly ref struct TagItem<T>` (carries `ReadOnlySpan<byte>`)
- `Util/SpanCharSplitter.cs:13` — `readonly ref struct SpanCharSplitter`
- `ClrProfiler/CallTarget/CallTargetReturn.cs:18` — `readonly ref struct CallTargetReturn<T>`
- `Util/FixedSizeArrayPool.cs:99` — `ref struct ArrayPoolItem` (RAII dispose pattern)
- `Iast/Propagation/StringModuleImpl.cs:592` — `ref struct StringConcatParams`

**When to use**: Types that hold `Span<T>`, temporary enumerators, RAII wrappers that must
not escape the calling method.

### 3.3 Struct Enumerators (Allocation-Free Iteration)

**Avoids**: Boxing a struct enumerator to `IEnumerator<T>` when used via `foreach`.

**Examples**:
- `Agent/SpanCollection.cs:207` — `public struct Enumerator : IEnumerator<Span>`
- `Util/SpanCharSplitter.cs:30` — `ref struct SpanSplitEnumerator` (zero-alloc string splitting)
- `Activity/Helpers/AllocationFreeEnumerator.cs` — `DynamicMethod`-based approach to force
  struct enumerator usage on arbitrary `IEnumerable<T>`

**Pattern**: Expose `GetEnumerator()` returning a concrete struct type. C#'s `foreach` will
use the struct directly without boxing:
```csharp
internal readonly struct MyCollection : IEnumerable<Item>
{
    public Enumerator GetEnumerator() => new Enumerator(this);

    public struct Enumerator : IEnumerator<Item>
    {
        // ...
    }
}
```

### 3.4 Generic Struct Constraints (Avoid Boxing)

**Avoids**: Boxing value types when calling interface methods.

**Examples**:
- `Propagators/SpanContextPropagator.cs:73` — `where TCarrierSetter : struct, ICarrierSetter<TCarrier>`
- `Propagators/W3CTraceContextPropagator.cs:122` — `where TCarrierSetter : struct, ICarrierSetter<TCarrier>`
- `Activity/DiagnosticSourceEventListener.cs:79` — `where T : struct, IActivity`
- `Baggage.cs:450` — `where T : struct, ICancellableObserver<KeyValuePair<string, string?>>`

**Pattern**: Define an interface, implement it as a `readonly struct`, and constrain the
generic parameter:
```csharp
internal interface ICarrierSetter<TCarrier>
{
    void Set(TCarrier carrier, string key, string value);
}

internal readonly struct HttpRequestCarrierSetter : ICarrierSetter<HttpRequestMessage> { ... }

// The JIT devirtualizes calls — no boxing, no interface dispatch overhead
internal void Inject<TCarrierSetter>(TCarrier carrier, TCarrierSetter setter)
    where TCarrierSetter : struct, ICarrierSetter<TCarrier>
```

---

## Category 4: String Optimization

### 4.1 `string.Create` with `stackalloc`

**Avoids**: Intermediate string allocations when formatting composite strings.

**Examples**:
- `Propagators/W3CTraceContextPropagator.cs:149`:
  ```csharp
  return string.Create(null, stackalloc char[128], $"00-{context.RawTraceId}-{context.RawSpanId}-{sampled}");
  ```
- `Propagators/B3SingleHeaderContextPropagator.cs:158`
- `ServiceFabric/ServiceRemotingHelpers.cs:195`

**When to use**: Formatting short strings from known components.

**Caveats**: Requires .NET 6+ (`string.Create` with `IFormatProvider?` overload).

### 4.2 `ReadOnlySpan<char>` / `.AsSpan()` for String Slicing

**Avoids**: `string.Substring()` allocating a new string for intermediate parsing.

**Examples**: ~181 occurrences across 42 non-vendor files:
- `Propagators/W3CTraceContextPropagator.cs` — parsing trace headers
- `Debugger/ExceptionAutoInstrumentation/ExceptionNormalizer.cs` — stack frame parsing
- `Propagators/B3SingleHeaderContextPropagator.cs` — header parsing

**Pattern**:
```csharp
// Before (allocates):
var part = header.Substring(start, length);

// After (zero-alloc):
ReadOnlySpan<char> part = header.AsSpan(start, length);
```

### 4.3 `string.Split()` Replacement with `SpanCharSplitter`

**Avoids**: `string.Split()` allocates both a `string[]` and a new `string` for each segment.

**Implementation**: `Util/SpanCharSplitter.cs` — a `readonly ref struct` with a `ref struct`
enumerator that yields `ReadOnlySpan<char>` slices without any allocation.

**Examples**:
- `Util/SpanCharSplitter.cs:13` — the implementation
- Usage pattern via `foreach`:
  ```csharp
  // Before (allocates string[] + individual strings):
  foreach (var part in input.Split(','))
  {
      Process(part);
  }

  // After (zero-alloc on NETCOREAPP):
  foreach (var part in new SpanCharSplitter(input, ',', int.MaxValue))
  {
      Process(part.AsSpan());
  }
  ```

**Caveats**: Only available on `NETCOREAPP` (uses `ref struct` and `Span<T>`). For .NET
Framework, use `IndexOf`-based manual parsing or accept the `Split()` allocation.

### 4.4 Pre-Computed Static Strings and Arrays

**Avoids**: Repeated string construction or array creation for constant values.

**Examples**:
- `RuntimeMetrics/RuntimeEventListener.cs:36-37` — `static readonly string[] CompactingGcTags`
- `SpanContext.cs:26` — `static readonly string[] KeyNames`
- `PlatformHelpers/ServiceFabric.cs:14-22` — static readonly env var lookups at startup

---

## Category 5: JIT Hints

### 5.1 `[MethodImpl(MethodImplOptions.AggressiveInlining)]`

**Avoids**: Method call overhead; enables further JIT optimizations (struct promotion,
dead-code elimination).

~690 occurrences across hot paths:
- `Util/FixedSizeArrayPool.cs:44` — on `Get()`
- `Util/SpanCharSplitter.cs:19,27` — on constructor and `GetEnumerator()`
- `ClrProfiler/CallTarget/CallTargetReturn.cs`, `CallTargetState.cs`

**When to use**: Small, frequently-called methods (1-3 lines). Especially important for
`readonly struct` methods to enable the JIT to promote them to registers.

### 5.2 `ThrowHelper` with `[MethodImpl(NoInlining)]`

**Avoids**: The JIT inlining exception-throwing code into the caller, which bloats the hot
path and may prevent the caller from being inlined.

**Implementation**: `Util/ThrowHelper.cs` — centralized `[DoesNotReturn]` throw methods.

**Examples**:
- `Util/ThrowHelper.cs:16` — `[MethodImpl(MethodImplOptions.NoInlining)]` on all throw methods
- `Util/UnmanagedMemoryPool.cs:212` — local `ThrowObjectDisposedException()` with `[NoInlining]`

**Pattern**:
```csharp
// Before (prevents caller inlining):
if (arg is null) throw new ArgumentNullException(nameof(arg));

// After (keeps hot path small):
if (arg is null) ThrowHelper.ThrowArgumentNullException(nameof(arg));
```

---

## Category 6: API Design for Zero Allocation

### 6.1 Generic Log Overloads (Avoid `params` + Boxing)

**Avoids**: `params object[]` array allocation AND boxing of value-type arguments.

**Implementation**: `Logging/Internal/DatadogSerilogLogger.cs:211-284` — typed overloads
for 1-5 arguments using `FixedSizeArrayPool`.

**Pattern**:
```csharp
// Provide typed overloads instead of params:
void Write<T>(LogEventLevel level, string template, T property) { ... }
void Write<T0, T1>(LogEventLevel level, string template, T0 p0, T1 p1) { ... }

// Each overload:
// 1. Checks IsEnabled first (avoids work entirely if disabled)
// 2. Uses FixedSizeArrayPool to get a reusable array
// 3. Boxes only after confirming the log will actually be written
```

### 6.2 Static / Cached Delegates

**Avoids**: Allocating a new delegate object on each call.

**Examples**:
- `AspNet/SharedItems.cs:17-18` — `static readonly Func<Stack<Scope>, Scope> Pop`
- `Debugger/DebuggerManager.cs:33-34` — `static readonly Func<string> ServiceNameProvider`
- `RuntimeMetrics/RuntimeMetricsWriter.cs:34` — `static readonly Func<...> InitializeListenerFunc`
- `Iast/IastModule.cs:65` — `static readonly Func<TaintedObject, bool> Always`

**When to use**: Any delegate passed to a method that is called frequently. The `static`
keyword on lambdas (C# 9+) prevents accidental captures.

### 6.3 `ValueTask` / `ValueTask<T>` for Hot Async Paths

**Avoids**: `Task` / `Task<T>` heap allocation when the async method completes synchronously
(which is the common case for cached results, already-available data, etc.).

**When to use**: Async methods on hot paths that frequently complete synchronously. Common in
I/O code with caching layers, or methods that only go async on cache miss.

**Pattern**:
```csharp
#if NETCOREAPP3_1_OR_GREATER
// Return ValueTask when synchronous completion is common:
internal ValueTask<string> GetCachedValueAsync()
{
    if (_cache.TryGetValue(key, out var value))
        return new ValueTask<string>(value); // no Task allocation

    return GetValueSlowAsync(); // only allocates Task on cache miss
}
#else
internal Task<string> GetCachedValueAsync()
{
    if (_cache.TryGetValue(key, out var value))
        return Task.FromResult(value);

    return GetValueSlowAsync();
}
#endif
```

**Caveats**:
- `ValueTask` is available from .NET Core 2.1+ but pooled async builders require
  `NETCOREAPP3_1_OR_GREATER` — use `#if` guards for .NET Framework
- `ValueTask` can only be awaited once (unlike `Task`). Do not cache or await multiple times
- Prefer `Task` when the result is always async (network calls without caching)

---

## Category 7: Unsafe / Low-Level

### 7.1 `Unsafe.As` / `Unsafe.ReadUnaligned`

**Avoids**: Memory copies when reinterpreting data.

**Examples**:
- `Ci/Coverage/Util/FileBitmap.cs:190+` — SIMD-optimized bitmap operations using
  `Unsafe.ReadUnaligned<Vector512<byte>>`, `Vector256`, `Vector128`, `ulong`, `uint`

**When to use**: Performance-critical data processing where the type system gets in the way.
Requires careful correctness review.

### 7.2 `HexConverter` with Unsafe Pointers

**Avoids**: Intermediate allocations in hex encoding.

**Implementation**: `Util/HexConverter.cs:158`:
```csharp
return string.Create(bytes.Length * 2, (RosPtr: (IntPtr)(&bytes), casing), static (chars, args) => { ... });
```

Uses `string.Create` with a state tuple containing an unsafe pointer to avoid copying the
input span.
