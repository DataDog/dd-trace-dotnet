# Codebase conventions

Repo-specific idioms: configuration, logging, shared helpers, test patterns, and build tooling. [AGENTS.md](../../../../AGENTS.md) also applies.

**When planning:** take the rules below into account when designing the change.

**When reviewing:** report a finding when the diff matches a rule's `Flag:` line and no `Not when:` applies. Cite the rule ID and name the helper or pattern to use.

## 1. Configuration

### CONV-1.1: Read settings through `config.WithKeys(...)`, never from the source directly or with null telemetry
- Flag: `source.GetString(...)`/`GetInt32(...)` on an `IConfigurationSource`, a `ConfigurationBuilder` created with `NullConfigurationTelemetry`, or a test asserting a key is *not* recorded in telemetry.
- ❌ `var ms = source.GetInt32("DD_INTERNAL_TIMEOUT_MS", NullConfigurationTelemetry.Instance, ...);`
- ✅ `var ms = config.WithKeys(ConfigurationKeys.Debugger.EvaluationTimeoutMs).AsInt32(DefaultTimeoutMs);`

### CONV-1.2: Pass validators and converters into the `As*`/`GetAs` call, not after it
- Flag: a range check, `switch`, or source-precedence logic applied to the value returned from `config.WithKeys(...)`, so telemetry records the raw rather than effective value.
- ❌ `var n = config.WithKeys(Key).AsInt32(Default); if (n <= 0) n = Default;`
- ✅ `var n = config.WithKeys(Key).AsInt32(Default, static x => x > 0).Value;` (or `GetAs(defaultResult, validator, converter)`)

### CONV-1.3: Return `ParsingResult<T>.Failure()` from converters for unrecognised values
- Flag: a `GetAs` converter that returns `ParsingResult<T>.Success(fallback)` for input it doesn't recognise.
- ❌ `_ => ParsingResult<Mode>.Success(Mode.Offline),`
- ✅ `_ => ParsingResult<Mode>.Failure(),`

### CONV-1.4: Override dependent settings in `TracerSettings` and record them as `ConfigurationOrigins.Calculated`
- Flag: a call site that ORs another flag into a setting (`settings.X || otherFeatureEnabled`) instead of fixing the effective value once in `TracerSettings`.
- ❌ `if (settings.RouteTemplateResourceNamesEnabled || otelSemanticsEnabled)`
- ✅ set `RouteTemplateResourceNamesEnabled = true` in `TracerSettings` and record it with `ConfigurationOrigins.Calculated`.

### CONV-1.5: Add new experimental feature keys to `DefaultExperimentalFeatures`
- Flag: a new `DD_TRACE_EXPERIMENTAL_*`/experimental feature key that isn't added to `DefaultExperimentalFeatures` in `TracerSettings`, so `DD_TRACE_EXPERIMENTAL_FEATURES_ENABLED=all` doesn't enable it.

## 2. Logging and errors

