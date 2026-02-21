# Fix xUnit Non-Serializable Data Warnings

## Context

xUnit v3 requires all `[Theory]` test data to be serializable so it can uniquely identify each test case. Several tests in `Datadog.Trace.Tests` produce warnings like:
```
warning [xUnit.net] Datadog.Trace.Tests: Non-serializable data ('System.Object[]') found for <test>
```
This happens when `[MemberData]` returns types xUnit can't serialize (collections as `IEnumerable<string>`, ValueTuple arrays, custom objects, nested `object[]`, `Dictionary<string, object?>`).

## Worktree

All changes in: `D:/source/datadog/dd-trace-dotnet-worktrees/lpimentel/fix-xunit-serialization/`

## Changes by File

### 1. `ParseUtilityTests.cs` — IEnumerable<string> parameters
**Problem:** `TheoryData<IEnumerable<string>, T>` — xUnit can't serialize `IEnumerable<string>`.
**Fix:** Change from `IEnumerable<string>` to `string[]` in the TheoryData type and test method signatures. For `List<string>` entries, convert to `string[]`. Since the tests also intentionally test `List<string>` behavior, and the actual SUT (`ParseUtility`) accepts `IEnumerable<string>`, using `string[]` (which implements `IEnumerable<string>`) is fine — the test still exercises the same code paths. The `List<string>` test cases can stay as arrays since the important distinction is the content, not the collection type.

Wait — actually the test deliberately uses both `string[]` and `List<string>` to test both code paths. We need to preserve that. The simplest fix: split the `string[]` cases into `[InlineData]` where possible, and for the cases that need `List<string>`, accept that some won't be serializable, or restructure to pass a string discriminator and build the collection inside the test.

**Revised fix:** Add a `string collectionType` parameter ("array", "list", or "null") and the string values as a `string[]`, then construct the appropriate collection inside the test method. Use `TheoryData<string, string[], T>` where the first param is the collection type.

Actually, looking again — the TheoryData already uses `TheoryData<IEnumerable<string>, ulong?>`. The simplest and least-invasive fix: change to `IEnumerable<object[]>` returning primitive-only data, with an int index or string tag, and look up the actual collection in the test. But that's over-engineered.

**Simplest practical fix:** Convert to `TheoryData<string, string[], ulong?>` where:
- First param = collection type: `"array"`, `"list"`, or `"null"`
- Second param = `string[]` values (or null)
- Third param = expected result

Then in the test method, construct the appropriate `IEnumerable<string>` based on the type tag. This makes all data serializable (strings and arrays of strings are serializable).

### 2. `W3CBaggagePropagatorTests.cs` — ValueTuple arrays
**Problem:** `TheoryData<string, (string Key, string Value)[]>` — ValueTuple arrays aren't serializable.
**Fix:** Split the key-value pairs into two parallel `string[]` parameters (keys and values). Change to `TheoryData<string, string[], string[]>`. In the test, zip them back together. Null cases use null arrays.

### 3. `W3CTraceContextPropagatorTests.cs` — IEnumerable<string> in object[]
**Problem:** `TryGetSingleTestCases()` returns `object[]` with `HashSet<string>`, `List<string>`, `Queue<string>`, LINQ expressions, and yield-return iterators. `TryExtractTestCases()` returns similar.
**Fix for TryGetSingleTestCases:** Change to pass a `string collectionType` parameter and construct the collection in the test. Types: "HashSet", "List", "Array", "Queue", "Enumerable", "Yield".
**Fix for TryExtractTestCases:** Similar — pass collection type string + the actual string values as a `string[]`, plus the `bool expectedSuccess`. Construct the appropriate collection in the test.

### 4. `RequestHeadersHelpersTests.cs` — Custom TestScenario object
**Problem:** `TestScenario` doesn't implement `IXunitSerializable`.
**Fix:** Make `TestScenario` implement `IXunitSerializable`. It already uses `SerializableDictionary` (which implements `IXunitSerializable`), and all other fields are primitives. Add a parameterless constructor (required by `IXunitSerializable`), implement `Serialize()` and `Deserialize()`.

### 5. `ExposureCacheTests.cs` — Nested object[] with ExposureEvent and bool[]
**Problem:** `Cases()` returns `IEnumerable<object?[]>` with `object[]` of `ExposureEvent` and `bool[]`.
**Fix:** Convert test data to use serializable types. Pass the exposure event data as parallel string arrays (flags, subjects, variants, allocations) and the expected bools as a `bool[]` won't work either since `bool[]` isn't serializable.

Better approach: Convert to individual test methods with `[InlineData]` where practical, or pass primitive arrays. Since the test creates `ExposureEvent` objects from 4 strings each, pass `string[]` for flags, subjects, variants, allocations + `bool[]` for expected results + int capacity + int expectedSize. But arrays aren't serializable either in xUnit...

**Revised approach:** Use `TheoryData<>` with a wrapper class implementing `IXunitSerializable`, or restructure to use `[InlineData]` with comma-separated strings that get parsed in the test. For example, pass `"flag:subject:variant:allocation,flag:subject:variant:allocation"` as a single string, and `"true,false"` as expected results string. Parse in test.

Actually, the simplest: create a small `ExposureCacheTestCase` class implementing `IXunitSerializable` that holds all the test data.

### 6. `FeatureFlagsEvaluatorTests.cs` — Mixed object?[] with Type objects and Dictionary
**Problem:** `MapValueCases()` returns `object?[]` with `DateTime`, `Type` refs. `FlattenContextCases()` returns `Dictionary<string, object?>`.

