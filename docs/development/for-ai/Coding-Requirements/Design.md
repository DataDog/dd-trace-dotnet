# Design

Where code belongs, who owns state, and API and type shape.

**When planning:** take the rules below into account when designing the change.

**When reviewing:** report a finding when the diff matches a rule's `Flag:` line and no `Not when:` applies. Cite the rule ID and propose the concrete alternative.

## 1. Settings and state ownership

### DES-1.1: Read DD_ENV, DD_SITE, and DD_API_KEY from the tracer's settings objects, not from `IConfigurationSource`
- Flag: a new settings class or component calls `config.WithKeys(ConfigurationKeys.Environment|Site|ApiKey)` (or another key the tracer already extracts) itself, or native code re-reads an env var (e.g. `DD_PROFILING_ENABLED`) that the managed side already resolves. If the key isn't on the shared settings yet (today `DD_SITE` and `DD_API_KEY`), the fix is to extract it there once and pass it in, not to add another independent read.
- ❌ `Env = config.WithKeys(ConfigurationKeys.Environment).AsString();`
- ✅ `Env = mutableSettings.Environment;`

### DES-1.2: Add new product settings to `TracerSettings` and pass global state in, instead of reading statics
- Flag: a settings class or new component with a static `FromDefaultSource()` factory, or one that reads `Tracer.Instance` or a global helper such as `ProfilerAvailabilityHelper.IsContinuousProfilerAvailable`.
- ❌ `internal static FeatureFlagsSettings FromDefaultSource() => new(GlobalConfigurationSource.Instance, ...);`
- ✅ `FeatureFlags = new FeatureFlagsSettings(source, telemetry); // in the TracerSettings ctor, like TelemetrySettings`

### DES-1.3: Move a type shared between products into a core namespace instead of referencing another product's type
- Flag: a `using` or type reference that reaches from one product (CI Visibility, Debugger, AppSec) into another product's namespace for a general-purpose helper (for example, `FeatureFlags` `SemanticVersion`).
- ❌ `using Datadog.Trace.FeatureFlags; ... SemanticVersion.TryParse(dir, out var v)` (in the coverage collector)
- ✅ `RuntimeVersion.TryParse(dir, out var v)` (a dedicated type, or one moved to `Datadog.Trace.Util`)

## 2. Fix components instead of working around issues

### DES-2.1: Fix transport or sidecar bugs in common components, not with caller-side workarounds
- Flag: an integration bypasses `IApiRequestFactory`/`IApiRequest` to work around a transport quirk, or the tracer adds health checks, debouncing, or retries for a bug in dogstatsd or the agent that ships alongside it.
- ❌ `if (metadata.IsHealthyDebounced()) // works around dogstatsd staying alive after failing to bind its pipe`
- ✅ Fix `ApiWebRequest` (or dogstatsd), and keep the caller on the shared abstraction.

### DES-2.2: Don't add code paths for states the tracer doesn't support or can't reach
- Flag: new branches and tests that reopen or resurrect state after a terminal operation (span added after the trace segment closed or serialized) or guard a state that's no longer reachable (uninitialized `HttpContext` after the store is cleared).
- ❌ `if (_openSpans == 1 && _segmentClosed) { _segmentClosed = false; _appSecRequestContext?.ReopenAdditiveContext(); }`
- ✅ Keep disposal terminal and treat late spans as unsupported.

## 3. Type and API shape

### DES-3.1: Replace a value property plus a `HasX` flag with one nullable property
- Flag: a type gains both `X` and `bool HasX`, or code sets `HasX = true` before `X` is assigned.
- ❌ `internal EvaluationBudget EvaluationBudget { get; set; } internal bool HasEvaluationBudget { get; set; }`
- ✅ `internal EvaluationBudget? EvaluationBudget { get; set; }`

### DES-3.2: Remove optionality that no caller uses
- Flag: a new nullable parameter that every call site supplies, or a new overload or parameter (for example, `Encoding`) that every caller passes the same value to.
- ❌ `public static string ComputeHashAsHexString(string input, Encoding encoding)` (every caller passes UTF-8)
- ✅ `public static string ComputeHashAsHexString(string input)`

### DES-3.3: Make a behaviour-switching `bool` parameter required
- Flag: a new `bool` parameter that changes semantics and has a default value (`= false`).
- ❌ `internal static void DecorateWebServerSpan(..., WebTags tags, bool otelSemanticsEnabled = false)`
- ✅ `internal static void DecorateWebServerSpan(..., WebTags tags, bool otelSemanticsEnabled)`

### DES-3.4: Use a `class` for a rarely created type that has non-nullable reference fields
- Flag: a new `struct` whose non-nullable reference field (`Uri`, `string`) is `null` in `default(T)` and is created only once or rarely (for example, once per app).
- Not when: the struct avoids an allocation on a per-call hot path (for example, once per probe hit).
- ❌ `internal readonly struct AgentlessEndpoint { public Uri Uri { get; } }`
- ✅ `internal sealed class AgentlessEndpoint { public Uri Uri { get; } }`

