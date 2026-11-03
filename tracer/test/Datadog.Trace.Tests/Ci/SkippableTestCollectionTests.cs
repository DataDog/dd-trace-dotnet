// <copyright file="SkippableTestCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
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
        var tests = new SkippableTestCollection(source);
        source[0] = Candidate("""{"arguments":{"value":"two"}}""");

        Match(tests, ["one"], OneParameter).Should().NotBeNull();
        Match(tests, ["two"], OneParameter).Should().BeNull();
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

    private static void Sample(object value)
    {
    }

    private static void TwoArguments(object first, object second)
    {
    }

    private static SkippableTest Candidate(string? parameters, string? module = null, bool missingCoverage = false)
        => new("Test", "Suite", parameters, module is null ? null : new TestsConfigurations("linux", string.Empty, "x64", null, null, null, null, module), missingCoverage);

    private static SkippableTest? Match(SkippableTestCollection tests, object[]? arguments, ParameterInfo[]? parameters, string? displayName = null, bool includeDisplayNameInFingerprint = true)
    {
        var query = new SkippableTestCollection.Query(arguments, parameters, displayName, includeDisplayNameInFingerprint, useParameterIndexForUnnamedParameters: false);
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
}
