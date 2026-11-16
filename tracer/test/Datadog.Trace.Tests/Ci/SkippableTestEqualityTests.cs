// <copyright file="SkippableTestEqualityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Ci;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class SkippableTestEqualityTests
{
    [Fact]
    public void EqualConfigurationsSupportTypedEqualityAndHashCollections()
    {
        var custom = new Dictionary<string, string> { ["key"] = "value" };
        var first = Configuration(custom);
        var second = Configuration(custom);

        AssertEqual(first, second);
        AssertEqual(default(TestsConfigurations), default(TestsConfigurations));
    }

    [Fact]
    public void EqualCandidatesSupportTypedEqualityAndHashCollections()
    {
        var custom = new Dictionary<string, string> { ["key"] = "value" };

        AssertEqual(Candidate(Configuration(custom)), Candidate(Configuration(custom)));
        AssertEqual(Candidate(null), Candidate(null));
        AssertEqual(default(SkippableTest), default(SkippableTest));
    }

    [Theory]
    [InlineData(nameof(SkippableTest.Name))]
    [InlineData(nameof(SkippableTest.Suite))]
    [InlineData(nameof(SkippableTest.RawParameters))]
    [InlineData(nameof(SkippableTest.Configurations))]
    [InlineData(nameof(SkippableTest.MissingLineCodeCoverage))]
    public void EveryCandidateFieldParticipatesInEquality(string field)
    {
        var original = Candidate(Configuration());
        var different = new SkippableTest(
            field == nameof(SkippableTest.Name) ? "test" : original.Name,
            field == nameof(SkippableTest.Suite) ? "suite" : original.Suite,
            field == nameof(SkippableTest.RawParameters) ? "other" : original.RawParameters,
            field == nameof(SkippableTest.Configurations) ? Configuration(bundle: "other") : original.Configurations,
            field == nameof(SkippableTest.MissingLineCodeCoverage) ? true : original.MissingLineCodeCoverage);

        AssertDistinct(original, different);
    }

    [Theory]
    [InlineData(nameof(TestsConfigurations.OSPlatform))]
    [InlineData(nameof(TestsConfigurations.OSVersion))]
    [InlineData(nameof(TestsConfigurations.OSArchitecture))]
    [InlineData(nameof(TestsConfigurations.RuntimeName))]
    [InlineData(nameof(TestsConfigurations.RuntimeVersion))]
    [InlineData(nameof(TestsConfigurations.RuntimeArchitecture))]
    [InlineData(nameof(TestsConfigurations.TestBundle))]
    [InlineData(nameof(TestsConfigurations.Custom))]
    public void EveryConfigurationFieldParticipatesInEquality(string field)
    {
        var original = Configuration(new Dictionary<string, string> { ["key"] = "value" });
        var different = new TestsConfigurations(
            field == nameof(TestsConfigurations.OSPlatform) ? "Linux" : original.OSPlatform,
            field == nameof(TestsConfigurations.OSVersion) ? "other" : original.OSVersion,
            field == nameof(TestsConfigurations.OSArchitecture) ? "other" : original.OSArchitecture,
            field == nameof(TestsConfigurations.RuntimeName) ? "other" : original.RuntimeName,
            field == nameof(TestsConfigurations.RuntimeVersion) ? "other" : original.RuntimeVersion,
            field == nameof(TestsConfigurations.RuntimeArchitecture) ? "other" : original.RuntimeArchitecture,
            field == nameof(TestsConfigurations.Custom) ? new Dictionary<string, string> { ["key"] = "value" } : original.Custom,
            field == nameof(TestsConfigurations.TestBundle) ? "other" : original.TestBundle);

        AssertDistinct(original, different);
        AssertDistinct(Candidate(original), Candidate(different));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData(false, true)]
    public void MissingCoverageRetainsAllThreeStates(bool? first, bool? second)
    {
        AssertDistinct(new SkippableTest("Test", "Suite", null, null, first), new SkippableTest("Test", "Suite", null, null, second));
    }

    [Fact]
    public void NullAndEmptyFieldsRemainDistinct()
    {
        AssertDistinct(Candidate(null), Candidate(default(TestsConfigurations)));
        AssertDistinct(Configuration(), Configuration(new Dictionary<string, string>()));
        AssertDistinct(Configuration(bundle: null), Configuration(bundle: string.Empty));
        AssertDistinct(new SkippableTest("Test", "Suite", null, null), new SkippableTest("Test", "Suite", string.Empty, null));
    }

    [Fact]
    public void CustomDictionaryMutationDoesNotInvalidateHashKeys()
    {
        var custom = new Dictionary<string, string> { ["key"] = "value" };
        var configuration = Configuration(custom);
        var candidate = Candidate(configuration);
        var configurationHash = configuration.GetHashCode();
        var candidateHash = candidate.GetHashCode();
        var configurations = new Dictionary<TestsConfigurations, int> { [configuration] = 1 };
        var candidates = new Dictionary<SkippableTest, int> { [candidate] = 2 };

        custom["key"] = "changed";
        custom["extra"] = "added";

        configuration.GetHashCode().Should().Be(configurationHash);
        candidate.GetHashCode().Should().Be(candidateHash);
        configurations[Configuration(custom)].Should().Be(1);
        candidates[Candidate(Configuration(custom))].Should().Be(2);
    }

    [Fact]
    public void HistoricalParametersDoNotAllShareOneHash()
    {
        var hashes = new HashSet<int>();
        for (var i = 0; i < 128; i++)
        {
            hashes.Add(new SkippableTest("Test", "Suite", i.ToString(CultureInfo.InvariantCulture), null).GetHashCode());
        }

        // Collisions are legal, but hashing only the shared name makes every insertion scan the whole bucket.
        hashes.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void ModuleConfigurationsDoNotAllShareOneHash()
    {
        var configurationHashes = new HashSet<int>();
        var candidateHashes = new HashSet<int>();
        for (var i = 0; i < 128; i++)
        {
            var configuration = Configuration(bundle: i.ToString(CultureInfo.InvariantCulture));
            configurationHashes.Add(configuration.GetHashCode());
            candidateHashes.Add(Candidate(configuration).GetHashCode());
        }

        configurationHashes.Count.Should().BeGreaterThan(1);
        candidateHashes.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void JsonRoundTripPreservesTheBackendFields()
    {
        const string json = """
                            {"name":"Test","suite":"Suite","parameters":"{}","configurations":{"os.platform":"linux","os.version":"1","os.architecture":"x64","runtime.name":"dotnet","runtime.version":"8","runtime.architecture":"x64","test.bundle":"Module","custom":{"key":"value"}},"_is_missing_line_code_coverage":null}
                            """;
        var candidate = JsonHelper.DeserializeObject<SkippableTest>(json);

        candidate.Name.Should().Be("Test");
        candidate.RawParameters.Should().Be("{}");
        candidate.MissingLineCodeCoverage.Should().BeNull();
        candidate.Configurations.Should().NotBeNull();
        candidate.Configurations.GetValueOrDefault().Custom.Should().Equal(new Dictionary<string, string> { ["key"] = "value" });
        JToken.DeepEquals(JsonHelper.ParseJObject(JsonHelper.SerializeObject(candidate)), JsonHelper.ParseJObject(json)).Should().BeTrue();
    }

    private static TestsConfigurations Configuration(Dictionary<string, string>? custom = null, string? bundle = "Module")
        => new("linux", "1", "x64", "dotnet", "8", "x64", custom, bundle);

    private static SkippableTest Candidate(TestsConfigurations? configuration)
        => new("Test", "Suite", "{}", configuration);

    private static void AssertEqual<T>(T first, T second)
        where T : struct
    {
        first.Should().BeAssignableTo<IEquatable<T>>().Which.Equals(second).Should().BeTrue();
        EqualityComparer<T>.Default.Equals(first, second).Should().BeTrue();
        first.Equals((object)second).Should().BeTrue();
        first.Equals(null).Should().BeFalse();
        first.Equals(new object()).Should().BeFalse();
        first.GetHashCode().Should().Be(second.GetHashCode());
        new HashSet<T> { first, second }.Should().ContainSingle();
        var dictionary = new Dictionary<T, int> { [first] = 1 };
        dictionary[second].Should().Be(1);
    }

    private static void AssertDistinct<T>(T first, T second)
        where T : struct
    {
        EqualityComparer<T>.Default.Equals(first, second).Should().BeFalse();
        EqualityComparer<T>.Default.Equals(second, first).Should().BeFalse();
        first.Equals((object)second).Should().BeFalse();
        new HashSet<T> { first, second }.Should().HaveCount(2);
        var dictionary = new Dictionary<T, int> { [first] = 1, [second] = 2 };
        dictionary[first].Should().Be(1);
        dictionary[second].Should().Be(2);
    }
}
