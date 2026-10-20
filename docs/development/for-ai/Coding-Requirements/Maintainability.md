# Maintainability

Duplication, dead code, comments, naming, and test value.

**When planning:** take the rules below into account when designing the change.

**When reviewing:** report a finding when the diff matches a rule's `Flag:` line and no `Not when:` applies. Cite the rule ID and point to the existing helper or simpler shape.

## 1. Duplication and reuse

### MAINT-1.1: Call the existing helper (`VarEncodingHelper`, `HexString`, `Utf8Formatter`, `ExporterSettings.IsOtlpTraceExport`, `Path.GetRandomFileName`) instead of reimplementing it
- Flag: a new method or loop that encodes varints, parses/formats hex, builds random names, or re-derives an existing settings check by hand.
- Not when: no suitable type is already referenced by the project (e.g. a test-only protobuf decoder where the generated types aren't available).
- ❌ `var name = prefix + Guid.NewGuid().ToString("N").Substring(0, 12);`
- ✅ `var name = prefix + Path.GetRandomFileName();`

### MAINT-1.2: Consolidate to one copy when the diff leaves the same constant, mapping, helper or lookup in two places
- Flag: after the change, the same constant, field-name map, serialization rule, helper body (new `TryPeekScope` beside the old one, `WaitForSend` beside `WaitForCount`) or multi-line lookup appears in two or more places.
- Not when: the copies are separate test base classes and deriving one from the other would couple unrelated fixture setup.
- ❌ `const string FileName = "supported-configurations.yaml"; // also declared in the generator`
- ✅ `Constants.SupportedConfigurationsFileName // shared file compiled into both projects`

### MAINT-1.3: Put the fix in the shared helper or apply it to every sibling site
- Flag: an extra setting, retry or call added next to one use of a helper/command (or in one of several near-identical files) when every other use needs it too.
- Not when: the other sites don't use the setting at all.
- ❌ `.SetTargetPlatformAnyCPU().SetProperty("PlatformTarget", "AnyCPU")`
- ✅ `.SetTargetPlatformAnyCPU() // helper now also sets PlatformTarget`

## 2. Dead and redundant code

### MAINT-2.1: Delete unused constants, empty `Dispose` implementations and `Debug.WriteLine` calls when touching the file
- Flag: a constant/field nothing reads, a type implementing `IDisposable` with an empty `Dispose()`, or `Debug.WriteLine` in a catch block (compiled out of release builds).
- Note: consider committing the cleanup as a stacked PR.
- Not when: the file would not otherwise be touched by the current work stream.
- ❌ `public void Dispose() { }`
- ✅ `// remove IDisposable and the Dispose() call sites`

### MAINT-2.2: Remove checks, casts and settings that are already guaranteed
- Flag: a null check or cast on a value whose static type already guarantees it, reflection to discover a type already available as `typeof(T)`, `|| flag` where `TracerSettings` already folds the flag in, or an env var set in a Nuke step that a `ModuleInitializer` already sets.
- ❌ `var enabled = settings.RouteTemplateResourceNamesEnabled || otelSemanticsEnabled;`
- ✅ `var enabled = settings.RouteTemplateResourceNamesEnabled;`

### MAINT-2.3: Replace `!` with nullability attributes (`[NotNullWhen]`, `[MemberNotNullWhen]`) or remove it
- Flag: a new null-forgiving `x!`, or a `Try*`/`IsValid` member without `[NotNullWhen(true)]`/`[MemberNotNullWhen(true, ...)]` that forces callers to write `!`.
- ❌ `public bool IsValid => Owner is not null && Context is not null;`
- ✅ `[MemberNotNullWhen(true, nameof(Owner), nameof(Context))] public bool IsValid => ...;`

### MAINT-2.4: Use the standard collection that matches the usage (`HashSet<T>`, `Stack<T>`)
- Flag: a dictionary/map whose values are never read, or a hand-rolled free list/linked structure (often `unsafe`) where a BCL collection would do.
- ❌ `std::unordered_map<ModuleID, bool> processedModules;`
- ✅ `std::unordered_set<ModuleID> processedModules;`

### MAINT-2.5: Inline trivial methods extracted only so they can be unit-tested
- Flag: a new one-line private/`[TestingAndPrivateOnly]` method (e.g. `return current + 1;`) plus a test class that exists only to call it.
- ❌ `internal static int NextCount(int current) => current + 1;`
- ✅ `count++;`

## 3. Comments and naming

### MAINT-3.1: Delete comments that restate the code, narrate the change, or repeat across sites
- Flag: a multi-line comment block above straightforward code (including CI/config YAML), "Moved above…"/"Mirrors X" comments, or a `/// <summary>` on a test method.
- ❌ `// Mirrors ExtractProperties: stop once max depth is reached`
- ✅ `if (depth > MaxDepth) { return; }`

### MAINT-3.2: Fix or remove comments that are false, stale or describe the current caller
- Flag: a comment claiming something untrue ("ArrayPool is unavailable on this TFM"), a setting requirement that's now forced, a caller detail stated as a type property ("runs on the serializer thread"), or hardcoded counts in a test comment.
- ❌ `/// Called on the serializer thread only.`
- ✅ `// (delete; nothing in the type requires it)`

### MAINT-3.3: Comment lifetimes and behaviour that aren't visible at the declaration
- Flag: a rented/pooled resource or `std::move` capture released elsewhere, an attribute property that still applies in an unexpected mode, or an unusual MSBuild filter, with no comment saying where/why.
- ❌ `var block = RecordPool.Instance.Rent();`
- ✅ `// Returned to the pool when the ThreadRecord is destroyed` above `var block = RecordPool.Instance.Rent();`

### MAINT-3.4: Rename methods and types so the name states the behaviour
- Flag: a call-site comment explaining what the callee really does ("only set when it differs, ignoring case"), or a vague/ambiguous name (`EvaluateHealth`, `V3V4` meaning "v3 package, version 4").
- Not when: the only ask is a `Try` prefix and the nullable return type already signals the outcome.
- ❌ `public static class XUnitTestAssemblyRunnerRunV3V4Integration`
- ✅ `public static class XUnitTestAssemblyRunnerRunV3_4XIntegration`

## 4. Tests

### MAINT-4.1: Give each new utility type its own `<Type>Tests` class with input/output cases
- Flag: a new encoder/hasher/parser with no dedicated test class, or its tests placed in another feature's test file behind comment banners.
- ❌ `// ---- ULeb128 codec ----` inside `SpanEnrichmentTests`
- ✅ `public class ULeb128EncoderTests { [Theory] [InlineData(...)] public void Encode(...) }`

### MAINT-4.2: Delete tests that add no new coverage
- Flag: a new test or theory row that re-asserts behaviour already covered (often by slow integration/snapshot tests), varies only an input the code ignores, stress-tests a limit another test already proves, or reflects over a type to assert an unchanged API still exists.
- Not when: the extra package version or case exercises a distinct instrumentation code path.
- ❌ `typeof(IConfigurationSource).GetMethod("GetString", ...).Should().NotBeNull();`

### MAINT-4.3: Test the real implementation, not a fake that reimplements it
- Flag: a test double that duplicates production logic so tests only verify the fake.
- ❌ `class FakeSlotProvider : ISlotProvider { /* copy of the native provider's logic */ }`
- ✅ `// contract test that reads the real record/payload and decodes it independently`

### MAINT-4.4: Don't multiply integration-test time with fixed delays or repetitions
- Flag: a fixed `Task.Delay`/quiet period in every test case of a serial suite, or a full sample run repeated N times per package version.
- ❌ `for (var i = 0; i < 20; i++) { await RunSampleAndAssert(packageVersion); }`
- ✅ `// run once per version; poll for the expected span count instead of a fixed wait`

### MAINT-4.5: Float package-version tests across every supported major
- Flag: a `PackageVersionsGeneratorDefinitions.json` entry or test data that pins exact old versions or one TFM, drops previously tested majors, or has an incomplete `VersionCondition` TFM exclusion list; a `supported_versions.json` cap below the current majors; or a pipeline step that hard-codes specific version folders.
- ❌ `"Versions": [ "3.1.2", "6.0.0" ]`
- ✅ `"Versions": [ "3.*.*", "6.*.*", "8.*.*", "10.*.*" ]`

## 5. Build files

### MAINT-5.1: Rely on standard `ProjectReference`/transitive package flow instead of extra references or copy targets
- Flag: a test csproj adding a direct `PackageReference` already provided by a referenced project (e.g. TestHelpers), or a custom `<Target>` with `<Copy>` to move a referenced project's output.
- ❌ `<PackageReference Include="Google.Protobuf" Version="..." /> <!-- already via TestHelpers -->`
- ✅ `<ProjectReference Include="..\Fixture\Fixture.csproj" OutputItemType="Content" CopyToOutputDirectory="PreserveNewest" />`
