// <copyright file="FlagEvaluationContextSnapshotTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using FluentAssertions;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class FlagEvaluationContextSnapshotTests
{
    [Fact]
    public void CapturesOnlyFlatScalarsAndOmitsRootTargetingKey()
    {
        var context = EvaluationContext.Builder()
                                       .Set("targetingKey", "subject")
                                       .Set("user", new Value(new Structure(new Dictionary<string, Value>
                                       {
                                           ["active"] = new(true),
                                           ["age"] = new(42),
                                           ["country"] = new("US"),
                                           ["targetingKey"] = new("nested"),
                                           ["tags"] = new(new List<Value> { new("paid"), new(false) }),
                                       })))
                                       .Build();

        var snapshot = FlagEvaluationContextSnapshot.Capture(context, out var reasons);

        snapshot.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["user.active"] = true,
            ["user.age"] = 42d,
            ["user.country"] = "US",
            ["user.targetingKey"] = "nested",
            ["user.tags.0"] = "paid",
            ["user.tags.1"] = false,
        });
        reasons.Should().Be(0);
    }

    [Fact]
    public void OversizedMapsUseRepeatableBoundedSelection()
    {
        var values = Enumerable.Range(0, 300).ToDictionary(i => "field" + i, i => new Value(i));
        var firstContext = EvaluationContext.Builder().Set("data", new Value(new Structure(values))).Build();
        var rebuiltContext = EvaluationContext.Builder().Set("data", new Value(new Structure(values.Reverse().ToDictionary(p => p.Key, p => p.Value)))).Build();

        var first = FlagEvaluationContextSnapshot.Capture(firstContext, out var reasons);

        first.Should().HaveCount(256);
        reasons.Should().NotBe(0);
        FlagEvaluationContextSnapshot.Capture(firstContext, out _).Should().BeEquivalentTo(first);
        FlagEvaluationContextSnapshot.Capture(rebuiltContext, out _).Should().BeEquivalentTo(first);
    }

    [Fact]
    public void SnapshotDoesNotRetainMutableInputs()
    {
        var list = new List<Value> { new("before") };
        var values = new Dictionary<string, Value> { ["items"] = new(list) };
        var builder = EvaluationContext.Builder().Set("data", new Value(new Structure(values)));
        var context = builder.Build();
        var snapshot = FlagEvaluationContextSnapshot.Capture(context, out _);

        list[0] = new("after");
        values.Clear();
        builder.Set("data", "after");

        snapshot.Should().BeEquivalentTo(new Dictionary<string, object?> { ["data.items.0"] = "before" });
    }

    [Fact]
    public void OmitsUnsupportedAndOversizedValuesWithoutTruncation()
    {
        var context = EvaluationContext.Builder()
                                       .Set(new string('k', 257), "oversized key")
                                       .Set("oversized", new string('v', 257))
                                       .Set(new string('k', 256), new string('v', 256))
                                       .Set("date", new Value(DateTime.UtcNow))
                                       .Set("null", new Value())
                                       .Set("nan", double.NaN)
                                       .Set("positiveInfinity", double.PositiveInfinity)
                                       .Set("negativeInfinity", double.NegativeInfinity)
                                       .Build();

        var snapshot = FlagEvaluationContextSnapshot.Capture(context, out var reasons);

        snapshot.Should().BeEquivalentTo(new Dictionary<string, object?> { [new string('k', 256)] = new string('v', 256) });
        reasons.Should().Be((int)(ContextOmissionReason.MaxKeyLength | ContextOmissionReason.MaxValueLength | ContextOmissionReason.UnsupportedValue));
    }

    [Fact]
    public void FlattenedKeyLengthIsBounded()
    {
        var context = EvaluationContext.Builder().Set("parent", new Value(new Structure(new Dictionary<string, Value> { [new string('k', 256)] = new(true) }))).Build();

        FlagEvaluationContextSnapshot.Capture(context, out var reasons).Should().BeEmpty();
        reasons.Should().Be((int)ContextOmissionReason.MaxKeyLength);
    }

    [Fact]
    public void ListTraversalStopsEvenWhenEveryValueIsDiscarded()
    {
        var list = Enumerable.Repeat(new Value(double.NaN), 256).Concat(new[] { new Value("must not visit") }).ToList();
        var context = EvaluationContext.Builder().Set("list", new Value(list)).Build();

        FlagEvaluationContextSnapshot.Capture(context, out var reasons).Should().BeEmpty();
        reasons.Should().Be((int)(ContextOmissionReason.MaxListElements | ContextOmissionReason.UnsupportedValue));
    }

    [Fact]
    public void RetainedFieldLimitAppliesAcrossContainers()
    {
        var list = Enumerable.Range(0, 200).Select(i => new Value(i)).ToList();
        var context = EvaluationContext.Builder().Set("a", new Value(list)).Set("b", new Value(list)).Build();

        FlagEvaluationContextSnapshot.Capture(context, out var reasons).Should().HaveCount(256);
        ((ContextOmissionReason)reasons).Should().HaveFlag(ContextOmissionReason.MaxContextFields);
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void DepthIsBounded(int depth, bool retained)
    {
        var value = new Value("leaf");
        for (var i = 1; i < depth; i++)
        {
            value = new Value(new Structure(new Dictionary<string, Value> { ["child"] = value }));
        }

        var context = EvaluationContext.Builder().Set("root", value).Build();
        var snapshot = FlagEvaluationContextSnapshot.Capture(context, out var reasons);

        snapshot.Should().HaveCount(retained ? 1 : 0);
        reasons.Should().Be(retained ? 0 : (int)ContextOmissionReason.MaxSnapshotDepth);
    }

    [Fact]
    public void TotalVisitsAreBoundedEvenWhenNothingIsRetained()
    {
        var value = new Value(double.NaN);
        for (var i = 0; i < 3; i++)
        {
            value = new Value(Enumerable.Repeat(value, 256).ToList());
        }

        var context = EvaluationContext.Builder().Set("root", value).Build();

        FlagEvaluationContextSnapshot.Capture(context, out var reasons).Should().BeEmpty();
        ((ContextOmissionReason)reasons).Should().HaveFlag(ContextOmissionReason.MaxVisitedNodes);
    }

    [Fact]
    public void MissingContextIsEmpty()
    {
        FlagEvaluationContextSnapshot.Capture(null, out var reasons).Should().BeEmpty();
        reasons.Should().Be(0);
    }
}