### DES-3.5: Throw from public APIs on failure, and document when customers call them
- Flag: a public method that silently returns when it times out or fails (for example, an `InitializeAsync` that completes after the timeout), or a new public method with no XML summary saying when and why to call it.
- ❌ `await Task.WhenAny(ready, Task.Delay(timeout)); return;`
- ✅ `if (await Task.WhenAny(ready, Task.Delay(timeout)) != ready) { throw new TimeoutException(...); }`

### DES-3.6: Put new responsibilities in a dedicated type, not in an unrelated existing class
- Flag: discovery, parsing, matching, or integration-specific lookup logic added to a class whose name doesn't cover it (for example, CODEOWNERS parsing in `CIEnvironmentValues`, or a WCF `OperationContext` lookup in `ProbeProcessor`).
- ❌ `partial class CIEnvironmentValues { ... ParseCodeOwners(...); MatchCodeOwners(...); }`
- ✅ `new CodeOwnersResolver(locator, parser).Resolve(path)`

### DES-3.7: Give each mode its own implementation type instead of mode checks across callers
- Flag: `if (_isOtlp)` (or a similar mode flag) branches across two or more classes that call different setup APIs (`SetStartTime` vs `SetMinStartTime`) for each mode.
- ❌ `if (_isOtlp) { _buffers[i].SetStartTime(now); }`
- ✅ `_buffers[i] = _isOtlp ? new OtlpStatsBuffer(...) : new DatadogStatsBuffer(...);`

## 4. Configuration and analyzers

### DES-4.1: Where possible, favour using analyzers and build-time checks over runtime checks
- Flag: a per-read check in `ConfigurationBuilder` or the telemetry path (for example, `IsSensitive(key)`) for a property that's known at compile time from `supported-configurations.yaml`.
- ❌ `var record = !SensitiveKeys.IsSensitive(key); // on every configuration read`
- ✅ An analyzer that requires `AsRedactedString()` or `record: false` for keys marked `sensitive: true`.

### DES-4.2: Report analyzer diagnostics on the invocation the code fix replaces, with a fix for every variant
- Flag: an analyzer reports on the key member access (`ConfigurationKeys.X`) instead of the accessor call, or the code fix covers `AsString()` but not the other flagged accessors (`AsStringResult`, `AsDictionaryResult`, `AsString(default)`).
- ❌ `Diagnostic.Create(rule, memberAccess.GetLocation(), key)`
- ✅ `Diagnostic.Create(rule, accessorInvocation.GetLocation(), key)` plus a `CodeFixProvider` for each accessor.

## 5. Telemetry

### DES-5.1: Surface new drop or failure reasons as distinct telemetry metric tags
- Flag: a new drop, skip, or failure reason added only to logs or a local counter, while the existing telemetry metric for that path isn't tagged with it.
- ❌ `Log.Debug("Dropping trace: buffer locked"); _droppedCount++;`
- ✅ `TelemetryFactory.Metrics.RecordCountTraceChunkDropped(MetricTags.DropReason.<NewReason>);`

## 6. Build and packaging

### DES-6.1: Wrap runtime-specific code in `#if NETFRAMEWORK` or `#if !NETFRAMEWORK`, in its own file or type
- Flag: .NET Framework-only logic (for example, `DataContract` handling) interleaved with shared code without a guard, or an ASP.NET Core-only type compiled into the .NET Framework build.
- ❌ `internal static class AspNetCoreEndpointCodeOrigin { ... }` (unguarded)
- ✅ `#if !NETFRAMEWORK` around the whole type

### DES-6.2: Scope assets and build items to the projects or packages that use them
- Flag: an assembly added to every monitoring-home package when only one deployment uses it (for example, Kubernetes-only, so lib-injection or SSI images only), or an item added in `Directory.Build.props` when only one project needs it.
- ❌ `<AdditionalFiles Include="...\supported-configurations.yaml" />` in `tracer/src/Directory.Build.props`
- ✅ The same item in `Datadog.Trace.csproj` only

### DES-6.3: Put platform-specific build steps in their own Nuke target, invoked only where needed
- Flag: a library-specific or platform-specific publish step bolted into an unrelated existing target (for example, `CompileTrimmingSamples`) behind special-case guards, on the build critical path.
- ❌ `PublishXUnitV4SamplesForCurrentPlatform(); // inside CompileTrimmingSamples`
- ✅ `Target CompilePlatformSpecificSamples => _ => _.Executes(...)`, depended on only by the jobs that need it

## Don't flag
- An integration that is disabled by default (`IsIntegrationEnabled(..., defaultValue: false)`) while a stacked series of PRs is still in flight.
- Reading a config key through the normal `ConfigurationBuilder` pipeline even though that records telemetry from every source, when the spec accepts every source.
