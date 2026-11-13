// <copyright file="DependencyEvaluationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class DependencyEvaluationTests
{
    [Fact]
    public void ContextNodeBudgetIncludesChildrenRejectedAtMaximumDepth()
    {
        var terminal = new CountingEnumerable(2_000);
        object? value = terminal;
        for (var depth = 0; depth < 31; depth++)
        {
            value = new object?[] { value };
        }

        DependencyEvaluation.SnapshotContext(
            new EvaluationContext("subject", new Dictionary<string, object?> { ["deep"] = value }),
            includeContext: true,
            out _);

        terminal.Visited.Should().BeLessOrEqualTo(1_280);
    }

    [Fact]
    public void DependencyContextFlattensNestedFieldsForEvaluationTelemetry()
    {
        var snapshot = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?> { ["country"] = "US" },
            ["tags"] = new object?[] { "paid", false },
        };

        var flattened = DependencyEvaluation.FlattenContext(snapshot, out _);
        var observation = new FlagEvalEvent("dependency", "on", null, "subject", 1, flattened, observeFullEvaluationData: true);
        var aggregator = new FlagEvaluationAggregator(10, 10, 10);
        aggregator.Add(observation);
        var payload = FlagEvaluationPayload.Encode(
            aggregator.Drain(),
            new Dictionary<string, string>(),
            flushTimeMs: 1,
            payloadLimitBytes: 1_000_000).Payloads.Single();
        var evaluationContext = JObject.Parse(Encoding.UTF8.GetString(payload))["flagEvaluations"]![0]!["context"]!["evaluation"]!;

        observation.ContextAttrs.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["user.country"] = "US",
            ["tags.0"] = "paid",
            ["tags.1"] = false,
        });
        evaluationContext["user.country"]!.Value<string>().Should().Be("US");
        evaluationContext["tags.0"]!.Value<string>().Should().Be("paid");
        evaluationContext["tags.1"]!.Value<bool>().Should().BeFalse();
    }

    [Fact]
    public void DependencyContextRejectsOversizedPathsBeforeTraversingChildren()
    {
        var oversizedKey = new string('x', 257);
        var snapshot = new Dictionary<string, object?>
        {
            [oversizedKey] = Enumerable.Range(0, 256).Cast<object?>().ToArray(),
            [new string('y', 255)] = new object?[] { "too-long" },
            ["valid"] = "retained",
        };

        DependencyEvaluation.FlattenContext(snapshot, out _).Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["valid"] = "retained",
        });
    }

    [Fact]
    public void DependencyContextSkipsOversizedKeysBeforeSnapshottingValuesAndReportsOmissions()
    {
        var throwing = new ThrowingEnumerable();
        var attributes = new Dictionary<string, object?>
        {
            [new string('x', 257)] = throwing,
            ["valid"] = "retained",
        };

        var snapshot = DependencyEvaluation.SnapshotContext(
            new EvaluationContext("subject", attributes),
            includeContext: true,
            out var snapshotOmissions);
        var flattened = DependencyEvaluation.FlattenContext(snapshot, out var flattenOmissions);

        throwing.Enumerated.Should().BeFalse();
        snapshot.Should().BeEquivalentTo(new Dictionary<string, object?> { ["valid"] = "retained" });
        flattened.Should().BeEquivalentTo(snapshot);
        (snapshotOmissions | flattenOmissions).Should().HaveFlag(ContextOmissionReason.MaxKeyLength);
    }

    [Fact]
    public void DependencyContextReportsSnapshotAndFlatteningLimits()
    {
        var throwingContext = new EvaluationContext(
            "subject",
            new Dictionary<string, object?> { ["throwing"] = new ThrowingEnumerable() });

        DependencyEvaluation.SnapshotContext(throwingContext, includeContext: true, out var snapshotOmissions).Should().BeNull();
        snapshotOmissions.Should().HaveFlag(ContextOmissionReason.SnapshotError);

        var oversized = new Dictionary<string, object?>
        {
            ["items"] = Enumerable.Range(0, 300).Cast<object?>().ToArray(),
        };
        DependencyEvaluation.FlattenContext(oversized, out var flattenOmissions).Should().HaveCount(256);
        flattenOmissions.Should().HaveFlag(ContextOmissionReason.MaxListElements);
    }

    [Fact]
    public void DependencyContextDoesNotInvokeUnsupportedValuesAndReportsThem()
    {
        var arbitrary = new ThrowingToString();
        var arbitraryKey = new ThrowingToString();
        var attributes = new Dictionary<string, object?>
        {
            ["arbitrary"] = arbitrary,
            ["token"] = new JConstructor("unsupported", 1),
            ["dictionary"] = new Hashtable { [arbitraryKey] = "value" },
        };

        var snapshot = DependencyEvaluation.SnapshotContext(
            new EvaluationContext("subject", attributes),
            includeContext: true,
            out var omissions);

        arbitrary.Called.Should().BeFalse();
        arbitraryKey.Called.Should().BeFalse();
        snapshot.Should().BeEquivalentTo(new Dictionary<string, object?>
        {
            ["arbitrary"] = null,
            ["token"] = null,
            ["dictionary"] = new Dictionary<string, object?>(),
        });
        omissions.Should().HaveFlag(ContextOmissionReason.UnsupportedValue);
        omissions.Should().NotHaveFlag(ContextOmissionReason.SnapshotError);
    }

    private sealed class CountingEnumerable(int count) : IEnumerable
    {
        public int Visited { get; private set; }

        public IEnumerator GetEnumerator()
        {
            for (var i = 0; i < count; i++)
            {
                Visited++;
                yield return i;
            }
        }
    }

    private sealed class ThrowingEnumerable : IEnumerable
    {
        public bool Enumerated { get; private set; }

        public IEnumerator GetEnumerator()
        {
            Enumerated = true;
            throw new InvalidOperationException("must not enumerate");
        }
    }

    private sealed class ThrowingToString
    {
        public bool Called { get; private set; }

        public override string ToString()
        {
            Called = true;
            throw new InvalidOperationException("must not stringify");
        }
    }
}
