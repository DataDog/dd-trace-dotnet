// <copyright file="SemVerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using FluentAssertions;
using Xunit;
using ValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

/// <summary>
/// Unit tests for SemVer parsing and comparison, ported from dd-trace-go's
/// openfeature/semver_test.go and evaluator_test.go (PR #5128).
/// </summary>
public class SemVerTests
{
    // ---------------------------------------------------------------------
    // ParseSemver tests (ported from Go TestParseSemver)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("0.0.0", 0UL, 0UL, 0UL, "")]
    [InlineData("18446744073709551615.18446744073709551615.18446744073709551615", ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, "")]
    [InlineData("1.2.3-alpha.1", 1UL, 2UL, 3UL, "alpha.1")]
    [InlineData("1.2.3-18446744073709551616", 1UL, 2UL, 3UL, "18446744073709551616")]
    [InlineData("1.2.3+build.001", 1UL, 2UL, 3UL, "")]
    [InlineData("1.2.3-alpha-1+build.001", 1UL, 2UL, 3UL, "alpha-1")]
    public void TryParseValidVersions(string version, ulong major, ulong minor, ulong patch, string prerelease)
    {
        var ok = SemVer.TryParse(version, out var result);
        ok.Should().BeTrue();
        result.Major.Should().Be(major);
        result.Minor.Should().Be(minor);
        result.Patch.Should().Be(patch);
        result.Prerelease.Should().Be(prerelease);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("v1.2.3")]
    [InlineData("01.2.3")]
    [InlineData("1.02.3")]
    [InlineData("1.2.03")]
    [InlineData("18446744073709551616.0.0")]
    [InlineData("0.18446744073709551616.0")]
    [InlineData("0.0.18446744073709551616")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3-alpha..1")]
    [InlineData("1.2.3+build..1")]
    [InlineData("1.2.3-01")]
    [InlineData("1.2.3-alpha_1")]
    [InlineData("1.2.3-alpha+build+other")]
    [InlineData("1.2.3-α")]
    [InlineData(" 1.2.3")]
    [InlineData("1.2.3 ")]
    public void TryParseInvalidVersions(string version)
    {
        var ok = SemVer.TryParse(version, out _);
        ok.Should().BeFalse();
    }

    // ---------------------------------------------------------------------
    // CompareSemver tests (ported from Go TestCompareSemver)
    // ---------------------------------------------------------------------

    [Fact]
    public void CompareSemverOrdersCorrectly()
    {
        // The canonical SemVer 2.0.0 precedence chain
        var ordered = new[]
        {
            "1.0.0-alpha",
            "1.0.0-alpha.1",
            "1.0.0-alpha.beta",
            "1.0.0-beta",
            "1.0.0-beta.2",
            "1.0.0-beta.11",
            "1.0.0-rc.1",
            "1.0.0",
            "1.0.1",
            "1.1.0",
            "2.0.0",
        };

        for (var i = 0; i < ordered.Length; i++)
        {
            SemVer.TryParse(ordered[i], out var left).Should().BeTrue();
            for (var j = 0; j < ordered.Length; j++)
            {
                SemVer.TryParse(ordered[j], out var right).Should().BeTrue();
                var ordering = SemVer.Compare(left, right);
                if (i < j)
                {
                    ordering.Should().BeNegative($"{ordered[i]} should precede {ordered[j]}");
                }
                else if (i > j)
                {
                    ordering.Should().BePositive($"{ordered[i]} should follow {ordered[j]}");
                }
                else
                {
                    ordering.Should().Be(0);
                }
            }
        }
    }

    [Fact]
    public void CompareSemverArbitrarilyLargeNumericPrereleaseIdentifiers()
    {
        SemVer.TryParse("1.0.0-99999999999999999999", out var left).Should().BeTrue();
        SemVer.TryParse("1.0.0-100000000000000000000", out var right).Should().BeTrue();
        SemVer.Compare(left, right).Should().BeNegative();
    }

    [Fact]
    public void CompareSemverBuildMetadataIsIgnored()
    {
        SemVer.TryParse("1.0.0+build.1", out var left).Should().BeTrue();
        SemVer.TryParse("1.0.0+build.2", out var right).Should().BeTrue();
        SemVer.Compare(left, right).Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // EvaluateSemverCondition tests (ported from Go TestEvaluateSemverCondition)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("SEMVER_EQ", "1.2.3", "1.2.3", true)]
    [InlineData("SEMVER_EQ", "1.2.4", "1.2.3", false)]
    [InlineData("SEMVER_NEQ", "1.2.4", "1.2.3", true)]
    [InlineData("SEMVER_NEQ", "1.2.3", "1.2.3", false)]
    [InlineData("SEMVER_LT", "1.9.9", "2.0.0", true)]
    [InlineData("SEMVER_LT", "2.0.0", "2.0.0", false)]
    [InlineData("SEMVER_LTE", "2.0.0", "2.0.0", true)]
    [InlineData("SEMVER_LTE", "2.0.1", "2.0.0", false)]
    [InlineData("SEMVER_GT", "1.0.1", "1.0.0", true)]
    [InlineData("SEMVER_GT", "1.0.0", "1.0.0", false)]
    [InlineData("SEMVER_GTE", "1.0.0", "1.0.0", true)]
    [InlineData("SEMVER_GTE", "0.9.9", "1.0.0", false)]
    // Prerelease ordering
    [InlineData("SEMVER_LT", "1.0.0-beta.1", "1.0.0", true)]
    [InlineData("SEMVER_LT", "1.0.0-beta.2", "1.0.0-beta.11", true)]
    // Build metadata is ignored
    [InlineData("SEMVER_EQ", "4.0.0+build.42", "4.0.0", true)]
    [InlineData("SEMVER_EQ", "4.0.0+exp.sha.5114f85", "4.0.0", true)]
    [InlineData("SEMVER_NEQ", "4.0.0+build.42", "4.0.0", false)]
    [InlineData("SEMVER_LT", "4.0.0+build.42", "4.0.0", false)]
    [InlineData("SEMVER_LTE", "4.0.0+build.42", "4.0.0", true)]
    [InlineData("SEMVER_GT", "4.0.0+build.42", "4.0.0", false)]
    [InlineData("SEMVER_GTE", "4.0.0+build.42", "4.0.0", true)]
    [InlineData("SEMVER_EQ", "1.0.0+linux", "1.0.0+darwin", true)]
    // Invalid attribute does not match
    [InlineData("SEMVER_NEQ", "not-a-version", "1.0.0", false)]
    [InlineData("SEMVER_GTE", "1.2", "1.0.0", false)]
    [InlineData("SEMVER_GTE", "v1.2.3", "1.0.0", false)]
    [InlineData("SEMVER_GTE", "18446744073709551616.0.0", "1.0.0", false)]
    public void EvaluateSemverConditionTests(string operatorName, string attribute, string comparand, bool want)
    {
        var op = ParseOperator(operatorName);
        var condition = new ConditionConfiguration
        {
            Operator = op,
            Attribute = "version",
            Value = comparand,
        };

        var context = new EvaluationContext("user", new Dictionary<string, object?> { { "version", attribute } });

        // Use the evaluator to test the full path
        var flags = CreateSemverTestFlag(op, comparand, want ? "matched" : "unmatched");
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });

        var result = evaluator.Evaluate("test-flag", ValueType.String, "default", context);

        if (want)
        {
            result.Value.Should().Be("matched");
            result.Reason.Should().Be(EvaluationReason.TargetingMatch);
        }
        else
        {
            result.Value.Should().Be("default");
            result.Reason.Should().Be(EvaluationReason.Default);
        }
    }

