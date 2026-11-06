// <copyright file="SkippableTestMatchingEquivalenceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.Reflection;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Ci;

public class SkippableTestMatchingEquivalenceTests
{
    [Theory]
    [InlineData(17, ".")]
    [InlineData(43, ",")]
    [InlineData(97, ".")]
    [InlineData(193, ",")]
    public void IndexSelectsTheSameCandidateAsTheLinearMatcher(int seed, string decimalSeparator)
    {
        string?[] payloads =
        [
            null, string.Empty, " ", "{}", "null", "invalid", "[]", "{\"arguments\":null}",
            """{"arguments":{}}""",
            """{"arguments":{"value":"one"}}""",
            """{"arguments":{"value":"(default)"}}""",
            """{"arguments":{"value":null}}""",
            """{"arguments":{"value":1.5}}""",
            """{"arguments":{"value":true}}""",
            """{"arguments":{"value":"[one, (null)]"}}""",
            """{"arguments":{"value":"\ud800"}}""",
            """{"arguments":{"value":"é😀"}}""",
            """{"arguments":{"Value":"one"}}""",
            """{"arguments":{"":"one"}}""",
            """{"arguments":{"value":"one","other":"two"}}""",
            """{"arguments":{"second":"two","first":"one"}}""",
            """{"metadata":{"test_name":"row"},"arguments":{"value":"one"}}""",
            """{"metadata":{"test_name":1.5},"arguments":{"value":"one"}}""",
            """{"metadata":{"test_name":null},"arguments":{"value":"one"}}""",
            """{"metadata":{"test_name":""},"arguments":{"value":"one"}}""",
            """{"metadata":{"test_name":"row"}}""",
            """{"metadata":{"test_name":"Row"},"arguments":{}}""",
            """{"metadata":{"_dd.parameters_format":"sha256-v1"},"arguments":{"_dd.parameters_fingerprint":"ac566f47cd0f489c807eb20ab7abe3fca081efd040d898778ae99fb935c6cbb6"}}""",
            """{"metadata":{"_dd.parameters_format":"sha256-v1"},"arguments":{"_dd.parameters_fingerprint":"44807e69196b853703e1452c1c76409905d18f04ff286c207ce5301edabbb06e"}}""",
            """{"metadata":{"_dd.parameters_format":"sha256-v1"},"arguments":{"_dd.parameters_fingerprint":"2cff0229b2965041a8cf5e35fb80d288ba94eb633926d98d870408ba80e88035"}}"""
        ];
        ParameterInfo[] oneParameter = [new NamedParameter("value")];
        object?[] arrayArgument = ["one", null];
        QueryCase[] queries =
        [
            new(null, null), new([], null), new([], []), new(null, oneParameter), new([], oneParameter),
            new(["one"], null), new(["one"], []), new(["one"], oneParameter), new(["absent"], oneParameter),
            new([null!], oneParameter), new(["one", "ignored"], oneParameter), new([true], oneParameter),
            new(["1.5"], oneParameter), new(["1,5"], oneParameter), new([1.5], oneParameter),
            new([arrayArgument], oneParameter), new(["\ud800"], oneParameter), new(["é😀"], oneParameter),
            new(["one"], [new NamedParameter(null)]), new(["one"], [new NamedParameter(string.Empty)]),
            new(["one", "one"], [new NamedParameter("value"), new NamedParameter("value")]),
            new(["one", "two"], [new NamedParameter("first"), new NamedParameter("second")]),
            new(["one"], [new NamedParameter("first"), new NamedParameter("second")])
        ];
        string?[] modules = [null, string.Empty, "module", "Module", "missing"];
        string?[] displayNames = [null, string.Empty, "row", "Row", "1,5", "1.5"];
        // A local fixed seed makes a failing input reproducible without depending on other tests' random state.
        var random = new Random(seed);
        var previousCulture = CultureInfo.CurrentCulture;
        var matches = 0;
        var misses = 0;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NumberDecimalSeparator = decimalSeparator;
            CultureInfo.CurrentCulture = culture;
            for (var iteration = 0; iteration < 30; iteration++)
            {
                // Different ordered subsets exercise misses as well as competing matches.
                var candidates = new SkippableTest[random.Next(1, 25)];
                for (var i = 0; i < candidates.Length; i++)
                {
                    var module = modules[random.Next(modules.Length)];
                    candidates[i] = new SkippableTest(
                        "Test",
                        "Suite",
                        payloads[random.Next(payloads.Length)],
                        new TestsConfigurations("linux", string.Empty, "x64", null, null, null, null, module),
                        missingLineCodeCoverage: i % 2 == 0);
                }

                var index = new SkippableTestCollection(candidates);
                foreach (var module in modules)
                {
                    foreach (var input in queries)
                    {
                        var displayName = displayNames[random.Next(displayNames.Length)];
                        for (var flags = 0; flags < 4; flags++)
                        {
                            var includeDisplayName = (flags & 1) != 0;
                            var useParameterIndex = (flags & 2) != 0;
                            var expected = LinearMatch(candidates, input, module, displayName, includeDisplayName, useParameterIndex);
                            var query = new SkippableTestCollection.Query(input.Arguments, input.Parameters, displayName, includeDisplayName, useParameterIndex);
                            try
                            {
                                var found = index.ForModule(module).TryMatch(ref query, out var actual);
                                found.Should().Be(expected.HasValue, "seed {0}, iteration {1}, module {2}, display {3}, flags {4}", seed, iteration, module, displayName, flags);
                                if (expected.HasValue)
                                {
                                    actual.Should().Be(expected.Value);
                                    matches++;
                                }
                                else
                                {
                                    misses++;
                                }
                            }
                            finally
                            {
                                query.Dispose();
                            }
                        }
                    }
                }
            }

            // Guard against accidentally turning this into an all-negative generator.
            matches.Should().BeGreaterThan(100);
            misses.Should().BeGreaterThan(100);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static SkippableTest? LinearMatch(SkippableTest[] candidates, QueryCase query, string? module, string? displayName, bool includeDisplayName, bool useParameterIndex)
    {
        // Reference behavior from Common.ShouldSkip before indexing (40a11ab8).
        // Deliberately keep the sequential scan independent of the index and Query implementation.
        string? localFingerprint = null;
        foreach (var candidate in candidates)
        {
            if (!candidate.MatchesModuleScope(module))
            {
                continue;
            }

            TestParameters? parameters = null;
            if (!StringUtil.IsNullOrWhiteSpace(candidate.RawParameters) && !candidate.TryGetParameters(out parameters))
            {
                continue;
            }

            if (parameters?.TryGetFingerprint(out var fingerprint) == true)
            {
                localFingerprint ??= Common.CreateTestParameters(query.Arguments, query.Parameters, includeDisplayName ? displayName : null, useParameterIndex).GetFingerprint();
                if (fingerprint == localFingerprint)
                {
                    return candidate;
                }

                continue;
            }

            var metadataMatches = parameters?.Metadata is null ||
                                  !parameters.Metadata.TryGetValue(TestTags.MetadataTestName, out var name) ||
                                  StringUtil.IsNullOrEmpty(name?.ToString()) ||
                                  (!StringUtil.IsNullOrEmpty(displayName) && name?.ToString() == displayName);
            if ((parameters?.Arguments is null || parameters.Arguments.Count == 0) &&
                (query.Arguments is null || query.Arguments.Length == 0) && metadataMatches)
            {
                return candidate;
            }

            if (parameters?.Arguments is not { } arguments || query.Arguments is null || query.Parameters is null || arguments.Count != query.Parameters.Length)
            {
                continue;
            }

            var matches = true;
            for (var i = 0; i < query.Parameters.Length; i++)
            {
                var target = i < query.Arguments.Length ? Common.GetParametersValueData(query.Arguments[i]) : "(default)";
                if (!arguments.TryGetValue(query.Parameters[i].Name ?? string.Empty, out var value) || (value as string ?? value?.ToString() ?? "(null)") != target)
                {
                    matches = false;
                    break;
                }
            }

            if (matches && metadataMatches)
            {
                return candidate;
            }
        }

        return null;
    }

    private readonly struct QueryCase(object[]? arguments, ParameterInfo[]? parameters)
    {
        public readonly object[]? Arguments = arguments;
        public readonly ParameterInfo[]? Parameters = parameters;
    }

    private sealed class NamedParameter(string? name) : ParameterInfo
    {
        public override string? Name => name;
    }
}
