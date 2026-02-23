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
- Pattern: see `HttpMessageHandlerTests.InstrumentationOptions` and `StringSizeExpectation` for examples of classes implementing `IXunitSerializable` directly via `AddValue`/`GetValue<T>`

## Worktree

All changes in: `D:/source/datadog/dd-trace-dotnet-worktrees/lpimentel/fix-xunit-serialization/`

## Changes by File

### 1. `ParseUtilityTests.cs` — IEnumerable<string> parameters
**Problem:** `TheoryData<IEnumerable<string>, T>` — xUnit can't serialize `IEnumerable<string>`.
**Fix:** Use `SerializableList<string>` (which implements `IXunitSerializable` and `IEnumerable<T>`) instead of `IEnumerable<string>` in the `TheoryData` type and test method signatures. Existing `List<string>` and `string[]` entries can be wrapped in `SerializableList<string>`. The SUT still receives an `IEnumerable<string>`, and the test still exercises both list and array data.

### 2. `W3CBaggagePropagatorTests.cs` — ValueTuple arrays
**Problem:** `TheoryData<string, (string Key, string Value)[]>` — ValueTuple arrays aren't serializable.
**Fix:** Create a small `SerializableKeyValuePair` class implementing `IXunitSerializable` (fields: `Key`, `Value`), and use `SerializableList<SerializableKeyValuePair>` for the array parameter. Note: `SerializableList<T>` uses JSON serialization, so `T` must be JSON-serializable (a simple two-string class is fine).

### 3. `W3CTraceContextPropagatorTests.cs` — IEnumerable<string> in object[]
**Problem:** `TryGetSingleTestCases()` and `TryExtractTestCases()` return `object[]` containing `HashSet<string>`, `List<string>`, `Queue<string>`, LINQ expressions, and yield-return iterators — deliberately testing different `IEnumerable<string>` implementations.
**Fix:** The tests intentionally exercise different collection types, so we must preserve that distinction. Use a `string collectionType` discriminator parameter ("HashSet", "List", "Array", "Queue", "Enumerable", "Yield") + `SerializableList<string>` for the values. Construct the appropriate collection in the test body based on `collectionType`. All parameters become serializable.

### 4. `RequestHeadersHelpersTests.cs` — Custom TestScenario object
**Problem:** `TestScenario` doesn't implement `IXunitSerializable`.
**Fix:** Make `TestScenario` implement `IXunitSerializable`. All fields are already primitives or `SerializableDictionary`. Add a parameterless constructor (required by xUnit), implement `Serialize()` and `Deserialize()` using `AddValue`/`GetValue<T>`.

### 5. `ExposureCacheTests.cs` — Nested object[] with ExposureEvent and bool[]
**Problem:** `Cases()` returns `IEnumerable<object?[]>` containing `ExposureEvent` objects and `bool[]`.
**Fix:** Create an `ExposureCacheTestCase` class implementing `IXunitSerializable`. Store `ExposureEvent` data as primitive fields (flag, subject, variant, allocation strings), expected bool results as a `SerializableList<bool>`, and the int capacity/size fields. Construct `ExposureEvent` objects inside the test from these primitives.

### 6. `FeatureFlagsEvaluatorTests.cs` — Mixed object?[] with Type objects and Dictionary
**Problem:** `MapValueCases()` uses `DateTime` and `Type` refs. `FlattenContextCases()` uses `Dictionary<string, object?>`.

**Fix for MapValueCases:** Create a `MapValueTestCase` class implementing `IXunitSerializable`. Encode `Type` as its assembly-qualified name string; encode `DateTime` as ISO 8601 string. Parse in `Deserialize()`.

**Fix for FlattenContextCases:** Only 4 cases — convert to 4 separate `[Fact]` methods (cleaner than a complex serializable wrapper for `Dictionary<string, object?>`).

### 7–11. Schema tests (Client, Database, Messaging, Naming, Server)
**Problem:** `[CombinatorialMemberData]` returns `IEnumerable<ValueTuple>` — ValueTuples aren't serializable by xUnit.
**Fix:** The tuples contain only primitives (`int`, `string`). Create a serializable wrapper class (e.g., `SchemaTestCase`) implementing `IXunitSerializable`, or simply expand the combinatorial product (tuple × bool combinations) into explicit `TheoryData` rows in the member data method. Since the tuples are combined with `bool` values via `[CombinatorialData]`, we replace `[CombinatorialData]` + `[CombinatorialMemberData]` with plain `[Theory]` + `[MemberData]` where the member data method emits all combinations explicitly.

## Summary

| Category | Files | Strategy |
|----------|-------|----------|
| `IEnumerable<string>` | `ParseUtilityTests` | Use `SerializableList<string>` |
| ValueTuple arrays | `W3CBaggagePropagatorTests` | `SerializableList<SerializableKeyValuePair>` (new small class) |
| `IEnumerable<string>` + collection type | `W3CTraceContextPropagatorTests` | `string collectionType` + `SerializableList<string>` |
| Custom object | `RequestHeadersHelpersTests` | Implement `IXunitSerializable` on `TestScenario` |
| Nested domain objects | `ExposureCacheTests` | New `ExposureCacheTestCase : IXunitSerializable` |
| `Type`/`DateTime` params | `FeatureFlagsEvaluatorTests` (MapValue) | New `MapValueTestCase : IXunitSerializable` |
| Complex dictionary data | `FeatureFlagsEvaluatorTests` (FlattenContext) | Split into 4 `[Fact]` methods |
| `CombinatorialMemberData` tuples | 5 Schema tests | Expand combinations explicitly in `[MemberData]` |

## Implementation Order (easiest to hardest)

1. **`TestScenario` (RequestHeadersHelpersTests)** — Add `IXunitSerializable` to existing class; all fields already serializable.
2. **`FlattenContextCases` (FeatureFlagsEvaluatorTests)** — Only 4 cases, split to `[Fact]` methods.
3. **`ParseUtilityTests`** — Swap `IEnumerable<string>` for `SerializableList<string>`; minimal changes.
4. **Schema tests (5 files)** — Mechanical: expand combinatorial products explicitly. Tedious but straightforward.
5. **`MapValueCases` (FeatureFlagsEvaluatorTests)** — New `MapValueTestCase` class with string-encoded `Type`/`DateTime`.
6. **`W3CBaggagePropagatorTests`** — New `SerializableKeyValuePair` + `SerializableList<SerializableKeyValuePair>`.
7. **`W3CTraceContextPropagatorTests`** — Collection-type discriminator + `SerializableList<string>`.
8. **`ExposureCacheTests`** — New `ExposureCacheTestCase : IXunitSerializable` with nested domain data as primitives.

## Verification

```bash
# Build the test project
dotnet build tracer/test/Datadog.Trace.Tests/ -c Release

# Run affected tests, verify no serialization warnings
dotnet test tracer/test/Datadog.Trace.Tests/ -c Release --filter "FullyQualifiedName~ParseUtilityTests|FullyQualifiedName~W3CBaggagePropagatorTests|FullyQualifiedName~W3CTraceContextPropagatorTests|FullyQualifiedName~RequestHeadersHelpersTests|FullyQualifiedName~ExposureCacheTests|FullyQualifiedName~FeatureFlagsEvaluatorTests|FullyQualifiedName~ClientSchemaTests|FullyQualifiedName~DatabaseSchemaTests|FullyQualifiedName~MessagingSchemaTests|FullyQualifiedName~NamingSchemaTests|FullyQualifiedName~ServerSchemaTests"
```

Verify:
1. All tests pass
2. No "Non-serializable data" warnings in output
3. Test count is the same or higher (individual cases now visible)
