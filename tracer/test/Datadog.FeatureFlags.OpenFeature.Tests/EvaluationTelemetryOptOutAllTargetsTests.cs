// <copyright file="EvaluationTelemetryOptOutAllTargetsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

// The netcoreapp3.1 target loads the netstandard2.0 provider, whose only hook is span enrichment.
[Collection(nameof(EvaluationTelemetryOptOutCollection))]
public class EvaluationTelemetryOptOutAllTargetsTests : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Api.Instance.ShutdownAsync();

    [Theory]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 0)]
    public async Task SpanEnrichmentIsGatedOnEveryTargetFramework(bool enabled, bool optedOut, int spanCalls)
    {
        var calls = new ConcurrentQueue<string?>();
        using var provider = CreateProvider(enabled, calls);
        var domain = Guid.NewGuid().ToString("N");
        await Api.Instance.SetProviderAsync(domain, provider);

        var result = await Api.Instance.GetClient(domain).GetBooleanDetailsAsync("flag", false, Context(optedOut));

        Assert.True(result.Value);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Equal(spanCalls, calls.Count);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void SynchronousSpanEnrichmentIsGatedOnEveryTargetFramework(bool optedOut, int spanCalls)
    {
        var calls = new ConcurrentQueue<string?>();
        using var provider = CreateProvider(enabled: true, calls);
#pragma warning disable DDFF001 // Verify the experimental synchronous API.
        var result = provider.ResolveBooleanValue("flag", false, Context(optedOut));
#pragma warning restore DDFF001

        Assert.True(result.Value);
        Assert.Equal(spanCalls, calls.Count);
    }

    private static DatadogProvider CreateProvider(bool enabled, ConcurrentQueue<string?> calls)
        => new(
            new DatadogProviderOptions { EnableEvaluationTelemetryOptOutParameter = enabled },
            (key, _, _, _, _) => new StubEvaluation(key),
            (_, _, subject, _, _, _) => calls.Enqueue(subject));

    private static EvaluationContext Context(bool optedOut)
        => EvaluationContext.Builder().SetTargetingKey("subject").Set("dd_evaluation_telemetry_opt_out", optedOut).Build();

    private sealed class StubEvaluation(string flagKey) : IEvaluation
    {
        public string FlagKey => flagKey;

        public object? Value => true;

        public EvaluationReason Reason => EvaluationReason.Static;

        public string? Variant => "on";

        public string? Error => null;

        public IDictionary<string, string>? FlagMetadata { get; } = new Dictionary<string, string>
        {
            [FeatureFlagMetadataKeys.SplitSerialId] = "100",
            [FeatureFlagMetadataKeys.DoLog] = "true",
        };
    }
}
