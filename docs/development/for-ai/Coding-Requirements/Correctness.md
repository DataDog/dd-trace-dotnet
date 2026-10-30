# Correctness

Bugs, races, unhandled edge cases, and tests that pass when they shouldn't.

**When planning:** take the rules below into account when designing the change.

**When reviewing:** report a finding when the diff matches a rule's `Flag:` line and no `Not when:` applies. Cite the rule ID, location, and the failing input or interleaving.

## 1. Nullability

### COR-1.1: Declare the value type nullable (`Dictionary<string, T?>`) instead of storing `null!`
- Flag: `null!` (or a `!` suppression) used to store or return a real null into a non-nullable type, or a property/signature whose nullability differs from what its converter/deserializer produces.
- ❌ `result[name] = IsValid(flag) ? flag : null!; // Dictionary<string, Flag>`
- ✅ `result[name] = IsValid(flag) ? flag : null;  // Dictionary<string, Flag?>`

### COR-1.2: Assume values coming from integrations or manual APIs could be null
- Flag: A value received from an integration or manual API should be assumed to be nullable, unless structurally proven otherwise

## 2. Concurrency

### COR-2.1: Use `Volatile.Read`/`Volatile.Write`/`Interlocked.Exchange` for fields accessed across threads outside their lock
- Flag: a field that is written under a lock, via `Interlocked`, or shared across `ExecutionContext`s, but is read or cleared with a plain access elsewhere; or only some fields of one published record are written with `Volatile.Write`.
- Not when: readers re-validate a version/timestamp after reading, or the write completes before any reader can exist.
- ❌ `_appSecRequestContext?.CloseWebSpan(span);`
- ✅ `Volatile.Read(ref _appSecRequestContext)?.CloseWebSpan(span);`

### COR-2.2: Return the `Interlocked.CompareExchange` result instead of re-reading the field
- Flag: `Interlocked.CompareExchange(ref _field, created, null);` followed by a separate read of `_field`.
- ❌ `Interlocked.CompareExchange(ref _context, created, null); return _context!;`
- ✅ `return Interlocked.CompareExchange(ref _context, created, null) ?? created;`

### COR-2.3: Make the `_disposed` check and the inner call atomic (shared lock or ref-count)
- Flag: `if (_disposed) return; _inner.Do();` in a type whose `Dispose()` can run concurrently on another thread.
- ❌ `if (_disposed) { return; } _innerBus.QueueMessage(message);`
- ✅ `lock (_lifecycleLock) { if (_disposed) { return; } _innerBus.QueueMessage(message); }`

### COR-2.4: Synchronise replay-on-register with the update path so a handler runs once per update
- Flag: a `Register`/`Subscribe` method that immediately invokes the new handler when state already exists, while the update path can invoke the same handler concurrently with no shared lock or generation check.

## 3. Runtime settings

### COR-3.1: Use `Tracer.Instance.CurrentTraceSettings` / `SettingsManager.SubscribeToChanges`, not `Manager.Initial*Settings`
- Flag: `Settings.Manager.InitialMutableSettings` or `InitialExporterSettings` read outside startup, or used to compute a value (env, endpoint, exporter mode) cached for the process lifetime.
- ❌ `if (Tracer.Settings.Manager.InitialExporterSettings.IsOtlpTraceExport) { ... }`
- ✅ `_isOtlp = settingsManager.InitialExporterSettings.IsOtlpTraceExport; settingsManager.SubscribeToChanges(changes => { if (changes.UpdatedExporter is { } e) { Volatile.Write(ref _isOtlp, e.IsOtlpTraceExport); } });`

### COR-3.2: When a mode forces one setting, also force off every setting that would contradict it, even if explicitly enabled
- Flag: `if (ModeEnabled) { A = ...; }` in `TracerSettings` while a related user-controllable setting (e.g. `ExpandRouteTemplatesEnabled`, peer-service/V1 tags) can still undo or contradict the forced behaviour.
- ❌ `if (OtelSemanticsEnabled) { RouteTemplateResourceNamesEnabled = true; }`
- ✅ `if (OtelSemanticsEnabled) { RouteTemplateResourceNamesEnabled = true; ExpandRouteTemplatesEnabled = false; }`

## 4. Platform and TFM guards

### COR-4.1: Guard single-OS native code with a positive check (`IsLinux`), not `!IsWindows`
- Flag: a negated OS check (`!IsWindows`, `if (IsWindows) return`) protecting code that only works on one other OS, so macOS also takes the path.
- ❌ `if (IsWindows) { return IntPtr.Zero; } return ResolveLinuxSymbol();`
- ✅ `if (!IsLinux) { return IntPtr.Zero; } return ResolveLinuxSymbol();`