### CONV-2.1: Log unexpected exceptions with `Log.Error`; expected environmental failures with `Log.Warning`/`ErrorSkipTelemetry`
- Flag: `Log.Warning`/`Log.Information` in a catch for a failure with no known cause, or `Log.Error` for expected environmental failures such as network connectivity issues or endpoint unavailability. See [AGENTS.md logging](../../../../AGENTS.md#logging-guidelines).
- Not when: an HTTP failure indicates a tracer or payload bug (for example, HTTP 400 Bad Request); preserve `Log.Error` and its telemetry.
- ❌ `catch (Exception ex) { Log.Warning(ex, "Failed to publish context"); }`
- ✅ `catch (Exception ex) { Log.Error(ex, "Failed to publish context"); }`

### CONV-2.2: Don't catch `DuckTypeException`; keep `DuckCast` outside the `try/catch`
- Flag: a `try/catch (Exception)` that wraps `DuckCast<T>()`/`TryDuckCast` and logs at `Debug`, swallowing duck-typing failures that should disable the integration. See [DuckTyping.md](../../DuckTyping.md).
- ❌ `try { var ctx = owin.DuckCast<IOwinContext>(); route = ctx.Route; } catch (Exception ex) { Log.Debug(ex, ...); }`
- ✅ `var ctx = owin.DuckCast<IOwinContext>(); try { route = ctx.Route; } catch (Exception ex) { Log.Error(ex, ...); }`

### CONV-2.3: Rate limit, or gate frequent logs behind `Debug` 
- Flag: A log message that could be written for every request/log, which could lead to log flooding

## 3. Shared helpers and idioms

### CONV-3.1: Signal shutdown with `TaskCompletionSource<bool>`, not `CancellationTokenSource`
- Flag: a `CancellationTokenSource` cancelled to stop a background loop on shutdown.
- ❌ `_cts.Cancel(); await Task.Delay(interval, _cts.Token);`
- ✅ `_processExit.TrySetResult(true); await Task.WhenAny(_processExit.Task, Task.Delay(interval));`

### CONV-3.2: Use `ThreadSafeRandom.Shared` instead of `new Random()`
- Flag: a `new Random()` field or local (e.g. for retry jitter).
- ❌ `private readonly Random _random = new();`
- ✅ `ThreadSafeRandom.Shared.Next(0, maxJitterMs)`

### CONV-3.3: Use `CharHelpers` instead of local character-classification helpers
- Flag: a private `IsAsciiLetterOrDigit`/`IsAsciiDigit`-style helper that duplicates `CharHelpers` (which polyfills `char.IsAscii*` on older TFMs).
- ❌ `private static bool IsAsciiLetterOrDigit(char c) => c is >= 'a' and <= 'z' or ...;`
- ✅ `char.IsAsciiLetterOrDigit(c)`

### CONV-3.4: Reuse `Separators` instead of new `static readonly char[]` separator arrays
- Flag: a new `static readonly char[] { '/' }` (or similar) passed to `Split`/`IndexOfAny`.
- ❌ `private static readonly char[] Slash = { '/' };`
- ✅ `Separators.ForwardSlash` (or `value.SplitIntoSpans('/')`)

### CONV-3.5: Don't add `#if NETFRAMEWORK` `using System.Buffers` aliases; `GlobalUsings.cs` provides them
- Flag: a manual `using`/alias for `System.Buffers` or `ArrayPool` behind `#if`, which also skips the vendored copy on `netstandard2.0`.
- ❌ `#if NETFRAMEWORK` / `using ArrayPool = Datadog.Trace.VendoredMicrosoftCode.System.Buffers...;`
- ✅ remove it; `ArrayPool<T>.Shared` resolves via `GlobalUsings.cs`.

### CONV-3.6: Mark members widened to `internal` for tests with `[TestingOnly]` or `[TestingAndPrivateOnly]`
- Flag: an `internal` or `public` member that is only used externally by tests. A comment such as `// for tests only` comment or no attribute (including sibling members of one that has it), or a test that reaches a private member by reflection instead of using a `[TestingOnly]` accessor.
- ❌ `// For tests only` / `internal long DroppedTraces => ...;`
- ✅ `[TestingOnly]` / `internal long DroppedTraces => ...;`

### CONV-3.7: Use collection expressions instead of `new[] { ... }`
- Flag: `new[] { ... }`/`new T[] { ... }` array literals where a collection expression compiles.
- ❌ `Encode(new[] { LocalRootSpanIdKey });`
- ✅ `Encode([LocalRootSpanIdKey]);`

### CONV-3.8: Pass `in` at the call site for `in` parameters
- Flag: a call to a method with an `in` parameter that omits `in` on the argument.
- Not when: the argument requires an implicit temporary (for example, a property, method result, constructor expression, or implicit conversion), so adding explicit `in` would not compile.
- ❌ `WarnFailure(result, attempts: 1);`
- ✅ `WarnFailure(in result, attempts: 1);`

## 4. Tests

### CONV-4.1: Assert integration-test spans with Verify snapshots, not per-tag assertions
- Flag: an integration test that checks individual span tags manually, or a unit test that serializes spans presented as a "snapshot" in place of an end-to-end `VerifyHelper` snapshot. See [AutomaticInstrumentation.md](../../AutomaticInstrumentation.md).
- ❌ `span.Tags["ffe.flag"].Should().Be("x");` across many spans
- ✅ `await VerifyHelper.VerifySpans(spans, settings);`

### CONV-4.2: Run all scenarios in one sample process with one snapshot, not one launch per mode
- Flag: a sample/test pair that relaunches the sample per operation or test mode instead of running everything once with stable span ordering.

### CONV-4.3: Add scrubbers for random values to the shared Verify scrubbers, not per test
- Flag: a per-test regex/scrubber for a value (e.g. random `server.port`) that other snapshot tests also emit.

### CONV-4.4: Use FluentAssertions instead of `Assert.*`
- Flag: `Assert.Equal`/`Assert.True` in new or modified tests.
- ❌ `Assert.Equal(expected, span.GetTag(Tags.ErrorType));`
- ✅ `span.GetTag(Tags.ErrorType).Should().Be(expected);`

### CONV-4.5: Collapse near-duplicate `[Fact]`s into one `[Theory]` with `[InlineData]`
- Flag: several tests that differ only in an input value and expected result.
- ❌ `[Fact] Timeout_10()`, `[Fact] Timeout_50()`, `[Fact] Timeout_Negative()`
- ✅ `[Theory] [InlineData("10", 10)] [InlineData("-1", Default)] void Timeout(string raw, int expected)`

### CONV-4.6: Use `[CombinatorialData]` for `bool`/enum theory parameters
- Flag: `[InlineData(true)]` + `[InlineData(false)]` (or every enum value) listed by hand. See [XunitCombinatorial.md](../../XunitCombinatorial.md).
- Not when: a custom `MemberData` generator deliberately expresses a non-combinatorial set of cases.
- ❌ `[Theory] [InlineData(false)] [InlineData(true)]`
- ✅ `[Theory] [CombinatorialData]`

### CONV-4.7: Add new library-version scenarios to the existing sample behind `#if`/`DefineConstants`, not a new sample project
- Flag: a new `Samples.<Lib><Version>` project duplicating an existing sample for one library version or feature.
- Not when: the new tests need process isolation from the existing sample's tests.
- ❌ new `Samples.NUnit5Tests.csproj`
- ✅ `<DefineConstants Condition="$([MSBuild]::VersionGreaterThanOrEquals($(ApiVersion), '5.0.0'))">$(DefineConstants);NUNIT_5_0</DefineConstants>` in the existing sample

### CONV-4.8: Don't pass `because` reason strings to FluentAssertions by default.
- Flag: a `Should().X(...)` call with a reason-string argument that restates the assertion, where the meaning behind the assertion is already clear.
- ❌ `context.Should().BeNull("the context is gone after the request");`
- ✅ `context.Should().BeNull();`

## 5. Build and process

### CONV-5.1: Build test-helper projects in `CompileManagedTestHelpers`
- Flag: a test fixture/helper project built from an unrelated Nuke target instead of `CompileManagedTestHelpers` (or named to match its `*TestHelpers` glob).

### CONV-5.2: Use the repo PR template headings in workflow-generated PR bodies
- Flag: a workflow/tool that opens PRs with a `body:` not using the `.github/pull_request_template.md` sections (Summary of changes, Reason for change, Implementation details, Test coverage, Other details).

## 6. Duck typing

### CONV-6.1: Use duck typing instead of reflection to read members of third-party types
- Flag: `GetType().GetProperty(...)`/`GetField(...)`/`GetMethod(...)` plus `GetValue`/`Invoke` on a library type where a duck-typed proxy would work. See [DuckTyping.md](../../DuckTyping.md).
- ❌ `var value = obj.GetType().GetProperty("Value")?.GetValue(obj);`
- ✅ `var value = obj.DuckCast<IValueProxy>().Value;`

### CONV-6.2: Choose the proxy kind per the DuckTyping.md best practices: constraint, then `[DuckCopy]` struct, then interface
- Flag: a `DuckCast<T>()` in an integration where the proxy could be a generic constraint (`where TTarget : IFoo, IDuckType`); an interface proxy used only to read fields/properties where a `[DuckCopy]` struct would do; or a `null` check on a constrained proxy (always a struct) instead of `IDuckType.Instance`. See [DuckTyping.md best practices](../../DuckTyping.md#best-practices).
- ❌ `OnMethodBegin<TTarget>(TTarget instance) { var ctx = instance.DuckCast<IContext>(); ... }`
- ✅ `OnMethodBegin<TTarget>(TTarget instance) where TTarget : IContext, IDuckType { ... }`

## Don't flag
- Honeypot `IntegrationGroups` map additions that accompany a new CallTarget integration on that assembly.
- A new test project's `ModuleInitializer` omitting `JTokenAssertions.Initialize()` when the project doesn't assert on `JToken`.
