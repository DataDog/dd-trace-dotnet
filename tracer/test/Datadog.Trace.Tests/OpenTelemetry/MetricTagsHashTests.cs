// <copyright file="MetricTagsHashTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Datadog.Trace.OpenTelemetry.Metrics;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry;

public class MetricTagsHashTests
{
    public static IEnumerable<object[]> NonStringValues() => new[]
    {
        new object[] { 42 },                       // int
        new object[] { -7 },                       // negative int
        new object[] { 0 },                        // zero
        new object[] { int.MaxValue },             // boundary int
        new object[] { int.MinValue },             // boundary int
        new object[] { 12345678901L },             // long
        new object[] { long.MinValue },            // boundary long
        new object[] { (short)123 },               // short
        new object[] { (byte)255 },                // byte
        new object[] { 3.14d },                    // double
        new object[] { -0.0001d },                 // small negative double
        new object[] { 1.5f },                     // float
        new object[] { true },                     // bool
        new object[] { false },                    // bool
        new object[] { 'c' },                      // char
        new object[] { 1234.5678m },               // decimal
        new object[] { (uint)4000000000 },         // uint
        new object[] { ulong.MaxValue },           // ulong
    };

    [Fact]
    public void IsDeterministic()
    {
        var tags = new[] { Tag("host", "a"), Tag("region", "us") };

        MetricTagsHash.Compute(tags).Should().Be(MetricTagsHash.Compute(tags));
    }

    [Fact]
    public void IsOrderIndependent()
    {
        var ordered = new[] { Tag("a", "1"), Tag("b", "2"), Tag("c", "3") };
        var shuffled = new[] { Tag("c", "3"), Tag("a", "1"), Tag("b", "2") };

        MetricTagsHash.Compute(ordered).Should().Be(MetricTagsHash.Compute(shuffled));
    }

    [Fact]
    public void DifferentValuesProduceDifferentHashes()
    {
        var first = new[] { Tag("host", "a") };
        var second = new[] { Tag("host", "b") };

        MetricTagsHash.Compute(first).Should().NotBe(MetricTagsHash.Compute(second));
    }

    [Fact]
    public void DifferentKeysProduceDifferentHashes()
    {
        var first = new[] { Tag("host", "a") };
        var second = new[] { Tag("region", "a") };

        MetricTagsHash.Compute(first).Should().NotBe(MetricTagsHash.Compute(second));
    }

    [Fact]
    public void KeyValueBoundaryIsUnambiguous()
    {
        // "ab" = "c" must not collide with "a" = "bc"
        var first = new[] { Tag("ab", "c") };
        var second = new[] { Tag("a", "bc") };

        MetricTagsHash.Compute(first).Should().NotBe(MetricTagsHash.Compute(second));
    }

    [Fact]
    public void KeyValueBoundaryIsUnambiguousWhenContentContainsEquals()
    {
        // A '=' inside a key/value must not shift the perceived key/value boundary:
        // {"a=b" = "c"} must not collide with {"a" = "b=c"}.
        var first = new[] { Tag("a=b", "c") };
        var second = new[] { Tag("a", "b=c") };

        MetricTagsHash.Compute(first).Should().NotBe(MetricTagsHash.Compute(second));
    }

    [Fact]
    public void PairBoundaryIsUnambiguous()
    {
        // {a=b, c=d} must not collide with {a=bc, d=} style regroupings
        var first = new[] { Tag("a", "b"), Tag("c", "d") };
        var second = new[] { Tag("a", "bcd") };

        MetricTagsHash.Compute(first).Should().NotBe(MetricTagsHash.Compute(second));
    }

    [Fact]
    public void PairBoundaryIsUnambiguousWhenContentContainsSeparators()
    {
        // A key/value that itself contains the historical separators ('=' and ';') must not
        // collapse two tags into one: {a=b, c=d} must not collide with {"a=b;c" = "d"}.
        var first = new[] { Tag("a", "b"), Tag("c", "d") };
        var second = new[] { Tag("a=b;c", "d") };

        MetricTagsHash.Compute(first).Should().NotBe(MetricTagsHash.Compute(second));
    }

