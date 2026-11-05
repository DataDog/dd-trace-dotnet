// <copyright file="ExposureCommitTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

extern alias Tracer;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;
using TracerFlags = Tracer::Datadog.Trace.FeatureFlags;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

public class ExposureCommitTests
{
    [Fact]
    public async Task DirectProviderResolutionCommitsCompletedGraphExposures()
    {
        var commits = 0;
        using var provider = new DatadogProvider((key, type, fallback, targetingKey, attributes) =>
            new TestEvaluation(key, () => Interlocked.Increment(ref commits)));

        var result = await provider.ResolveBooleanValueAsync("root", false);

        result.Value.Should().BeTrue();
        Volatile.Read(ref commits).Should().Be(1);
    }

    [Fact]
    public async Task FeatureClientAfterHookFailureDoesNotDiscardCompletedGraphExposures()
    {
        var commits = 0;
        var domain = $"exposure-failure-{Guid.NewGuid():N}";
        using var provider = new DatadogProvider((key, type, fallback, targetingKey, attributes) =>
            new TestEvaluation(key, () => Interlocked.Increment(ref commits)));
        await Api.Instance.SetProviderAsync(domain, provider);
        var client = Api.Instance.GetClient(domain);
        client.AddHooks(new FailingAfterHook());

        var result = await client.GetBooleanDetailsAsync("root", false);

        result.ErrorType.Should().Be(ErrorType.General);
        result.Value.Should().BeFalse();
        Volatile.Read(ref commits).Should().Be(1, "exposure transactionality ends when the evaluator graph succeeds");
    }

    [Fact]
    public async Task SuccessfulFeatureClientEvaluationsCommitTheirOwnOrderedBatchOnce()
    {
        var batches = new ConcurrentQueue<string[]>();
        var domain = $"exposure-success-{Guid.NewGuid():N}";
        using var provider = new DatadogProvider((key, type, fallback, targetingKey, attributes) =>
        {
            var ownedBatch = new[] { $"{key}-dependency", key };
            return new TestEvaluation(key, () => batches.Enqueue(ownedBatch));
        });
        await Api.Instance.SetProviderAsync(domain, provider);
        var client = Api.Instance.GetClient(domain);

        await Task.WhenAll(
            client.GetBooleanDetailsAsync("left", false),
            client.GetBooleanDetailsAsync("right", false));

        batches.Should().HaveCount(2);
        batches.Should().Contain(batch => batch.SequenceEqual(new[] { "left-dependency", "left" }));
        batches.Should().Contain(batch => batch.SequenceEqual(new[] { "right-dependency", "right" }));
    }

    private sealed class FailingAfterHook : Hook
    {
        public override ValueTask AfterAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("after failed");
    }

    private sealed class TestEvaluation(string flagKey, Action commit) : IEvaluation
    {
        public string FlagKey { get; } = flagKey;

        public object Value => true;

        public EvaluationReason Reason => EvaluationReason.Static;

        public string Variant => "on";

        public string? Error => null;

        public IDictionary<string, string> FlagMetadata { get; } = new TracerFlags.ExposureCommitMetadata(null, commit);
    }
}
