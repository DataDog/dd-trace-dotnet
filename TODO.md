# TODO

## Allocation Analyzers

New Roslyn analyzers to detect heap-allocation anti-patterns at compile time. Add to `tracer/src/Datadog.Trace.Tools.Analyzers/` (analyzer) and `tracer/src/Datadog.Trace.Tools.Analyzers.CodeFixes/` (code fix). Both projects target `netstandard2.0`. Use `DDALLOC` prefix for diagnostic IDs. Follow existing conventions (see `LogAnalyzer/` or `SealedAnalyzer/` for examples).

- [ ] **DDALLOC001: `ToString()` on numeric types in log calls**
  - Detect calls to `IDatadogLogger` methods (`Debug`, `Information`, `Warning`, `Error`, `ErrorSkipTelemetry`) where any argument has `.ToString()` called on a numeric type (`int`, `long`, `double`, etc.)
  - The generic log overloads (`Log.Debug<int>(...)`) handle formatting without allocating a string — the `.ToString()` is unnecessary and allocates
  - Code fix: remove the `.ToString()` call and pass the value directly; if the log call uses explicit generic type args (e.g., `Log.Debug<string>(...)`), update the type arg to match the numeric type
  - Example: `Log.Debug<string>("attempt {Attempt}", (attempt + 1).ToString())` → `Log.Debug<int>("attempt {Attempt}", attempt + 1)`
  - Severity: Warning
  - Extends the existing `LogAnalyzer` infrastructure in `tracer/src/Datadog.Trace.Tools.Analyzers/LogAnalyzer/`
  - [ ] Remove AP-2 from `.claude/skills/find-allocation-opportunities/references/anti-patterns.md` once analyzer is working

- [x] **DDALLOC002: Missing `JsonArrayPool` on Newtonsoft.Json readers/writers**
  - Detect `new JsonTextReader(...)` and `new JsonTextWriter(...)` object creations (types: `Newtonsoft.Json.JsonTextReader`, `Newtonsoft.Json.JsonTextWriter`) where the `ArrayPool` property is not set to `JsonArrayPool.Shared` in an object initializer
  - Without `ArrayPool`, Newtonsoft.Json allocates internal `char[]` buffers on every read/write
  - Code fix: add `{ ArrayPool = JsonArrayPool.Shared }` object initializer (or append to existing initializer)
  - `JsonArrayPool` lives at `Datadog.Trace.Util.Json.JsonArrayPool`
  - Example: `new JsonTextReader(sr)` → `new JsonTextReader(sr) { ArrayPool = JsonArrayPool.Shared }`
  - Severity: Warning
  - [x] Remove AP-11 from `.claude/skills/find-allocation-opportunities/references/anti-patterns.md` once analyzer is working

- [ ] **DDALLOC003: `new StringBuilder()` instead of `StringBuilderCache`**
  - Detect `new StringBuilder()` and `new StringBuilder(int)` object creations in tracer code
  - `StringBuilderCache` (`Datadog.Trace.Util.StringBuilderCache`) uses a `[ThreadStatic]` cached instance — avoids allocating a new `StringBuilder` per call
  - Code fix: replace `new StringBuilder()` with `StringBuilderCache.Acquire()` and ensure the result is consumed via `StringBuilderCache.GetStringAndRelease(sb)`. This fix is complex (needs to track usage to insert the release call), so a diagnostic-only analyzer (no code fix) is acceptable as a first pass
  - Suppress when: capacity > 360 (exceeds `StringBuilderCache` max), or inside a method that already calls `StringBuilderCache.Acquire()` (not nestable on the same thread)
  - Severity: Info (suggestion)
  - [ ] Remove AP-12 from `.claude/skills/find-allocation-opportunities/references/anti-patterns.md` once analyzer is working

- [ ] **DDALLOC004: `Enum.HasFlag()` boxing**
  - Detect calls to `.HasFlag()` on enum-typed expressions
  - On .NET Framework and pre-.NET 7 runtimes, `Enum.HasFlag()` boxes both operands. Since the tracer targets `netstandard2.0`/`net462`+, this is always relevant
  - Code fix: rewrite `flags.HasFlag(MyEnum.Value)` → `(flags & MyEnum.Value) != 0`. For multi-flag checks like `flags.HasFlag(MyEnum.A | MyEnum.B)`, rewrite to `(flags & (MyEnum.A | MyEnum.B)) == (MyEnum.A | MyEnum.B)`
  - Severity: Warning
  - [ ] Remove AP-9 (`HasFlag` portion) from `.claude/skills/find-allocation-opportunities/references/anti-patterns.md` once analyzer is working

- [ ] **DDALLOC005: `Enum.ToString()` allocates**
  - Detect `.ToString()` calls on enum-typed expressions (use semantic model: check if receiver type's `TypeKind == TypeKind.Enum`)
  - `Enum.ToString()` boxes the value and allocates a string via reflection on all runtimes
  - Code fix is hard to auto-generate (would need a switch expression over all enum members), so diagnostic-only is fine. Message should suggest using a switch expression or cached dictionary lookup
  - Severity: Info (suggestion) — lower priority since the fix is manual
  - [ ] Remove AP-9 (`ToString` portion) from `.claude/skills/find-allocation-opportunities/references/anti-patterns.md` once analyzer is working

- [ ] **DDALLOC006: Capturing lambdas in `Task.Run` / `ContinueWith`**
  - Detect lambdas passed to `Task.Run(...)`, `Task.Factory.StartNew(...)`, and `.ContinueWith(...)` that are not marked `static` (C# 9+) and capture variables from the enclosing scope
  - Capturing lambdas allocate a compiler-generated display class + a delegate object. The `state` parameter overloads avoid this
  - Use semantic model's data flow analysis (`AnalyzeDataFlow`) on the lambda to check for captured variables, or check for non-`static` lambdas that reference identifiers from enclosing scope
  - No auto-fix — restructuring to use `state` parameter varies too much. Diagnostic message should suggest: (1) use `static` lambda if no captures needed, (2) use `Task.Factory.StartNew(static state => ..., stateObj)` overload to pass state without closure
  - Severity: Info (suggestion)
  - [ ] Remove AP-14 from `.claude/skills/find-allocation-opportunities/references/anti-patterns.md` once analyzer is working

- [ ] **DDALLOC007: `throw new` in `[AggressiveInlining]` methods**
  - Detect `throw new XxxException(...)` statements inside methods decorated with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
  - `throw` prevents the JIT from inlining the method, defeating the purpose of `[AggressiveInlining]`. The throw should be moved to a separate `[MethodImpl(MethodImplOptions.NoInlining)]` helper (see `ThrowHelper` pattern at `Util/ThrowHelper.cs`)
  - No auto-fix — the developer needs to decide the ThrowHelper method name/location. Diagnostic message should suggest extracting to a `ThrowHelper` method with `[DoesNotReturn]` and `[NoInlining]`
  - Severity: Warning
  - [ ] Remove AP-13 from `.claude/skills/find-allocation-opportunities/references/anti-patterns.md` once analyzer is working
