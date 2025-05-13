# Xunit Combinatorial

[Xunit.Combinatorial](https://github.com/AArnott/Xunit.Combinatorial) has allowed us to reduce some integrations tests from taking 10+ minutes each CI run to being around 1 minute while maintaining a good coverage of tested configurations. Migrating a test suite to leverage this is a relatively quick process and you will immediately be able to take advantage of the performance improvements.

Instead of running our exhaustive combinatorial test suite on every PR we can *dynamically* determine when we should run the full test configuration or a much reduced [Pairwise](https://en.wikipedia.org/wiki/All-pairs_testing) test configuration.

For an overview and comparison to normal Xunit I'd recommend Andrew Lock's [Simplifying Theory test data with Xunit.Combinatorial](https://andrewlock.net/simplifying-theory-test-data-with-xunit-combinatorial/).

## CombinatorialOrPairwiseData Attribute

Powering this is a custom `[CombinatorialOrPairwiseData]` attribute that replaces our commonly used `[MemberData(nameof(GetEnabledConfig))]` attribute. It acts as a combination of the `[CombinatorialData]` and `[PairwiseData]` attributes that are part of the Xunit.Combinatorial package

## Choosing Between Combinatorial or Pairwise Test Configurations

Choosing the correct configurations is handled automatically, so after migrating to combinatorial tests there are no further steps. Our Nuke scripts will detect when we need to run the full / reduced configuration and will insert a `USE_FULL_TEST_CONFIG` environment variable automatically.

Full combinatorial test suites are run when:
- Running locally
- Default branch or release branch triggered the CI run (e.g., `master`)
- Any *new* instrumentations have been added
- Any *current* instrumentations have been changed
- 100+ snapshots have changed

Reduced pairwise test suites are used when all of the above aren't true.
> At a later point we can consider fine-tuning this further to more selectively choose what configuration to use on PRs.

## Migrating to Combinatorial Tests

There are *typically* just three main steps to migrating a test to use `Xunit.Combinatorial`:
1. Swap the test's `[MemberData(nameof(GetEnabledConfig))]` to `[CombinatorialOrPairwiseData]`
2. In the test's function definition change/add (if necessary) `[PackageVersionData(nameof(PackageVersions.PropertyNameHere))] string packageVersion`
3. In the test's function definition change/add (if necessary) `[MetadataSchemaVersionData] string metadataSchemaVersion`

And that is it.

For a visual representation of the *simplest case*:

```csharp
public static IEnumerable<object[]> GetEnabledConfig()
    => from packageVersionArray in PackageVersions.Foo
       from metadataSchemaVersion in new[] { "v0", "v1" }
       select new[] { packageVersionArray[0], metadataSchemaVersion };

[SkippableTheory]
[MemberData(nameof(GetEnabledConfig))]
[Trait("Category", "EndToEnd")]
public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
{
    // test code here
}
```

Becomes:

```csharp
[SkippableTheory]
[CombinatorialOrPairwiseData]
[Trait("Category", "EndToEnd")]
public async Task SubmitsTraces(
     [PackageVersionData(nameof(PackageVersions.MySqlConnector))] string packageVersion,
     [MetadataSchemaVersionData] string metadataSchemaVersion)
{
    // test code here
}

```

For more examples refer to the `ADONET` tests and the `HttpMessageHandlerTests`

When you change a test to use `[CombinatorialOrPairwiseData]` you get many additional benefits (note that you don't need to do anything to get pairwise configurations working, this is handled behind the scenes):

- `bool` parameter options are automatically expanded
- `enum` parameter options are automatically expanded
- `CombinatorialMemberData` can be used in the *same* way as `MemberData`
- `CombinatorialValues` can be used to provide an array of values to use for the test (e.g. a subset of `enum`, specific `string`, etc)

### PackageVersionData Attribute

With this we've introduced a `[PackageVersionData]` attribute, usage is like so:

```csharp
[SkippableTheory]
[CombinatorialOrPairwiseData]
 public async Task Foo(
     [PackageVersionData(nameof(PackageVersions.NameHere))] string packageVersion)
```

Additionally, there are overloads for supplying a `minInclusive` and/or `maxInclusive` NuGet versions to allow for easier handling of NuGet-specific functionality. Note that it supports a `glob` via a `*` for both:

- `[PackageVersionData(nameof(PackageVersions.NameHere), minInclusive:"2.0.0")]`
- `[PackageVersionData(nameof(PackageVersions.NameHere), minInclusive:"2.0.0", maxInclusive:"3.0.0")]`
- `[PackageVersionData(nameof(PackageVersions.NameHere), minInclusive:"2.0.0", maxInclusive:"3.*.*")]`
	- `maxInclusive` will be treated simply as `3.9999.9999` as the `*` are just simply swapped to `9999`
## Best Practices

### Use Pre-defined custom attributes

Make use of the `[PackageVersionData]` and `[MetadataSchemaVersionData]` attributes.

### Prefer to NOT Skip

Skipping tests is fine for combinatorial-mode, but in pairwise-mode this leads to us potentially skipping many test combinations that the pairwise algorithm chooses for us. We don't usually have skip logic in tests though, so this should be uncommon.

### Prefer to create new attributes for re-used simple data

It's common to have multiple different integrations use/re-use similar configuration values. In these cases it will help with maintainability and readability of the code to create a custom attribute for them similar to the `MetadataSchemaVersionData` and `DbmPropagationModesData`

### Avoid Non-Serializable Data

This is mainly a visual bug and doesn't impact the functionality anyway, but when Xunit isn't able to serialize the test data it just treats all of the different input parameters of a test as a "single" test.

What this means though in our case is that we won't have much visibility in being able to see *what* tests are being run in pairwise-mode.

"Non-Serializable" data is usually any non-primitive data type used as a test parameter.
To fix, create a class (if necessary) and implement `IXunitSerializable` - see the examples in `HttpMessageHandlerTests`. Note that there are already implementations for `SerializableList<T>` and `SerializableDictionary` which serve as serializable versions of `List<T>` and `Dictionary<string, string>`.