    [Fact]
    public void ContentIsImmaterialToFraming()
    {
        // Length framing makes collisions structurally impossible regardless of content, including a
        // literal null char. {"a\0b" = "c"} must not collide with {"a" = "b\0c"}.
        var first = new[] { Tag("a\0b", "c") };
        var second = new[] { Tag("a", "b\0c") };

        MetricTagsHash.Compute(first).Should().NotBe(MetricTagsHash.Compute(second));
    }

    [Fact]
    public void DigitContentDoesNotBlurIntoLengthPrefix()
    {
        // The length prefix is fixed-width binary, not decimal text, so content that "looks like a
        // number" (e.g. a value ending in digits) can never be re-read as an adjacent length field.
        // {k="12", x="3"} must not collide with {k="1", x="23"} (same keys, digits regrouped).
        var first = new[] { Tag("k", "12"), Tag("x", "3") };
        var second = new[] { Tag("k", "1"), Tag("x", "23") };

        MetricTagsHash.Compute(first).Should().NotBe(MetricTagsHash.Compute(second));

        // ...and regrouping digits across the key/value boundary is equally safe.
        MetricTagsHash.Compute(new[] { Tag("k", "12") })
                      .Should().NotBe(MetricTagsHash.Compute(new[] { Tag("k1", "2") }));
    }

    [Fact]
    public void EmptyTagsAreDeterministic()
    {
        MetricTagsHash.Compute(System.Array.Empty<KeyValuePair<string, object?>>())
                      .Should().Be(MetricTagsHash.Compute(System.Array.Empty<KeyValuePair<string, object?>>()));
    }

    [Fact]
    public void NullAndEmptyStringValuesAreTreatedEquivalently()
    {
        // A null value contributes nothing after the '=' separator, matching an empty string value.
        var nullValue = new[] { Tag("host", null) };
        var emptyValue = new[] { Tag("host", string.Empty) };

        MetricTagsHash.Compute(nullValue).Should().Be(MetricTagsHash.Compute(emptyValue));
    }

    [Fact]
    // TODO: When we emit metric attributes while maintaining their primitive types, this should no longer be true.
    public void StringAndNumericValuesWithSameTextAreEquivalent()
    {
        // Values are hashed by their (invariant) string form, so the integer 1 and the string "1" hash the same.
        var numeric = new[] { Tag("count", 1) };
        var text = new[] { Tag("count", "1") };

        MetricTagsHash.Compute(numeric).Should().Be(MetricTagsHash.Compute(text));
    }

    [Theory]
    [MemberData(nameof(NonStringValues))]
    public void NonStringValuesHashAsTheirInvariantStringRepresentation(object value)
    {
        // A non-string value must hash identically to its (invariant) string representation.
        var expected = value is IFormattable formattable
                           ? formattable.ToString(format: null, CultureInfo.InvariantCulture)
                           : value.ToString();

        var typed = new[] { Tag("k", value) };
        var asString = new[] { Tag("k", expected) };

        MetricTagsHash.Compute(typed).Should().Be(MetricTagsHash.Compute(asString));
    }

    [Fact]
    public void FormatsValuesUsingInvariantCulture()
    {
        // Hashing must be culture-independent: a value formatted under a non-invariant current
        // culture must still hash as its invariant representation (e.g. "3.14", not "3,14").
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

            var typed = new[] { Tag("k", 3.14d) };
            var invariant = new[] { Tag("k", "3.14") };

            MetricTagsHash.Compute(typed).Should().Be(MetricTagsHash.Compute(invariant));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void DifferentNonStringValuesProduceDifferentHashes()
    {
        // Sanity check that formatting actually contributes to the hash for non-string types.
        MetricTagsHash.Compute(new[] { Tag("k", 1) })
                      .Should().NotBe(MetricTagsHash.Compute(new[] { Tag("k", 2) }));

        MetricTagsHash.Compute(new[] { Tag("k", true) })
                      .Should().NotBe(MetricTagsHash.Compute(new[] { Tag("k", false) }));

        MetricTagsHash.Compute(new[] { Tag("k", 1.5d) })
                      .Should().NotBe(MetricTagsHash.Compute(new[] { Tag("k", 2.5d) }));
    }

    private static KeyValuePair<string, object?> Tag(string key, object? value) => new(key, value);
}
#endif
