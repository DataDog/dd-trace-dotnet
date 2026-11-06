// <copyright file="DeferredExposureTests.cs" company="Datadog">
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

public class DeferredExposureTests
{
    [Fact]
    public async Task TheTracerIsAskedOncePerProvider()
    {
        var tracer = new FakeTracer(supportsDeferral: true);
        using var provider = tracer.CreateProvider();

        for (var i = 0; i < 3; i++)
        {
            await provider.ResolveBooleanValueAsync("flag", false, Context());
        }

        tracer.ProbeCalls.Should().Be(1);
    }

    [Fact]
    public async Task AnOlderTracerKeepsSendingDuringEvaluation()
    {
        var tracer = new FakeTracer(supportsDeferral: false);
        var client = await ClientAsync(tracer.CreateProvider());

        var details = await client.GetBooleanDetailsAsync("flag", false, Context());

        details.ErrorType.Should().Be(ErrorType.None);
        tracer.ImmediateEvaluations.Should().Be(1);
        tracer.DeferredEvaluations.Should().Be(0);
        tracer.Commits.Should().BeEmpty("an older tracer intercepts no commit and already sent the exposure");
    }

    [Fact]
    public async Task ANewTracerGetsOneCommitWithItsOwnMetadataInstance()
    {
        var tracer = new FakeTracer(supportsDeferral: true);
        var client = await ClientAsync(tracer.CreateProvider());

        await client.GetBooleanDetailsAsync("flag", false, Context());

        tracer.ImmediateEvaluations.Should().Be(0);
        tracer.DeferredEvaluations.Should().Be(1);
        tracer.Commits.Should().ContainSingle().Which.Should().BeSameAs(tracer.Returned[0], "the tracer finds the pending exposure by reference");
    }

    [Fact]
    public async Task AnAfterHookFailureCommitsNothing()
    {
        var tracer = new FakeTracer(supportsDeferral: true);
        var client = await ClientAsync(tracer.CreateProvider());
        var received = new List<ExposureEvaluation>();
        client.AddHooks(new ExposureHook(e => received.Add(e)));

        var details = await client.GetBooleanDetailsAsync("flag", false, Context(), new FlagEvaluationOptions(new ThrowingAfterHook()));

        details.ErrorType.Should().NotBe(ErrorType.None);
        tracer.DeferredEvaluations.Should().Be(1);
        tracer.Commits.Should().BeEmpty();
        received.Should().ContainSingle().Which.IsExposure.Should().BeFalse();
    }

    [Fact]
    public async Task TheExposureHookSeesTheCommitVerdict()
    {
        var tracer = new FakeTracer(supportsDeferral: true);
        var client = await ClientAsync(tracer.CreateProvider());
        var received = new List<ExposureEvaluation>();
        client.AddHooks(new ExposureHook(e => received.Add(e)));

        await client.GetBooleanDetailsAsync("flag", false, Context());
        await client.GetBooleanDetailsAsync("flag", false, Context());

        received.Should().HaveCount(2);
        received[0].ShouldSend.Should().BeTrue();
        received[0].IsCacheHit.Should().BeFalse();
        received[1].ShouldSend.Should().BeFalse();
        received[1].IsCacheHit.Should().BeTrue();
    }

    [Fact]
    public async Task CallingTheProviderWithoutAClientRunsNoHooksAndCommitsNothing()
    {
        var tracer = new FakeTracer(supportsDeferral: true);
        using var provider = tracer.CreateProvider();

        await provider.ResolveBooleanValueAsync("flag", false, Context());

        tracer.DeferredEvaluations.Should().Be(1);
        tracer.Commits.Should().BeEmpty();
    }

