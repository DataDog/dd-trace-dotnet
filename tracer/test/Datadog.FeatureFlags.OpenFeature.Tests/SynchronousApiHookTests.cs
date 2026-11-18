// <copyright file="SynchronousApiHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

[Collection(nameof(OpenFeatureApiHooksCollection))]
#pragma warning disable DDFF001 // Exercise the experimental synchronous API.
public class SynchronousApiHookTests
{
    [Fact]
    public void AnApiExposureHookReceivesSynchronousEvaluationsWithTheCommittedVerdict()
    {
        var commits = 0;
        using var provider = DeferringProvider(() => ++commits == 1 ? ExposureCommitResult.New : ExposureCommitResult.Repeat);
        var received = new List<ExposureEvaluation>();

        WithApiHooks(
            [new ExposureHook(e => received.Add(e))],
            () =>
            {
                provider.ResolveBooleanValue("flag", false, DeferredExposureTests.Context());
                provider.ResolveBooleanValue("flag", false, DeferredExposureTests.Context());
            });

        commits.Should().Be(2);
        received.Should().HaveCount(2);
        received[0].ShouldSend.Should().BeTrue("the provider's commit runs before the API hook's finally stage");
        received[1].IsCacheHit.Should().BeTrue();
    }

    [Fact]
    public void OtherApiHooksDoNotRunOnTheSynchronousPath()
    {
        var commits = 0;
        using var provider = DeferringProvider(() =>
        {
            commits++;
            return ExposureCommitResult.New;
        });
        var hook = new BeforeDependentHook();
        ResolutionDetails<bool>? result = null;

        WithApiHooks([hook], () => result = provider.ResolveBooleanValue("flag", false, DeferredExposureTests.Context()));

        result!.ErrorType.Should().Be(ErrorType.None, "a hook whose after stage needs its before stage would fail the evaluation");
        result.Value.Should().BeTrue();
        hook.Stages.Should().BeEmpty();
        commits.Should().Be(1);
    }

    private static void WithApiHooks(Hook[] hooks, Action evaluate)
    {
        Api.Instance.AddHooks(hooks);
        try
        {
            evaluate();
        }
        finally
        {
            Api.Instance.ClearHooks();
        }
    }

    private static DatadogProvider DeferringProvider(Func<int> commit)
        => new(
            (f, t, d, k, a) => throw new InvalidOperationException("immediate path must not run"),
            (f, t, d, k, a) => new ExposureHookTests.TestEvaluation(new Dictionary<string, string>
            {
                [FeatureFlagMetadataKeys.AllocationKey] = "alloc",
                [FeatureFlagMetadataKeys.DoLog] = "true",
            }),
            () => true,
            _ => commit());

    // Like a timing hook that keeps state from its before stage: its after stage throws when before never ran.
    private sealed class BeforeDependentHook : Hook
    {
        public List<string> Stages { get; } = [];

        public override ValueTask<EvaluationContext> BeforeAsync<T>(HookContext<T> context, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Stages.Add("before");
            return new(context.EvaluationContext);
        }

        public override ValueTask AfterAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Stages.Add("after");
            return Stages.Contains("before") ? default : throw new InvalidOperationException("before stage did not run");
        }

        public override ValueTask FinallyAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Stages.Add("finally");
            return default;
        }
    }
}
#pragma warning restore DDFF001
