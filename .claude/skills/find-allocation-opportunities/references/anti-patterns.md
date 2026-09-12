# Anti-Patterns: Allocations to Flag

This file lists allocation-producing patterns to search for, organized by severity and
detectability. Each entry includes grep-friendly search patterns and the recommended fix.

## Search Guidelines

When using Grep to search for these anti-patterns:
- **Always exclude** `Vendors/`, `Generated/`, and `test/` directories from results — these
  are vendored code, auto-generated code, and test code respectively
- **Check `#if` preprocessor context** around findings. Many optimization patterns (e.g.,
  `Span<T>`, `stackalloc`, `ValueTask`) require `NETCOREAPP` or `NETCOREAPP3_1_OR_GREATER`.
  Do not suggest TFM-specific fixes inside `#if NETFRAMEWORK` blocks
- **Verify path temperature** before reporting — an anti-pattern in a cold path is low priority

---

## Tracer-Specific Anti-Patterns

These come from the dd-trace-dotnet Performance Guidelines and are highest priority.

### AP-1: String Interpolation in Log Calls

**What allocates**: The interpolated string is constructed even if the log level is disabled.
On .NET Framework, also allocates a `string` + boxes value types.

**Search patterns**:
```
Log\.\w+\(\$"
Log\.\w+\(.*\$"
```

**Fix**: Use format strings with positional placeholders:
```csharp
// Bad:
Log.Debug($"Processing span {spanId} with tag {tagName}");

// Good:
Log.Debug("Processing span {SpanId} with tag {TagName}", spanId, tagName);
```

**Reference**: Pattern 6.1 (Generic Log Overloads)

### AP-2: `ToString()` on Numeric Types in Log Calls

**What allocates**: Unnecessarily converts to string, allocating a `string` object. The
generic log overloads handle formatting without allocation.

**Search patterns**:
```
Log\.\w+.*\.ToString\(\)
```

**Fix**: Pass the value directly:
```csharp
// Bad:
Log.Debug<string>("Error (attempt {Attempt})", (attempt + 1).ToString());

// Good:
Log.Debug<int>("Error (attempt {Attempt})", attempt + 1);
```

### AP-3: `params` Array Without Typed Overloads

**What allocates**: A new `object[]` on every call, plus boxing of any value-type arguments.

**Search patterns**: Look for public/internal methods with `params object[]` that are called
from hot paths without corresponding typed overloads.

**Fix**: Provide typed overloads for common arities (0-5 args):
```csharp
// Instead of only:
void Log(string template, params object[] args);

// Also provide:
void Log<T>(string template, T arg);
void Log<T0, T1>(string template, T0 arg0, T1 arg1);
```

**Reference**: Pattern 6.1 (Generic Log Overloads), `Logging/Internal/DatadogSerilogLogger.cs`

### AP-4: Boxing Through Interface Dispatch

**What allocates**: A boxed copy of the struct on every interface method call.

**Search patterns**: Look for methods that accept an interface parameter and are called with
struct arguments without a generic constraint:
```
void Process(IMyInterface item)  // boxing occurs when called with a struct
```

**Fix**: Use generic constraints:
```csharp
// Bad:
void Inject(ICarrierSetter setter) // boxes struct callers

// Good:
void Inject<TSetter>(TSetter setter) where TSetter : struct, ICarrierSetter
```

**Reference**: Pattern 3.4 (Generic Struct Constraints)

---

## General .NET Allocation Pitfalls

### AP-5: LINQ in Hot Paths

**What allocates**: Iterator objects, delegate objects for lambdas, sometimes intermediate
collections. Each LINQ method in a chain allocates at least one iterator.

**Search patterns**:
```
\.Where\(
\.Select\(
\.Any\(
\.First\(
\.OrderBy\(
\.ToList\(\)
\.ToArray\(\)
\.ToDictionary\(
```

**Fix**: Replace with `foreach` loops or array indexing:
```csharp
// Bad (3 allocations: Where iterator + Select iterator + ToList):
var names = items.Where(x => x.IsActive).Select(x => x.Name).ToList();

// Good (1 allocation: the list itself):
var names = new List<string>(items.Count);
foreach (var item in items)
{
    if (item.IsActive)
        names.Add(item.Name);
}
```

