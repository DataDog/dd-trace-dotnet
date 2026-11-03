// <copyright file="SkippableTestCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Util.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class SkippableTestCollectionTests
{
    private const string Fingerprint = """{"metadata":{"_dd.parameters_format":"sha256-v1"},"arguments":{"_dd.parameters_fingerprint":"ac566f47cd0f489c807eb20ab7abe3fca081efd040d898778ae99fb935c6cbb6"}}""";
    private static readonly ParameterInfo[] OneParameter = ((Action<object>)Sample).Method.GetParameters();

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("{}", true)]
    [InlineData("{\"arguments\":{}}", true)]
    [InlineData("null", false)]
    [InlineData("{\"arguments\":", false)]
    [InlineData("[]", false)]
    [InlineData("true", false)]
    [InlineData("{\"arguments\":[]}", false)]
    [InlineData("{\"metadata\":42}", false)]
    [InlineData("{\"arguments\":null}", true)]
    public void HandlesAbsentAndInvalidParameters(string? parameters, bool matches)
    {
        var tests = new SkippableTestCollection([Candidate(parameters)]);

        Match(tests, null, null).HasValue.Should().Be(matches);
    }

    [Theory]
    [InlineData("{\"arguments\":{\"value\":\"one\"}}", "one", true)]
    [InlineData("{\"arguments\":{\"value\":\"one\"}}", "two", false)]
    [InlineData("{\"arguments\":{\"value\":null}}", null, true)]
    [InlineData("{\"arguments\":{\"other\":\"one\"}}", "one", false)]
    [InlineData("{\"arguments\":{\"value\":\"one\",\"extra\":\"two\"}}", "one", false)]
    [InlineData("{\"arguments\":{\"value\":1}}", "1", true)]
    [InlineData("{\"arguments\":{\"value\":true}}", "True", true)]
    [InlineData("{\"arguments\":{\"value\":true}}", "true", false)]
    [InlineData("{\"arguments\":{\"value\":\"One\"}}", "one", false)]
    [InlineData("{\"arguments\":{\"Value\":\"one\"}}", "one", false)]
    [InlineData("{\"arguments\":{\"value\":\"\"}}", "", true)]
    [InlineData("{\"arguments\":{\"value\":\"\"}}", null, false)]
    [InlineData("{\"arguments\":{\"value\":\"(null)\"}}", null, true)]
    [InlineData("{\"arguments\":{\"value\":\"one\",\"value\":\"two\"}}", "two", true)]
    public void MatchesArgumentNamesValuesAndCount(string parameters, object? argument, bool matches)
    {
        var tests = new SkippableTestCollection([Candidate(parameters)]);

        Match(tests, [argument!], OneParameter).HasValue.Should().Be(matches);
    }

    [Fact]
    public void MatchesArgumentNamesIndependentlyOfJsonPropertyOrder()
    {
        var parameters = ((Action<object, object>)TwoArguments).Method.GetParameters();
        var tests = new SkippableTestCollection([Candidate("""{"arguments":{"second":"two","first":"one"}}""")]);

        Match(tests, ["one", "two"], parameters).Should().NotBeNull();
        Match(tests, ["two", "one"], parameters).Should().BeNull();
    }

    [Fact]
    public void PreservesMissingArgumentsAndUnavailableReflectionMetadata()
    {
        var defaults = new SkippableTestCollection([Candidate("""{"arguments":{"value":"(default)"}}""")]);
        var empty = new SkippableTestCollection([Candidate("""{"arguments":{}}""")]);
        var absent = new SkippableTestCollection([Candidate(null)]);

        Match(defaults, [], OneParameter).Should().NotBeNull();
        Match(defaults, null, OneParameter).Should().BeNull();
        Match(empty, ["extra"], []).Should().NotBeNull();
        Match(absent, ["extra"], []).Should().BeNull();
        Match(absent, [], null).Should().NotBeNull();
        Match(defaults, ["(default)"], null).Should().BeNull();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("different", false)]
    [InlineData("row", true)]
    public void RequiresMatchingDisplayNameWhenCandidateIncludesIt(string? displayName, bool matches)
    {
        var tests = new SkippableTestCollection([Candidate("""{"metadata":{"test_name":"row"},"arguments":{"value":"one"}}""")]);

        Match(tests, ["one"], OneParameter, displayName).HasValue.Should().Be(matches);
    }

    [Theory]
    [CombinatorialData]
    public void KeepsResponseOrderAcrossWildcardAndNamedCandidates(bool wildcardFirst)
    {
        var wildcard = Candidate("""{"arguments":{"value":"one"}}""", missingCoverage: true);
        var named = Candidate("""{"metadata":{"test_name":"row"},"arguments":{"value":"one"}}""");
        var tests = new SkippableTestCollection(wildcardFirst ? [wildcard, named] : [named, wildcard]);

        Match(tests, ["one"], OneParameter, "row").Should().Be(wildcardFirst ? wildcard : named);
    }

    [Theory]
    [CombinatorialData]
    public void KeepsResponseOrderAcrossFingerprintAndRegularCandidates(bool fingerprintFirst)
    {
        var fingerprint = Candidate(Fingerprint, missingCoverage: true);
        var regular = Candidate("""{"arguments":{"value":"one"}}""");
        var tests = new SkippableTestCollection(fingerprintFirst ? [fingerprint, regular] : [regular, fingerprint]);

        Match(tests, ["one"], OneParameter, "row").Should().Be(fingerprintFirst ? fingerprint : regular);
        Match(new SkippableTestCollection([fingerprint]), ["one"], OneParameter, "different").Should().BeNull();
    }

    [Fact]
    public void SupportsFingerprintWithoutDisplayName()
    {
        var tests = new SkippableTestCollection([Candidate("""{"metadata":{"_dd.parameters_format":"sha256-v1"},"arguments":{"_dd.parameters_fingerprint":"44807e69196b853703e1452c1c76409905d18f04ff286c207ce5301edabbb06e"}}""")]);

        Match(tests, ["one"], OneParameter, "row", includeDisplayNameInFingerprint: false).Should().NotBeNull();
        Match(tests, ["one"], OneParameter, "row").Should().BeNull();
    }

    [Fact]
    public void KeepsNonStringJsonConversionInTheQueryCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var tests = new SkippableTestCollection([Candidate("""{"arguments":{"value":1.5}}""")]);
            // Keep the formatting difference explicit even when ICU locale data is unavailable.
            var queryCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            queryCulture.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = queryCulture;

            Match(tests, ["1,5"], OneParameter).Should().NotBeNull();
            Match(tests, ["1.5"], OneParameter).Should().BeNull();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [CombinatorialData]
    public void KeepsResponseOrderAcrossIndexedAndNonStringCandidates(bool nonStringFirst)
    {
        var number = Candidate("""{"arguments":{"value":1}}""", missingCoverage: true);
        var text = Candidate("""{"arguments":{"value":"1"}}""");
        var tests = new SkippableTestCollection(nonStringFirst ? [number, text] : [text, number]);

        Match(tests, ["1"], OneParameter).Should().Be(nonStringFirst ? number : text);
    }

    [Fact]
    public void PreservesRepeatedAndUnnamedReflectionParameterNames()
    {
        var repeated = new SkippableTestCollection([Candidate("""{"arguments":{"value":"one","other":"two"}}""")]);
        ParameterInfo[] repeatedNames = [new TestParameterInfo("value"), new TestParameterInfo("value")];
        var unnamed = new SkippableTestCollection([Candidate("""{"arguments":{"":"one"}}""")]);

        Match(repeated, ["one", "one"], repeatedNames).Should().NotBeNull();
        Match(repeated, ["one", "two"], repeatedNames).Should().BeNull();
        Match(unnamed, ["one"], [new TestParameterInfo(null)]).Should().NotBeNull();
    }

    [Fact]
    public void PreservesOrderAndModuleScopeForEquivalentCandidates()
    {
        var first = Candidate("""{"arguments":{"value":"one"}}""", module: "first", missingCoverage: true);
        var unscoped = Candidate("""{"arguments":{"value":"one"}}""");
        var last = Candidate("""{"arguments":{"value":"one"}}""", module: "last", missingCoverage: true);
        var tests = new SkippableTestCollection([first, unscoped, last]);

        tests.ForModule("first").Should().Equal(first, unscoped);
        tests.ForModule("last").Should().Equal(unscoped, last);
        tests.ForModule(null).Should().Equal(unscoped);
        tests.ForModule("missing").Should().Equal(unscoped);
        Match(tests.ForModule("first"), ["one"], OneParameter).Should().Be(first);
        Match(tests.ForModule("last"), ["one"], OneParameter).Should().Be(unscoped);
    }

    [Fact]
    public void OwnsAnImmutableCandidateSnapshot()
    {
        SkippableTest[] source = [Candidate("""{"arguments":{"value":"one"}}""")];
        var original = source[0];
        var tests = new SkippableTestCollection(source);
        source[0] = Candidate("""{"arguments":{"value":"two"}}""");

        Match(tests, ["one"], OneParameter).Should().Be(original);
        Match(tests, ["two"], OneParameter).Should().BeNull();
        tests.Count.Should().Be(1);
        tests[0].Should().Be(original);
        tests.Should().Equal(original);
        Match(new SkippableTestCollection(source), ["two"], OneParameter).Should().Be(source[0]);
    }

    [Fact]
    public void SupportsConcurrentQueriesWithoutSharingLocalArgumentState()
    {
        var candidates = new SkippableTest[128];
        for (var i = 0; i < candidates.Length; i++)
        {
            candidates[i] = Candidate("{\"arguments\":{\"value\":\"" + i + "\"}}");
        }

        var tests = new SkippableTestCollection(candidates);
        Parallel.For(0, candidates.Length, i =>
        {
            Match(tests, [i.ToString(CultureInfo.InvariantCulture)], OneParameter).Should().Be(candidates[i]);
            Match(tests, ["absent-" + i], OneParameter).Should().BeNull();
        });
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("{\"test_name\":null}", true)]
    [InlineData("{\"test_name\":\"\"}", true)]
    [InlineData("{\"other\":\"ignored\"}", true)]
    [InlineData("{\"test_name\":\"row\"}", true)]
    [InlineData("{\"test_name\":\"Row\"}", false)]
    [InlineData("{\"test_name\":\" row\"}", false)]
    [InlineData("{\"test_name\":true}", false)]
    public void PreservesMetadataWildcardAndOrdinalSemantics(string? metadata, bool matches)
    {
        var tests = new SkippableTestCollection([Candidate("{\"metadata\":" + (metadata ?? "null") + ",\"arguments\":{\"value\":\"one\"}}")]);

        Match(tests, ["one"], OneParameter, "row").HasValue.Should().Be(matches);
    }

    [Fact]
    public void NonStringMetadataUsesTheQueryCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var tests = new SkippableTestCollection([Candidate("""{"metadata":{"test_name":1.5},"arguments":{"value":"one"}}""")]);
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = ",";
            CultureInfo.CurrentCulture = culture;

            Match(tests, ["one"], OneParameter, "1,5").Should().NotBeNull();
            Match(tests, ["one"], OneParameter, "1.5").Should().BeNull();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(false, "76a612fe9d7a27966c15cf0b942fbbde57e473da76bdd4941178ee65ba10a9d2")]
    [InlineData(true, "2cff0229b2965041a8cf5e35fb80d288ba94eb633926d98d870408ba80e88035")]
    public void FingerprintsUseTheFrameworkPolicyForUnnamedParameters(bool useIndex, string fingerprint)
    {
        var candidate = Candidate("{\"metadata\":{\"_dd.parameters_format\":\"sha256-v1\"},\"arguments\":{\"_dd.parameters_fingerprint\":\"" + fingerprint + "\"}}");
        var tests = new SkippableTestCollection([candidate]);

        Match(tests, ["one"], [new TestParameterInfo(null)], "row", useParameterIndexForUnnamedParameters: useIndex).Should().Be(candidate);
        Match(tests, ["one"], [new TestParameterInfo(null)], "row", useParameterIndexForUnnamedParameters: !useIndex).Should().BeNull();
    }

    [Theory]
    [CombinatorialData]
    public void FingerprintsPreserveMissingArguments(bool nullArguments)
    {
        var candidate = Candidate("""{"metadata":{"_dd.parameters_format":"sha256-v1"},"arguments":{"_dd.parameters_fingerprint":"acb3d7e377acf61f51ea48dda089a8c706943a867ea4680bc81b125ce35203f5"}}""");

        Match(new SkippableTestCollection([candidate]), nullArguments ? null : [], OneParameter).Should().Be(candidate);
    }

    [Theory]
    [InlineData("sha256-v2", "ac566f47cd0f489c807eb20ab7abe3fca081efd040d898778ae99fb935c6cbb6")]
    [InlineData("sha256-v1", "too-short")]
    [InlineData("sha256-v1", "AC566F47CD0F489C807EB20AB7ABE3FCA081EFD040D898778AE99FB935C6CBB6")]
    public void DoesNotAcceptAnUnsupportedOrDifferentFingerprint(string format, string fingerprint)
    {
        var candidate = Candidate("{\"metadata\":{\"_dd.parameters_format\":\"" + format + "\"},\"arguments\":{\"_dd.parameters_fingerprint\":\"" + fingerprint + "\"}}");

        Match(new SkippableTestCollection([candidate]), ["one"], OneParameter, "row").Should().BeNull();
    }

    [Fact]
    public void ChoosesTheFirstMatchAcrossEveryOrderingOfCandidateKinds()
    {
        SkippableTest[] candidates =
        [
            Candidate("""{"arguments":{"value":"1"}}"""),
            Candidate("""{"metadata":{"test_name":"row"},"arguments":{"value":"1"}}"""),
            Candidate("""{"arguments":{"value":1}}"""),
            Candidate("""{"metadata":{"_dd.parameters_format":"sha256-v1"},"arguments":{"_dd.parameters_fingerprint":"f5a36d456f411e6f8041b2910c87eb95cbd9676e71d1ab811b75ff7e162647b1"}}""")
        ];

        foreach (var first in candidates)
        {
            foreach (var second in candidates.Where(c => !c.Equals(first)))
            {
                foreach (var third in candidates.Where(c => !c.Equals(first) && !c.Equals(second)))
                {
                    var fourth = candidates.Single(c => !c.Equals(first) && !c.Equals(second) && !c.Equals(third));
                    var tests = new SkippableTestCollection([Candidate("invalid"), first, second, third, fourth]);

                    Match(tests, ["1"], OneParameter, "row").Should().Be(first);
                }
            }
        }
    }

    [Theory]
    [InlineData("{\"metadata\":{\"test_name\":\"row\"},\"arguments\":{\"value\":\"1\"}}")]
    [InlineData("{\"arguments\":{\"value\":1}}")]
    [InlineData("{\"metadata\":{\"_dd.parameters_format\":\"sha256-v1\"},\"arguments\":{\"_dd.parameters_fingerprint\":\"f5a36d456f411e6f8041b2910c87eb95cbd9676e71d1ab811b75ff7e162647b1\"}}")]
    public void ALaterCandidateInTheSameBucketCannotHideAnEarlierMatch(string parameters)
    {
        var first = Candidate(parameters);
        var competing = Candidate("""{"arguments":{"value":"1"}}""");
        var wrongModule = Candidate(parameters, module: "other");
        var tests = new SkippableTestCollection([first, competing, wrongModule]).ForModule("module");

        Match(tests, ["1"], OneParameter, "row").Should().Be(first);
    }

    [Fact]
    public async Task OverlappingQueriesKeepPartiallyFormattedArgumentsIsolated()
    {
        var first = Candidate("""{"arguments":{"first":"one","second":"shared"}}""");
        var second = Candidate("""{"arguments":{"first":"two","second":"shared"}}""");
        var tests = new SkippableTestCollection([first, second]);
        var parameters = ((Action<object, object>)TwoArguments).Method.GetParameters();
        using var barrier = new Barrier(2);
        var firstQuery = Task.Factory.StartNew(
            () => Match(tests, ["one", new SynchronizedArgument(barrier)], parameters),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var secondQuery = Task.Factory.StartNew(
            () => Match(tests, ["two", new SynchronizedArgument(barrier)], parameters),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        (await Task.WhenAll(firstQuery, secondQuery)).Should().Equal(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("module")]
    [InlineData("Module")]
    [InlineData("missing")]
    public void ModuleViewsHaveConsistentMatchingEnumerationAndIndexing(string? module)
    {
        var scoped = Candidate("""{"arguments":{"value":"one"}}""", module: "module");
        var unscoped = Candidate(null);
        var emptyScope = Candidate(null, module: string.Empty, missingCoverage: true);
        var view = new SkippableTestCollection([scoped, unscoped, emptyScope]).ForModule(module);
        SkippableTest[] expected = module == "module" ? [scoped, unscoped, emptyScope] : [unscoped, emptyScope];

        view.Count.Should().Be(expected.Length);
        view.Should().Equal(expected);
        var enumeratedCount = 0;
        foreach (var candidate in (IEnumerable)view)
        {
            candidate.Should().Be(expected[enumeratedCount]);
            enumeratedCount++;
        }

        enumeratedCount.Should().Be(expected.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            view[i].Should().Be(expected[i]);
        }

        var belowStart = () => view[-1];
        var pastEnd = () => view[expected.Length];
        belowStart.Should().Throw<ArgumentOutOfRangeException>();
        pastEnd.Should().Throw<ArgumentOutOfRangeException>();
        Match(view, ["one"], OneParameter).Should().Be(module == "module" ? scoped : null);
    }

    [Theory]
    [CombinatorialData]
    public void EmptyCollectionsAreSafeBeforeAndAfterModuleFiltering(bool defaultValue, bool filter)
    {
        var tests = defaultValue ? default : new SkippableTestCollection([]);
        if (filter)
        {
            tests = tests.ForModule("module");
        }

        tests.Count.Should().Be(0);
        tests.Should().BeEmpty();
        Match(tests, null, null).Should().BeNull();
        Match(tests, ["one"], OneParameter).Should().BeNull();
        var index = () => tests[0];
        index.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("{\"arguments\":{\"value\":\"wrong\"}}")]
    [InlineData("{\"arguments\":{\"wrong\":\"one\"}}")]
    [InlineData("{\"arguments\":{\"value\":\"one\",\"extra\":\"one\"}}")]
    [InlineData("{\"metadata\":{\"test_name\":\"wrong\"},\"arguments\":{\"value\":\"one\"}}")]
    public void CandidateVerificationRejectsFalsePositivesFromTheIndex(string parameters)
    {
        // A hash hit is only a candidate; exact verification must reject a collision.
        var parsed = JsonHelper.DeserializeObject<TestParameters>(parameters);
        var query = new SkippableTestCollection.Query(["one"], OneParameter, "row", includeMetadataTestNameInFingerprint: true, useParameterIndexForUnnamedParameters: false);
        try
        {
            query.Matches(parsed).Should().BeFalse();
        }
        finally
        {
            query.Dispose();
        }
    }

    [Fact]
    public void ReusesFormattedArgumentsAcrossCollectionsAndFingerprints()
    {
        var argument = new CountingArgument();
        var miss = new SkippableTestCollection([Candidate(Fingerprint.Replace("ac566", "bc566"))]);
        var hit = Candidate("""{"arguments":{"value":"one"}}""");
        var query = new SkippableTestCollection.Query([argument], OneParameter, "row", includeMetadataTestNameInFingerprint: true, useParameterIndexForUnnamedParameters: false);
        try
        {
            miss.TryMatch(ref query, out _).Should().BeFalse();
            new SkippableTestCollection([hit]).TryMatch(ref query, out var matched).Should().BeTrue();
            matched.Should().Be(hit);
            new SkippableTestCollection([Candidate(Fingerprint)]).TryMatch(ref query, out _).Should().BeTrue();
            argument.Calls.Should().Be(1);
        }
        finally
        {
            query.Dispose();
        }
    }

    [Fact]
    public void SharedIndexSupportsConcurrentQueryCulturesAndModules()
    {
        var number = Candidate("""{"arguments":{"value":1.5}}""", module: "number");
        var text = Candidate("""{"arguments":{"value":"1.5"}}""", module: "text");
        var tests = new SkippableTestCollection([number, text]);
        Parallel.For(0, 128, i =>
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                culture.NumberFormat.NumberDecimalSeparator = i % 2 == 0 ? "," : ".";
                CultureInfo.CurrentCulture = culture;

                Match(tests.ForModule("number"), [i % 2 == 0 ? "1,5" : "1.5"], OneParameter).Should().Be(number);
                Match(tests.ForModule("text"), ["1.5"], OneParameter).Should().Be(text);
                Match(tests.ForModule("missing"), ["1.5"], OneParameter).Should().BeNull();
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        });
    }

    private static void Sample(object value)
    {
    }

    private static void TwoArguments(object first, object second)
    {
    }

    private static SkippableTest Candidate(string? parameters, string? module = null, bool missingCoverage = false)
        => new("Test", "Suite", parameters, module is null ? null : new TestsConfigurations("linux", string.Empty, "x64", null, null, null, null, module), missingCoverage);

    private static SkippableTest? Match(SkippableTestCollection tests, object[]? arguments, ParameterInfo[]? parameters, string? displayName = null, bool includeDisplayNameInFingerprint = true, bool useParameterIndexForUnnamedParameters = false)
    {
        var query = new SkippableTestCollection.Query(arguments, parameters, displayName, includeDisplayNameInFingerprint, useParameterIndexForUnnamedParameters);
        try
        {
            return tests.TryMatch(ref query, out var candidate) ? candidate : null;
        }
        finally
        {
            query.Dispose();
        }
    }

    private sealed class TestParameterInfo(string? name) : ParameterInfo
    {
        public override string? Name => name;
    }

    private sealed class CountingArgument
    {
        public int Calls { get; private set; }

        public override string ToString()
        {
            Calls++;
            return "one";
        }
    }

    private sealed class SynchronizedArgument(Barrier barrier)
    {
        public override string ToString()
        {
            // The bound detects a missing participant; the barrier, not elapsed time, orders the queries.
            if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
            {
                throw new TimeoutException("Both queries must format their first argument before either continues.");
            }

            return "shared";
        }
    }
}