    [Fact]
    public void EvaluateSemverConditionMissingAttributeDoesNotMatch()
    {
        var condition = new ConditionConfiguration
        {
            Operator = ConditionOperator.SEMVER_EQ,
            Attribute = "version",
            Value = "1.2.3",
        };

        var flags = CreateSemverTestFlag(ConditionOperator.SEMVER_EQ, "1.2.3", "matched");
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });

        var context = new EvaluationContext("user"); // No attributes
        var result = evaluator.Evaluate("test-flag", ValueType.String, "default", context);

        result.Value.Should().Be("default");
        result.Reason.Should().Be(EvaluationReason.Default);
    }

    [Fact]
    public void EvaluateSemverConditionNonStringAttributeDoesNotMatch()
    {
        var flags = CreateSemverTestFlag(ConditionOperator.SEMVER_EQ, "1.2.0", "matched");
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });

        var context = new EvaluationContext("user", new Dictionary<string, object?> { { "version", 1.2 } }); // Non-string
        var result = evaluator.Evaluate("test-flag", ValueType.String, "default", context);

        result.Value.Should().Be("default");
        result.Reason.Should().Be(EvaluationReason.Default);
    }

    [Fact]
    public void EvaluateSemverConditionInvalidComparandReturnsParseError()
    {
        var flags = CreateSemverTestFlag(ConditionOperator.SEMVER_EQ, "not-a-version", "matched");
        var evaluator = new FeatureFlagsEvaluator(null, new ServerConfiguration { Flags = flags });

        var context = new EvaluationContext("user", new Dictionary<string, object?> { { "version", "1.2.3" } });
        var result = evaluator.Evaluate("test-flag", ValueType.String, "default", context);

        result.Value.Should().Be("default");
        result.Reason.Should().Be(EvaluationReason.Error);
        result.Error.Should().Be("PARSE_ERROR");
    }

    private static ConditionOperator ParseOperator(string name) => name switch
    {
        "SEMVER_EQ" => ConditionOperator.SEMVER_EQ,
        "SEMVER_NEQ" => ConditionOperator.SEMVER_NEQ,
        "SEMVER_LT" => ConditionOperator.SEMVER_LT,
        "SEMVER_LTE" => ConditionOperator.SEMVER_LTE,
        "SEMVER_GT" => ConditionOperator.SEMVER_GT,
        "SEMVER_GTE" => ConditionOperator.SEMVER_GTE,
        _ => throw new ArgumentException($"Unknown operator: {name}"),
    };

    private static Dictionary<string, Flag> CreateSemverTestFlag(ConditionOperator op, string comparand, string variantKey)
    {
        var variants = new Dictionary<string, Variant>
        {
            ["matched"] = new Variant { Key = "matched", Value = "matched" },
            ["unmatched"] = new Variant { Key = "unmatched", Value = "unmatched" },
        };

        var conditions = new List<ConditionConfiguration>
        {
            new ConditionConfiguration { Operator = op, Attribute = "version", Value = comparand },
        };

        var rules = new List<Rule> { new Rule(conditions) };
        var splits = new List<Split> { new Split { Shards = new List<Shard>(), VariationKey = variantKey } };
        var alloc = new Allocation { Key = "test-alloc", Rules = rules, Splits = splits, DoLog = false };

        var flag = new Flag
        {
            Key = "test-flag",
            Enabled = true,
            VariationType = ValueType.String,
            Variations = variants,
            Allocations = new List<Allocation> { alloc },
        };

        return new Dictionary<string, Flag> { ["test-flag"] = flag };
    }
}