**Note**: LINQ is fine in cold paths (configuration, startup, error handling). Only flag it
in hot paths.

### AP-6: String Concatenation in Loops

**What allocates**: A new `string` on each `+` or `+=` operation.

**Search patterns**: Look for `+=` on string variables inside loops, or repeated `string.Concat`
/ `string.Format` calls.

**Fix**: Use `StringBuilderCache` or `ValueStringBuilder`:
```csharp
// Bad:
string result = "";
foreach (var item in items)
    result += item.Name + ",";

// Good:
var sb = StringBuilderCache.Acquire();
foreach (var item in items)
    sb.Append(item.Name).Append(',');
return StringBuilderCache.GetStringAndRelease(sb);
```

**Reference**: Pattern 2.3 (`StringBuilderCache`), Pattern 1.2 (`ValueStringBuilder`)

### AP-7: Closures Capturing Local Variables

**What allocates**: The compiler generates a display class (heap object) to hold captured
variables. A new delegate is also allocated.

**Search patterns**: Lambdas inside hot-path methods that reference local variables or `this`:
```csharp
// Look for lambdas that use variables from the enclosing scope:
var threshold = GetThreshold();
items.Where(x => x.Value > threshold);  // captures 'threshold'
```

**Fix**:
- Use `static` lambdas (C# 9+) when possible — the compiler rejects accidental captures
- Pass state through method parameters instead of closures
- Cache the delegate in a `static readonly` field if it doesn't capture
- For `Task.ContinueWith` and similar, use the `state` parameter overload

**Reference**: Pattern 6.2 (Static / Cached Delegates)

### AP-8: `Substring` Instead of `Span` Slicing

**What allocates**: A new `string` for each `Substring` call.

**Search patterns**:
```
\.Substring\(
```

**Fix**: Use `AsSpan()` / `ReadOnlySpan<char>` for intermediate parsing:
```csharp
// Bad:
var host = url.Substring(0, colonIndex);
var port = url.Substring(colonIndex + 1);

// Good:
ReadOnlySpan<char> host = url.AsSpan(0, colonIndex);
ReadOnlySpan<char> port = url.AsSpan(colonIndex + 1);
```

**Caveat**: Only if the result doesn't need to be stored as a `string`. If you ultimately
need a `string`, the allocation is unavoidable.

**Reference**: Pattern 4.2 (`ReadOnlySpan<char>` / `.AsSpan()`)

### AP-9: `Enum.ToString()` and `Enum.HasFlag()` Boxing

**What allocates**: On .NET Framework and older runtimes, `Enum.ToString()` boxes the enum
value. `Enum.HasFlag()` boxes both operands before .NET 7.

**Search patterns**:
```
\.ToString\(\)  // on enum-typed variables
\.HasFlag\(
```

**Fix**:
- For `ToString()`: Use a switch expression or lookup dictionary
- For `HasFlag()`: Use bitwise operations: `(flags & MyEnum.Value) != 0`

**Note**: .NET 7+ JIT optimizes `HasFlag` to avoid boxing. `ToString()` is still not free.

**Search caveat**: The `\.ToString\(\)` pattern is very broad — it matches all `ToString()`
calls, not just enum ones. Manually verify the variable is an enum type, or narrow the search
to known enum types in the codebase (e.g., `SpanKind`, `SamplingPriority`).

### AP-10: `IEnumerable<T>` Return Types on Hot Methods

**What allocates**: Returning `IEnumerable<T>` forces callers to use the interface
`GetEnumerator()`, which boxes struct enumerators. Also prevents callers from using
`Count` or indexing without `ToList()`.

**Search patterns**: Methods on hot paths returning `IEnumerable<T>` where a concrete type
would work.

**Fix**: Return concrete types (`List<T>`, `T[]`, or a custom struct collection):
```csharp
// Bad:
IEnumerable<Span> GetSpans() => _spans;

// Good:
SpanCollection GetSpans() => _spans;  // SpanCollection is a readonly struct
```

**Reference**: Pattern 3.3 (Struct Enumerators)

### AP-11: Missing `JsonArrayPool` on Newtonsoft.Json Readers/Writers

**What allocates**: Newtonsoft.Json internally allocates `char[]` buffers for tokenization.

**Search patterns**:
```
new JsonTextReader\(
new JsonTextWriter\(
```
Then check if `ArrayPool = JsonArrayPool.Shared` is set.

**Fix**:
```csharp
using var reader = new JsonTextReader(sr) { ArrayPool = JsonArrayPool.Shared };
```

**Reference**: Pattern 2.4 (`JsonArrayPool`)

### AP-12: `new StringBuilder()` Instead of `StringBuilderCache`

**What allocates**: A new `StringBuilder` on each call.

**Search patterns**:
```
new StringBuilder\(
```

**Fix**: Use `StringBuilderCache.Acquire()` / `GetStringAndRelease()` for strings under 360
chars. For longer strings or nested usage, `new StringBuilder()` is fine.

**Reference**: Pattern 2.3 (`StringBuilderCache`)

### AP-13: Throwing Exceptions Inline in Hot Paths

**What allocates**: The `throw` statement and exception object construction can prevent the
JIT from inlining the containing method.

**Search patterns**: `throw new` inside small methods that should be inlineable.

**Fix**: Move to a `ThrowHelper` method:
```csharp
// Bad:
if (value is null) throw new ArgumentNullException(nameof(value));

// Good:
if (value is null) ThrowHelper.ThrowArgumentNullException(nameof(value));
```

**Reference**: Pattern 5.2 (`ThrowHelper` with `[NoInlining]`)

### AP-14: `Task.Run` / `Task.ContinueWith` with Capturing Lambdas

**What allocates**: A display class for captures + a delegate + the `Task` wrapper.

**Search patterns**:
```
Task\.Run\(
\.ContinueWith\(
```

**Fix**: Use the `state` parameter overload to pass data without closures:
```csharp
// Bad:
Task.Run(() => Process(item));

// Good:
Task.Factory.StartNew(static state => Process((Item)state), item);
```

Or restructure to avoid `Task.Run` entirely in hot paths.

**Note**: `Task.Run` and `ContinueWith` are fine in cold paths (startup, configuration).
Only flag in hot paths.

### AP-15: `string.Split()` in Hot Paths

**What allocates**: `string.Split()` allocates a `string[]` plus a new `string` for each
segment — multiple heap allocations per call.

**Search patterns**:
```
\.Split\(
```

**Fix**: Use `SpanCharSplitter` (NETCOREAPP) or manual `IndexOf`-based parsing:
```csharp
// Bad:
foreach (var part in input.Split(','))
    Process(part);

// Good (NETCOREAPP — zero-alloc):
foreach (var part in new SpanCharSplitter(input, ',', int.MaxValue))
    Process(part.AsSpan());

// Good (.NET Framework — manual parsing):
int start = 0;
int idx;
while ((idx = input.IndexOf(',', start)) >= 0)
{
    Process(input, start, idx - start);
    start = idx + 1;
}
Process(input, start, input.Length - start);
```

**Reference**: Pattern 4.3 (`string.Split()` Replacement with `SpanCharSplitter`)

### AP-16: `async Task` Where `ValueTask` Would Suffice

**What allocates**: Every `async Task` method allocates a `Task` object and an async state
machine on the heap, even when the method completes synchronously (e.g., cache hit, early
return).

**Search patterns**: Look for `async Task` or `async Task<` methods on hot paths that have
early synchronous return paths (cache lookups, guard clauses, already-computed results).

**Fix**: Return `ValueTask` / `ValueTask<T>` to avoid the `Task` allocation on synchronous
completion:
```csharp
#if NETCOREAPP3_1_OR_GREATER
internal ValueTask<string> GetValueAsync()
{
    if (_cache.TryGetValue(key, out var value))
        return new ValueTask<string>(value); // no heap allocation

    return GetValueSlowAsync();
}
#else
internal Task<string> GetValueAsync()
{
    if (_cache.TryGetValue(key, out var value))
        return Task.FromResult(value);

    return GetValueSlowAsync();
}
#endif
```

**Caveats**:
- `ValueTask` requires `NETCOREAPP3_1_OR_GREATER` for pooled async builders — use `#if` guards
- `ValueTask` can only be awaited once — do not cache or await multiple times
- Only beneficial when synchronous completion is common; use `Task` for always-async operations

**Reference**: Pattern 6.3 (`ValueTask` / `ValueTask<T>` for Hot Async Paths)