### COR-4.2: Put a TFM-limited setting and any override that forces it inside the feature's minimum-TFM `#if`
- Flag: a setting forced on/off outside the `#if` that gates the feature's support, or a guard that differs from the sibling feature's (`NETCOREAPP3_1_OR_GREATER` where the feature needs `NET6_0_OR_GREATER`).
- ❌ `if (OtelSemanticsEnabled) { SingleSpanAspNetCoreEnabled = true; } // all TFMs, conflicts with the pre-.NET 6 force-disable`
- ✅ `#if NET6_0_OR_GREATER` … `if (OtelSemanticsEnabled) { SingleSpanAspNetCoreEnabled = true; }` … `#endif`

## 5. Edge cases and resources

### COR-5.1: Use `StringUtil.IsNullOrEmpty` (not `??`) when an empty string should also fall back
- Flag: `value ?? fallback` on a string where `""` is not a meaningful value (paths, URLs, names).
- ❌ `var dir = _outputPath ?? Environment.CurrentDirectory;`
- ✅ `var dir = StringUtil.IsNullOrEmpty(_outputPath) ? Environment.CurrentDirectory : _outputPath;`

### COR-5.2: Give each stream layer one owner: `using` the layers you create, and `leaveOpen: true` on wrappers over a stream something else disposes
- Flag: `GZipStream`/`StreamReader` built over another stream without `using` on each layer you create, disposed manually in some branches only, or a wrapper that disposes a stream another layer (or the caller) also disposes.
- ❌ `var reader = new StreamReader(new GZipStream(stream, CompressionMode.Decompress));`
- ✅ `using var decompressed = new GZipStream(stream, CompressionMode.Decompress); using var reader = new StreamReader(decompressed, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);`

### COR-5.3: Give `[InstrumentMethod]` exact `ParameterTypeNames`, not the `"_"` wildcard
- Flag: `ParameterTypeNames = new[] { "_" }` (or any `"_"` entry) where the parameter type is known, so a future overload could bind instead.
- ❌ `ParameterTypeNames = new[] { "_" }`
- ✅ `ParameterTypeNames = new[] { "!0" }`

## 6. Tests

### COR-6.1: Create a fresh instance per test instead of using a static `.Instance` or the global tracer
- Flag: unit tests using a `SomeType.Instance` singleton, `[TracerRestorer]`/`SetTracer`, or `Tracer.Instance` where a locally constructed instance would do, or a `Reset()`/`Clear()` added to production state only so tests can reuse it.
- ❌ `var store = CoreHttpContextStore.Instance;`
- ✅ `var store = new CoreHttpContextStore();`

### COR-6.2: Skip a test explicitly instead of commenting out its body or assertions
- Flag: a commented-out test body or assertion (including sanity asserts in shared helpers/normalizers), or a body replaced by `return Task.CompletedTask;`.
- ❌ `// return base.FlakyRetries(packageVersion); return Task.CompletedTask;`
- ✅ `throw new SkipException("Not supported on xUnit v3: <reason>");`

### COR-6.3: Filter `WaitForSpansAsync` to the spans under test and assert the returned count
- Flag: `await agent.WaitForSpansAsync(n)` whose result count is never asserted, or whose count includes background spans (monitor/admin/health) that can satisfy it early.
- ❌ `await agent.WaitForSpansAsync(expectedCount);`
- ✅ `var spans = await agent.WaitForSpansAsync(expectedCount, operationName: ExpectedOperationName); spans.Should().HaveCount(expectedCount);`

### COR-6.4: Wait on events in tests, not on time
- Flag: a test that waits for a condition with `Thread.Sleep`, `Task.Delay` or a short timed wait, or a timeout raised to fix a flaky test. Wait on a `TaskCompletionSource`, a `std::promise` or a marker queued behind the work instead, and give the test a large (30s+) timeout so a failure can't hang it.
- Not when: the time bound detects a failure that no event can report, such as missing output from another process (`MockTracerAgent.WaitForSpans`), a deadlock probe or a check that something stays blocked, and a comment says why.
- ❌ `Thread.Sleep(500); processed.Should().BeTrue();`
- ✅ `await processedTcs.Task; // set by the handler`

## Don't flag
- A call made outside a lock when the caller holds a lifetime lease that the remover must acquire first.
- An "edge case" that the callee's contract makes impossible (e.g. a delegate wrapper that cannot return both an exception and a task).
