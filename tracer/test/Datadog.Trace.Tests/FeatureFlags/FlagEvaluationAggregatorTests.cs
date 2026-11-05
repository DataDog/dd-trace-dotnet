// <copyright file="FlagEvaluationAggregatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FlagEvaluationAggregatorTests
{
    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 2)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 1)]
    public void FullIdentityIncludesConsent(bool first, bool second, int buckets)
    {
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(Observation(consent: first));
        aggregator.Add(Observation(consent: second));

        var drained = aggregator.Drain();

        drained.Full.Should().HaveCount(buckets);
        drained.Full.Values.Sum(e => e.Count).Should().Be(2);
        drained.Degraded.Should().BeEmpty();
        drained.Dropped.Should().Be(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DegradedIdentityExcludesConsentAndSubject(bool first, bool second)
    {
        var aggregator = new FlagEvaluationAggregator(0, 0, 10);
        aggregator.Add(Observation(consent: first, target: "first", attrs: new Dictionary<string, object?> { ["country"] = "US" }));
        aggregator.Add(Observation(consent: second, target: "second", attrs: new Dictionary<string, object?> { ["country"] = "CA" }));

        var drained = aggregator.Drain();
        var entry = drained.Degraded.Should().ContainSingle().Subject.Value;

        drained.Full.Should().BeEmpty();
        entry.Count.Should().Be(2);
        entry.ObserveFullEvaluationData.Should().Be(first && second);
        entry.ContextAttrs.Should().BeNull();
    }

    [Fact]
    public void ProtectedEventsNeverInspectCallerAttributes()
    {
        var observation = Observation(attrs: new ThrowingAttributes());
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);

        observation.ContextAttrs.Should().BeNull();
        aggregator.Add(observation);
        aggregator.Add(Observation(attrs: new Dictionary<string, object?> { ["pii"] = "different" }));

        var entry = aggregator.Drain().Full.Should().ContainSingle().Subject.Value;
        entry.Count.Should().Be(2);
        entry.ContextAttrs.Should().BeNull();
    }

    [Fact]
    public void EventConstructorIndependentlyDiscardsProtectedAttributes()
    {
        Observation(attrs: new Dictionary<string, object?> { ["pii"] = "canary" }).ContextAttrs.Should().BeNull();
    }

    [Fact]
    public void InvalidTargetIsOmittedAndCountedOncePerAdmission()
    {
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(Observation(target: "\uD800"));
        aggregator.Add(Observation(target: "\uDC00"));
        aggregator.Add(Observation(target: null));
        aggregator.Add(Observation(target: string.Empty));

        var drained = aggregator.Drain();

        drained.InvalidTargetingKeys.Should().Be(2);
        drained.Full.Should().HaveCount(2);
        drained.Full.Single(p => p.Key.TargetingKey is null).Value.Count.Should().Be(3);
        drained.Full.Single(p => p.Key.TargetingKey == string.Empty).Value.Count.Should().Be(1);
        aggregator.Drain().InvalidTargetingKeys.Should().Be(0);
    }

    [Fact]
    public void AggregationIndependentlyDiscardsInjectedProtectedAttributes()
    {
        var observation = Observation(attrs: null);
        // Deliberately bypass the event constructor's normalization to test the next boundary.
        typeof(FlagEvalEvent).GetField("<ContextAttrs>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                             .SetValue(observation, new Dictionary<string, object?> { ["pii"] = "injected-canary" });
        observation.ContextAttrs.Should().NotBeNull();
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);

        aggregator.Add(observation);
        aggregator.Add(Observation());

        var bucket = aggregator.Drain().Full.Should().ContainSingle().Subject;
        bucket.Key.ContextKey.Should().BeEmpty();
        bucket.Value.ContextAttrs.Should().BeNull();
        bucket.Value.Count.Should().Be(2);
    }

    [Fact]
    public void OwnsBoundedContextAndCanonicalKeyRetainsScalarTypes()
    {
        var attrs = new Dictionary<string, object?> { ["value"] = 1, ["country"] = "US" };
        var observation = Observation(consent: true, attrs: attrs);
        attrs["value"] = "changed";
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(observation);
        aggregator.Add(Observation(consent: true, attrs: new Dictionary<string, object?> { ["country"] = "US", ["value"] = 1 }));
        aggregator.Add(Observation(consent: true, attrs: new Dictionary<string, object?> { ["country"] = "US", ["value"] = "1" }));
        aggregator.Add(Observation(consent: true, attrs: new Dictionary<string, object?> { ["country"] = "US", ["value"] = 1d }));

        var drained = aggregator.Drain();

        drained.Full.Should().HaveCount(3);
        drained.Full.Values.Select(e => e.Count).Should().BeEquivalentTo(new long[] { 2, 1, 1 });
        observation.ContextAttrs!["value"].Should().Be(1);
    }

    [Fact]
    public void SnapshotFailurePreservesEvaluationAndDoesNotCallToString()
    {
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(Observation(consent: true, attrs: new ThrowingAttributes()));
        aggregator.Add(Observation(consent: true, attrs: new Dictionary<string, object?> { ["unsupported"] = new ExplosiveValue() }));

        var entry = aggregator.Drain().Full.Should().ContainSingle().Subject.Value;
        entry.Count.Should().Be(2);
        entry.ContextAttrs.Should().BeNull();
    }

    [Fact]
    public void ExistingBucketsStillIncrementAtCapacityAndDrainResetsState()
    {
        var aggregator = new FlagEvaluationAggregator(1, 1, 1);
        aggregator.Add(Observation(target: "first", time: 20));
        aggregator.Add(Observation(target: "first", time: 10));
        aggregator.Add(Observation(target: "second", time: 40));
        aggregator.Add(Observation(target: "third", time: 30));
        aggregator.Add(Observation(flag: "different"));

        var drained = aggregator.Drain();
        var full = drained.Full.Should().ContainSingle().Subject.Value;
        var degraded = drained.Degraded.Should().ContainSingle().Subject.Value;

        full.Count.Should().Be(2);
        full.FirstEvaluationMs.Should().Be(10);
        full.LastEvaluationMs.Should().Be(20);
        degraded.Count.Should().Be(2);
        degraded.FirstEvaluationMs.Should().Be(30);
        degraded.LastEvaluationMs.Should().Be(40);
        drained.Dropped.Should().Be(1);
        aggregator.Add(Observation(flag: "different"));
        aggregator.Drain().Full.Should().ContainSingle();
        aggregator.Drain().Full.Should().BeEmpty();
    }

    [Fact]
    public void PerFlagLimitDoesNotPreventAnotherFlagsFullBucket()
    {
        var aggregator = new FlagEvaluationAggregator(10, 1, 10);
        aggregator.Add(Observation(target: "first"));
        aggregator.Add(Observation(target: "second"));
        aggregator.Add(Observation(flag: "different"));

        var drained = aggregator.Drain();

        drained.Full.Should().HaveCount(2);
        drained.Degraded.Should().ContainSingle();
    }

    [Fact]
    public void NullAndEmptyVariantsAreDistinctInBothTiers()
    {
        foreach (var cap in new[] { 0, 10 })
        {
            var aggregator = new FlagEvaluationAggregator(cap, cap, 10);
            aggregator.Add(Observation(variant: null));
            aggregator.Add(Observation(variant: string.Empty));

            var drained = aggregator.Drain();
            (drained.Full.Count + drained.Degraded.Count).Should().Be(2);
        }
    }

    [Fact]
    public void ErrorsAreSanitizedBeforeTheyBecomeAggregationDimensions()
    {
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(Observation(error: "private one"));
        aggregator.Add(Observation(error: "private two"));

        var bucket = aggregator.Drain().Full.Should().ContainSingle().Subject;

        bucket.Key.Dimensions.ErrorCode.Should().Be("GENERAL");
        bucket.Value.Count.Should().Be(2);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void EntryDefensivelyFoldsConsentAndReleasesContext(bool first, bool second)
    {
        var entry = new EvaluationEntry(20, first, new Dictionary<string, object?> { ["pii"] = "canary" });

        entry.Observe(10, second);

        entry.ObserveFullEvaluationData.Should().BeFalse();
        entry.ContextAttrs.Should().BeNull();
        entry.Count.Should().Be(2);
        entry.FirstEvaluationMs.Should().Be(10);
        entry.LastEvaluationMs.Should().Be(20);
    }

    private static FlagEvalEvent Observation(string flag = "flag", string? variant = "on", string? target = "subject", long time = 10, bool consent = false, IReadOnlyDictionary<string, object?>? attrs = null, string? error = null) =>
        new(flag, variant, "allocation", target, time, attrs, error, consent);

    private sealed class ExplosiveValue
    {
        public override string ToString() => throw new InvalidOperationException("pii-canary");
    }

    private sealed class ThrowingAttributes : IReadOnlyDictionary<string, object?>
    {
        public IEnumerable<string> Keys => throw new InvalidOperationException();

        public IEnumerable<object?> Values => throw new InvalidOperationException();

        public int Count => throw new InvalidOperationException();

        public object? this[string key] => throw new InvalidOperationException();

        public bool ContainsKey(string key) => throw new InvalidOperationException();

        public bool TryGetValue(string key, out object? value) => throw new InvalidOperationException();

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => throw new InvalidOperationException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
