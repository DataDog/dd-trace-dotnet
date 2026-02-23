# Fix xUnit Non-Serializable Data Warnings

## Context

xUnit v3 requires serializable `[Theory]` test data to uniquely identify each test case. Several tests in `Datadog.Trace.Tests` produce warnings like:
```
warning [xUnit.net] Datadog.Trace.Tests: Non-serializable data ('System.Object[]') found for <test>
```
Per `docs/development/XunitCombinatorial.md`, this is **a visual bug only** — tests still run. The impact is that individual test cases aren't visible as separate entries (especially relevant in pairwise mode). The preferred fix is to implement `IXunitSerializable` on custom types.

## Existing Infrastructure

- `SerializableList<T>` — `tracer/test/Datadog.Trace.TestHelpers/SerializableList.cs` — JSON-serializes a `List<T>`
- `SerializableDictionary` — `tracer/test/Datadog.Trace.TestHelpers/SerializableDictionary.cs` — JSON-serializes a `Dictionary<string, string>`
- `BaggageEntry` — `tracer/test/Datadog.Trace.TestHelpers/BaggageEntry.cs` — JSON-serializable key/value pair for use with `SerializableList<BaggageEntry>` (**new**)
- Pattern: see `HttpMessageHandlerTests.InstrumentationOptions` and `StringSizeExpectation` for examples of classes implementing `IXunitSerializable` directly via `AddValue`/`GetValue<T>`

## Worktree

All changes in: `D:/source/datadog/dd-trace-dotnet-worktrees/lpimentel/fix-xunit-serialization/`

## Changes by File

### ✅ 1. `RequestHeadersHelpersTests.cs` / `TestScenario.cs` — Custom TestScenario object
**Fix:** Converted `TestScenario` from primary constructor to implement `IXunitSerializable`. Added parameterless constructor, `Serialize()` and `Deserialize()`. All fields are primitives or `SerializableDictionary`.

### ✅ 2. `ParseUtilityTests.cs` — IEnumerable<string> parameters
**Fix:** Replaced `TheoryData<IEnumerable<string>, T>` with `TheoryData<SerializableList<string>, T>`. Updated all three `TheoryData` properties and four test method signatures.

### ✅ 3. `FeatureFlagsEvaluatorTests.cs` (FlattenContext) — Dictionary<string, object?> data
**Fix:** Replaced single `[Theory]` + `FlattenContextCases` member data with 4 named `[Fact]` methods sharing a private `AssertFlattenContext` helper.

### ✅ 4. `W3CBaggagePropagatorTests.cs` — ValueTuple arrays
**Fix:** Added `BaggageEntry` to TestHelpers (JSON-serializable). Changed `TheoryData` to use `SerializableList<BaggageEntry>`. Updated `CreateHeader` and `ParseHeader` method signatures.

### ✅ 5. Schema tests (Client, Database, Messaging, Naming, Server)
**Fix:** Replaced `IEnumerable<ValueTuple>` return types with local wrapper classes implementing `IXunitSerializable` (one per distinct tuple shape per file). Added `#pragma warning disable SA1201` around each class. Data methods now return the wrapper type.

### ✅ 6. `FeatureFlagsEvaluatorTests.cs` (MapValue) — Type/DateTime in object?[]
**Fix:** Added `MapValueTestCase : IXunitSerializable` as a nested class. Encodes `Type` as assembly-qualified name, `DateTime` as ISO string, primitives via `ToString()`/`Parse()`. Changed `MapValueCases` to return `TheoryData<MapValueTestCase>`. Updated test method to accept `MapValueTestCase`.

### ✅ 7. `W3CTraceContextPropagatorTests.cs` — IEnumerable<string> + collection type
**Fix:** Changed `TryGetSingleTestCases` and `TryExtractTestCases` to `TheoryData` with a `string collectionType` discriminator + `SerializableList<string>`. Added private `BuildCollection` helper to reconstruct the appropriate collection type (HashSet, List, Array, Queue, Enumerable, Yield, or null). Updated test method signatures.

### ✅ 8. `ExposureCacheTests.cs` — Nested ExposureEvent objects and bool[]
**Fix:** Added `ExposureEventData` (JSON-serializable) and `ExposureCacheTestCase : IXunitSerializable`. Changed `Cases()` to return `TheoryData<ExposureCacheTestCase>`. Updated test method to accept `ExposureCacheTestCase` and reconstruct `ExposureEvent` objects in the test body.

## Summary

| # | File(s) | Status | Strategy |
|---|---------|--------|----------|
| 1 | `RequestHeadersHelpersTests` / `TestScenario` | ✅ Done | `IXunitSerializable` on `TestScenario` |
| 2 | `ParseUtilityTests` | ✅ Done | `SerializableList<string>` |
| 3 | `FeatureFlagsEvaluatorTests` (FlattenContext) | ✅ Done | Split into 4 `[Fact]` methods |
| 4 | `W3CBaggagePropagatorTests` | ✅ Done | `BaggageEntry` + `SerializableList<BaggageEntry>` |
| 5 | 5 Schema tests | ✅ Done | Local `IXunitSerializable` wrapper classes per file |
| 6 | `FeatureFlagsEvaluatorTests` (MapValue) | ✅ Done | `MapValueTestCase : IXunitSerializable` |
| 7 | `W3CTraceContextPropagatorTests` | ✅ Done | `string collectionType` + `SerializableList<string>` |
| 8 | `ExposureCacheTests` | ✅ Done | `ExposureCacheTestCase : IXunitSerializable` |

## Verification

```bash
# Build the test project
dotnet build tracer/test/Datadog.Trace.Tests/ -c Release

# Run affected tests, verify no serialization warnings
dotnet test tracer/test/Datadog.Trace.Tests/ -c Release --filter "FullyQualifiedName~ParseUtilityTests|FullyQualifiedName~W3CBaggagePropagatorTests|FullyQualifiedName~W3CTraceContextPropagatorTests|FullyQualifiedName~RequestHeadersHelpersTests|FullyQualifiedName~ExposureCacheTests|FullyQualifiedName~FeatureFlagsEvaluatorTests|FullyQualifiedName~ClientSchemaTests|FullyQualifiedName~DatabaseSchemaTests|FullyQualifiedName~MessagingSchemaTests|FullyQualifiedName~NamingSchemaTests|FullyQualifiedName~ServerSchemaTests"
```

Result: **926 tests passed, 0 failed, 0 warnings** (net6.0)
