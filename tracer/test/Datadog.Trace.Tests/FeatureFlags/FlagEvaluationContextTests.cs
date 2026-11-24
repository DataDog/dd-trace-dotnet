// <copyright file="FlagEvaluationContextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FlagEvaluationContextTests
{
    [Fact]
    public void DoesNotEnumerateBeyondTheBoundedSubset()
    {
        var attributes = new CountingAttributes();

        var copy = FlagEvaluationContext.Copy(attributes, out var omissions);

        copy.Should().HaveCount(256);
        attributes.Visited.Should().Be(256);
        omissions.Should().Be(ContextOmissionReason.MaxContextFields);
    }

    [Fact]
    public void RejectsMalformedUnsupportedAndOversizedScalars()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["valid"] = "text",
            ["\uD800"] = "invalid key",
            ["invalid value"] = "\uD800",
            [new string('k', 257)] = true,
            ["long value"] = new string('v', 257),
            ["nan"] = double.NaN,
            ["infinity"] = float.PositiveInfinity,
            ["nested"] = new Dictionary<string, string>(),
            ["null"] = null,
        };

        var copy = FlagEvaluationContext.Copy(attributes, out var omissions);

        copy.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, object?>("valid", "text"));
        omissions.Should().Be(ContextOmissionReason.UnsupportedValue | ContextOmissionReason.MaxKeyLength | ContextOmissionReason.MaxValueLength);
    }

    [Fact]
    public void CanonicalKeyIsCultureIndependentAndUnambiguous()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var attributes = new Dictionary<string, object?> { ["value"] = 1.25d, ["decimal"] = 1.25m };
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var expected = FlagEvaluationContext.CanonicalKey(attributes);
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            FlagEvaluationContext.CanonicalKey(attributes).Should().Be(expected);
            FlagEvaluationContext.CanonicalKey(new Dictionary<string, object?> { ["a"] = "b:c" })
                                 .Should().NotBe(FlagEvaluationContext.CanonicalKey(new Dictionary<string, object?> { ["a:b"] = "c" }));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    private sealed class CountingAttributes : IReadOnlyDictionary<string, object?>
    {
        public int Count => 1000;

        public int Visited { get; private set; }

        public IEnumerable<string> Keys => throw new NotSupportedException();

        public IEnumerable<object?> Values => throw new NotSupportedException();

        public object? this[string key] => throw new NotSupportedException();

        public bool ContainsKey(string key) => throw new NotSupportedException();

        public bool TryGetValue(string key, out object? value) => throw new NotSupportedException();

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (var i = 0; i < Count; i++)
            {
                Visited++;
                yield return new KeyValuePair<string, object?>(i.ToString(CultureInfo.InvariantCulture), true);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