**Fix for MapValueCases:** The data is essentially `(object? input, object? expected, Type? expectedException)`. Convert to pass primitive string representations: `string inputType`, `string inputValue`, `string expectedType`, `string expectedValue`, `string exceptionTypeName`. Parse in test.

**Fix for FlattenContextCases:** Only 4 cases — convert to separate `[Fact]` methods or use an int case index with a switch in the test method. Given the complexity of the dictionary data, an index-based approach is simplest.

### 7–11. Schema tests (Client, Database, Messaging, Naming, Server)
**Problem:** `[CombinatorialMemberData]` returns `IEnumerable<ValueTuple>` — ValueTuples aren't serializable.
**Fix:** Change the member data methods to return `TheoryData<int, int, string>` (or appropriate primitive types) instead of `IEnumerable<ValueTuple>`. The `[CombinatorialMemberData]` attribute should work with individual parameters rather than a single tuple parameter. Restructure the test methods to accept individual parameters instead of a single tuple.

Actually — looking at how `[CombinatorialMemberData]` works with `[CombinatorialData]`: the member data provides values for ONE parameter, and `[CombinatorialData]` generates combinations with other `[CombinatorialValues]` parameters. The tuple is a single parameter that gets combined with booleans.

The fix is to split the tuple parameter into individual parameters and provide each via separate `[CombinatorialMemberData]` or `[CombinatorialValues]`. But that would change the combinatorial behavior — currently specific tuples are combined with all bool values, not all individual field values combined with each other.

**Better fix:** Make the tuples serializable by converting to use `TheoryData` with individual params instead of `[CombinatorialData]`. Generate all combinations explicitly in the member data. This changes from `[CombinatorialData]` + `[CombinatorialMemberData]` to plain `[Theory]` + `[MemberData]`.

## Summary of Approach by Category

| Category | Files | Strategy |
|----------|-------|----------|
| IEnumerable<string> | ParseUtilityTests, W3CTraceContextPropagatorTests | Pass collection type string + string[] values, construct collection in test |
| ValueTuple arrays | W3CBaggagePropagatorTests | Split into parallel string[] for keys and values |
| Custom object | RequestHeadersHelpersTests | Make TestScenario implement IXunitSerializable |
| Complex nested objects | ExposureCacheTests | Create IXunitSerializable test case wrapper |
| Mixed object?[] | FeatureFlagsEvaluatorTests (MapValue) | Convert to string-encoded primitives |
| Dictionary test data | FeatureFlagsEvaluatorTests (FlattenContext) | Convert to index-based lookup (only 4 cases) |
| CombinatorialMemberData tuples | 5 Schema tests | Replace [CombinatorialData]+[CombinatorialMemberData] with [Theory]+[MemberData], expanding combinations explicitly |

## Implementation Order (easiest to hardest)

### Low-hanging fruit

1. **`TestScenario` (RequestHeadersHelpersTests)** — Just add `IXunitSerializable` to an existing class. All fields are already primitives or `SerializableDictionary` (which already implements `IXunitSerializable`). Add parameterless constructor + `Serialize()`/`Deserialize()`.

2. **`FlattenContextCases` (FeatureFlagsEvaluatorTests)** — Only 4 cases with complex dictionaries. Convert to 4 `[Fact]` methods or an int index with a switch.

3. **Schema tests (5 files)** — Mechanical transformation. The tuples only contain primitives (`int`, `string`, `bool`). Expand the combinatorial product into explicit `[MemberData]` rows. Tedious but straightforward.

4. **`MapValueCases` (FeatureFlagsEvaluatorTests)** — Mostly primitives, but includes `Type` and `DateTime`. Needs string encoding, but the pattern is repetitive and simple.

### Moderate effort

5. **`W3CBaggagePropagatorTests`** — Splitting tuple arrays into parallel string arrays. Doable but needs care with null handling.

6. **`ParseUtilityTests`** — Needs the collection-type-string pattern since the test deliberately exercises both `string[]` and `List<string>`.

### Harder

7. **`W3CTraceContextPropagatorTests`** — Uses `HashSet`, `Queue`, LINQ, yield-return iterators deliberately to test different `IEnumerable<string>` implementations. Needs collection-type dispatch.

8. **`ExposureCacheTests`** — Needs a custom `IXunitSerializable` wrapper for `ExposureEvent` data, which has several nested domain objects.

## Verification

```bash
cd D:/source/datadog/dd-trace-dotnet-worktrees/lpimentel/fix-xunit-serialization

# Build the test project
dotnet build tracer/test/Datadog.Trace.Tests/ -c Release

# Run the specific affected tests and verify no serialization warnings
dotnet test tracer/test/Datadog.Trace.Tests/ -c Release --filter "FullyQualifiedName~ParseUtilityTests|FullyQualifiedName~W3CBaggagePropagatorTests|FullyQualifiedName~W3CTraceContextPropagatorTests|FullyQualifiedName~RequestHeadersHelpersTests|FullyQualifiedName~ExposureCacheTests|FullyQualifiedName~FeatureFlagsEvaluatorTests|FullyQualifiedName~ClientSchemaTests|FullyQualifiedName~DatabaseSchemaTests|FullyQualifiedName~MessagingSchemaTests|FullyQualifiedName~NamingSchemaTests|FullyQualifiedName~ServerSchemaTests"
```

Verify:
1. All tests pass
2. No "Non-serializable data" warnings in output
3. Test count is the same or higher (individual cases now visible)
