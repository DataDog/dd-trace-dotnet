# Performance

Allocations and CPU cost on hot paths (span creation and finish, tagging, propagation, instrumentation callbacks), at startup, and in tooling.

**When planning:** take the rules below into account when designing the change.

**When reviewing:** report a finding when the diff matches a rule's `Flag:` line on a hot, startup, or high-volume path. Cite the rule ID and say which path it is on.

## 1. Strings and parsing

### PERF-1.1: Slice `ReadOnlySpan<char>` instead of `Substring`/`Split`/`Trim` when parsing
- Flag: a `Substring`/`string.Split`/`Trim()` result that is only fed into another string op, a comparison, or an interpolation.
- ❌ `var prefix = path.Substring(0, markerPos); return $"{host}{prefix}";`
- ✅ `var prefix = path.AsSpan(0, markerPos);` (or `str.SplitIntoSpans('/')` instead of `Split`)

### PERF-1.2: Compare with `StringComparison.OrdinalIgnoreCase` instead of `ToLowerInvariant()`
- Flag: `ToLowerInvariant()`/`ToUpperInvariant()` (often after `Trim()`) used only to compare against a literal.
- ❌ `Path.GetExtension(file).ToLowerInvariant() == ".dll"`
- ✅ `string.Equals(Path.GetExtension(file), ".dll", StringComparison.OrdinalIgnoreCase)`

### PERF-1.3: Check blanks with `StringUtil.IsNullOrWhiteSpace` instead of `Trim()`
- Flag: `Trim()` called only to test whether a value is empty.
- ❌ `x = value?.Trim().Length > 0 ? value : null;`
- ✅ `x = !StringUtil.IsNullOrWhiteSpace(value) ? value : null;`

### PERF-1.4: Use `IntStringCache.ToInvariantString` instead of `int.ToString()` for tag values
- Flag: an `int` tag/status code converted with `ToString()` on a serialization, sampling, or tagging path, or formatted only to be parsed back to `int`.
- ❌ `writer.Write(item.Value.ToString());`
- ✅ `writer.Write(IntStringCache.ToInvariantString(item.Value));`

### PERF-1.5: Keep pre-encoded UTF-8 tag bytes as bytes; don't decode them to split
- Flag: `Encoding.UTF8.GetString(...)` over a cached UTF-8 tag buffer to recover its key/value.
- ❌ `var s = Encoding.UTF8.GetString(tagBytes); var key = s.Split(':')[0];`
- ✅ store key/value lengths alongside the single buffer and expose `ReadOnlySpan<byte>` slices.

### PERF-1.6: Size `stackalloc` buffers with `Encoding.GetMaxByteCount`, not `GetByteCount`
- Flag: `GetByteCount(str)` used only to choose between `stackalloc` and a heap/pooled buffer.
- ❌ `var size = Encoding.UTF8.GetByteCount(value);`
- ✅ `var size = Encoding.UTF8.GetMaxByteCount(value.Length);`

## 2. Buffers and allocation

### PERF-2.1: Use `stackalloc` (≤ 512 bytes) or `ArrayPool<T>.Shared` for temporary arrays
- Flag: `new T[]`/`ToArray()` for a scratch buffer discarded before the method returns, or a `stackalloc` threshold above 512 bytes.
- ❌ `var sorted = ids.ToArray(); Array.Sort(sorted);`
- ✅ `if(count <= 32) {sorted = stackalloc long[count];} else { rented = ArrayPool<long>.Shared.Rent(count); sorted = rented.AsSpan(0, count);}`

### PERF-2.2: Deserialize JSON from the response stream straight into the target type
- Flag: reading a response body into a `string`, or parsing into `JToken`/`JObject`, before extracting the part you need.
- ❌ `var body = await response.ReadAsStringAsync(); var token = JToken.Parse(body);`
- ✅ `using var reader = new JsonTextReader(new StreamReader(stream)); serializer.Deserialize<TConfig>(reader);`

### PERF-2.3: Put opt-in feature fields on a lazily created nested tags object
- Flag: new fields added directly to a widely used tags class (e.g. `WebTags`) that are only set when an opt-in feature is enabled.
- ❌ `public string? OtelHttpRoute { get; set; }` on `WebTags`
- ✅ `public OtelTags? Otel { get; private set; }` created only when the feature is on.