    [Theory]
    [InlineData("true", ExposureCommitResult.New, true, false)]
    [InlineData("false", ExposureCommitResult.Repeat, false, true)]
    [InlineData("false", ExposureCommitResult.NothingPending, false, false)]
    public async Task TheCommittedVerdictDecidesAndPlainHooksReadTheAdvisoryStamp(string advisory, int commit, bool expectedShouldSend, bool expectedCacheHit)
    {
        using var provider = new DatadogProvider(
            (f, t, d, k, a) => throw new InvalidOperationException("immediate path must not run"),
            (f, t, d, k, a) => new FakeEvaluation(
                new Dictionary<string, string>
                {
                    [FeatureFlagMetadataKeys.AllocationKey] = "alloc",
                    [FeatureFlagMetadataKeys.DoLog] = "true",
                    [FeatureFlagMetadataKeys.ExposureCacheHit] = advisory,
                },
                null),
            () => true,
            _ => commit);
        var client = await ClientAsync(provider);
        var received = new List<ExposureEvaluation>();
        var plain = new MetadataReadingHook();
        client.AddHooks(new ExposureHook(e => received.Add(e)));
        client.AddHooks(plain);

        await client.GetBooleanDetailsAsync("flag", false, Context());

        received.Should().ContainSingle().Which.IsExposure.Should().BeTrue();
        received[0].ShouldSend.Should().Be(expectedShouldSend);
        received[0].IsCacheHit.Should().Be(expectedCacheHit);
        plain.CacheHits.Should().Equal([advisory == "true"]);
    }

#pragma warning disable DDFF001 // Exercise the experimental synchronous API.
    [Fact]
    public void TheSynchronousApiCommitsSuccessfulEvaluationsOnly()
    {
        var tracer = new FakeTracer(supportsDeferral: true);
        using var provider = tracer.CreateProvider();

        provider.ResolveBooleanValue("flag", false, Context()).ErrorType.Should().Be(ErrorType.None);
        tracer.Fail = true;
        provider.ResolveBooleanValue("flag", false, Context()).ErrorType.Should().NotBe(ErrorType.None);

        tracer.Commits.Should().ContainSingle().Which.Should().BeSameAs(tracer.Returned[0]);
    }
#pragma warning restore DDFF001

    internal static EvaluationContext Context() => EvaluationContext.Builder().SetTargetingKey("user").Build();

    private static async Task<FeatureClient> ClientAsync(DatadogProvider provider)
    {
        var domain = $"deferred-{Guid.NewGuid():N}";
        await Api.Instance.SetProviderAsync(domain, provider);
        return Api.Instance.GetClient(domain);
    }

    private sealed class MetadataReadingHook : Hook
    {
        public List<bool?> CacheHits { get; } = [];

        public override ValueTask FinallyAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            CacheHits.Add(details.FlagMetadata?.GetBool(FeatureFlagMetadataKeys.ExposureCacheHit));
            return default;
        }
    }

    private sealed class ThrowingAfterHook : Hook
    {
        public override ValueTask AfterAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("after hook failure");
    }

    private sealed class FakeTracer(bool supportsDeferral)
    {
        public int ProbeCalls { get; private set; }

        public int ImmediateEvaluations { get; private set; }

        public int DeferredEvaluations { get; private set; }

        public bool Fail { get; set; }

        public List<IDictionary<string, string>> Returned { get; } = [];

        public List<IDictionary<string, string>> Commits { get; } = [];

        public DatadogProvider CreateProvider()
            => new(
                (flag, type, defaultValue, targetingKey, attributes) =>
                {
                    ImmediateEvaluations++;
                    return Evaluation();
                },
                (flag, type, defaultValue, targetingKey, attributes) =>
                {
                    DeferredEvaluations++;
                    return Evaluation();
                },
                () =>
                {
                    ProbeCalls++;
                    return supportsDeferral;
                },
                metadata =>
                {
                    Commits.Add(metadata);
                    return Commits.Count == 1 ? ExposureCommitResult.New : ExposureCommitResult.Repeat;
                });

        private IEvaluation Evaluation()
        {
            var metadata = new Dictionary<string, string>
            {
                ["__dd_allocation_key"] = "allocation",
                [FeatureFlagMetadataKeys.DoLog] = "true",
            };
            Returned.Add(metadata);
            return new FakeEvaluation(metadata, Fail ? "FLAG_NOT_FOUND" : null);
        }
    }

    private sealed class FakeEvaluation(IDictionary<string, string> metadata, string? error) : IEvaluation
    {
        public string FlagKey => "flag";

        public object? Value => true;

        public EvaluationReason Reason => error is null ? EvaluationReason.Static : EvaluationReason.Error;

        public string? Variant => error is null ? "on" : null;

        public string? Error => error;

        public IDictionary<string, string>? FlagMetadata => metadata;
    }
}