### PERF-2.4: Mark non-mutating members of a mutable struct `readonly`
- Flag: a non-`readonly` struct whose getters/methods that don't write state lack the `readonly` modifier.
- Not when: questioning whether a mutable struct passed by `ref` on a hot path should be a class.
- ❌ `internal bool IsInitialized => _state != State.Uninitialized;`
- ✅ `internal readonly bool IsInitialized => _state != State.Uninitialized;`

### PERF-2.5: Cap long-lived collections that grow with application data
- Flag: a static or long-lived `Dictionary`/`ConcurrentDictionary`/`HashSet`/`List`/queue that adds entries keyed or sized by application data (URLs, resource names, tag values, exception types, customer types) with no size limit or eviction.
- ❌ `_statsByResource.GetOrAdd(resourceName, _ => new Stats());`
- ✅ `if (_statsByResource.Count < MaxEntries) { _statsByResource.TryAdd(resourceName, new Stats()); } else { _overflow.Increment(); }`

## 3. Hot paths

### PERF-3.1: Defer hashing/encoding/`SetTag` from `Span.Finish` and per-call paths to the serializer thread
- Flag: SHA-256, varint/base64 encoding, collection building, or `Tags.SetTag` with computed values in `Span.Finish` or per-evaluation code; store the raw input and transform at serialization.
- ❌ `_subjects.Add(Sha256Helper.Hash(targetingKey));`
- ✅ `_subjects.Add(targetingKey);` (hash when the trace is serialized)

### PERF-3.2: Remove `Log.Debug` calls whose arguments do work on instrumentation paths
- Flag: `Log.Debug(...)` in an integration callback whose arguments call methods (e.g. `GetType()`), evaluated even when debug logging is off.
- ❌ `Log.Debug("Descriptor type {Type}", actionDescriptor?.GetType());`
- ✅ remove the log, or guard it with `if (Log.IsEnabled(LogEventLevel.Debug))`.

### PERF-3.3: Emit one aggregated log, not one log per counter or per invalid value
- Flag: a sequence of `Log.Warning` calls, one per drop reason/counter, or a log per invalid configuration value (already reported via configuration telemetry).
- ❌ `if (a > 0) Log.Warning(...a); if (b > 0) Log.Warning(...b);`
- ✅ `if (a != 0 || b != 0) Log.Warning("Dropped traces: {A} {B}", a, b);`

### PERF-3.4: Don't block inlining of small hot helpers with `try/catch` or `[MethodImpl(NoInlining)]`
- Flag: a `try/catch` around a call that shouldn't throw (e.g. `GetActiveScope()`), or `NoInlining` on a small helper on a hot path.
- Not when: the `try/catch` wraps third-party access with a known throw case, or `NoInlining` is on a rarely-taken slow path whose inlining would bloat every caller.
- ❌ `try { scope = tracer.InternalActiveScope; } catch { ... }`
- ✅ `scope = tracer.InternalActiveScope;`

### PERF-3.5: Match small fixed key sets with an `is "a" or "b"` pattern instead of a `HashSet`
- Flag: a `HashSet<string>` lookup over a handful of constants on a per-read or per-call path.
- ❌ `static readonly HashSet<string> Keys = new() { "A", "B" }; ... Keys.Contains(key)`
- ✅ `key is "A" or "B"`

## 4. Startup and tooling

### PERF-4.1: Reuse the existing settings object instead of constructing `ExporterSettings`/`TracerSettings` again at startup
- Flag: `new ExporterSettings(...)`/`new TracerSettings(...)` built a second time during initialization when an instance is already available.
- ❌ `var exporter = new ExporterSettings(source, telemetry);`
- ✅ `internal void Initialize(ExporterSettings exporter) // pass in the instance the tracer already built`

### PERF-4.2: Avoid LINQ in analyzers, source generators, and hot paths.
- Flag: LINQ chains (`Where`/`Select`/`ToList`/`GroupBy`) in a Roslyn analyzer or generator callback, instrumentation code, or other hot path.
- ❌ `options.AdditionalFiles.Where(...).Select(...).ToList()`
- ✅ a plain `foreach` over `options.AdditionalFiles`.

## Don't flag
- Duplicate `ReadOnlySpan<byte>` literal properties across generated tag classes (the compiler shares the blob).
- Proposals to split a hot callback into per-feature-combination variants, or to replace an uncontended per-request `lock` with `Interlocked`, without a measurement.
- Interface dispatch vs type-pattern-matching trade-offs on tag classes without a benchmark.
