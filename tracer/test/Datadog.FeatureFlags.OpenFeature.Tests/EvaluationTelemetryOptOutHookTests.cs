// <copyright file="EvaluationTelemetryOptOutHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

[Collection(nameof(EvaluationTelemetryOptOutCollection))]
public class EvaluationTelemetryOptOutHookTests
{
    private static readonly Dictionary<string, object> Hints = new() { ["hint"] = "value" };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForwardsBeforeAfterAndErrorWhateverTheDecision(bool optedOut)
    {
        var inner = new ProbeHook();
        var hook = new EvaluationTelemetryOptOutHook(inner);
        var context = CreateHookContext(optedOut);
        var details = CreateDetails();
        var error = new InvalidOperationException("boom");
        using var cts = new CancellationTokenSource();

        var returned = await hook.BeforeAsync(context, Hints, cts.Token);
        await hook.AfterAsync(context, details, Hints, cts.Token);
        await hook.ErrorAsync(context, error, Hints, cts.Token);
        await hook.FinallyAsync(context, details, Hints, cts.Token);

        Assert.Same(ProbeHook.BeforeResult, returned);
        Assert.Same(details, inner.AfterDetails);
        Assert.Same(error, inner.Error);
        Assert.Equal(optedOut ? new[] { "Before", "After", "Error" } : new[] { "Before", "After", "Error", "Finally" }, inner.Stages.ToArray());
        Assert.All(inner.Hints, h => Assert.Same(Hints, h));
        Assert.All(inner.Tokens, t => Assert.Equal(cts.Token, t));
    }

    // Independent of the installed OpenFeature version: the Before decision wins, and Finally's context is the fallback.
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(null, true, false)]
    [InlineData(null, false, true)]
    public async Task FinallyUsesTheBeforeDecisionAndFallsBackToItsOwnContext(bool? beforeOptedOut, bool finallyOptedOut, bool forwarded)
    {
        var inner = new ProbeHook();
        var hook = new EvaluationTelemetryOptOutHook(inner);
        if (beforeOptedOut.HasValue)
        {
            await hook.BeforeAsync(CreateHookContext(beforeOptedOut.Value));
        }

        await hook.FinallyAsync(CreateHookContext(finallyOptedOut), CreateDetails());

        Assert.Equal(forwarded, inner.Stages.Contains("Finally"));
    }

    // The wrapper forwards Before/After/Error for opted-out subjects, so built-in telemetry must stay Finally-only.
    [Fact]
    public void BuiltInTelemetryHooksOnlyObserveFinally()
    {
        using var provider = new DatadogProvider(
            new DatadogProviderOptions { EnableEvaluationTelemetryOptOutParameter = false },
            (key, _, _, _, _) => throw new InvalidOperationException("not evaluated"),
            (_, _, _, _, _, _) => { });

        foreach (var hook in provider.GetProviderHooks())
        {
            foreach (var name in new[] { nameof(Hook.BeforeAsync), nameof(Hook.AfterAsync), nameof(Hook.ErrorAsync) })
            {
                var declaringType = hook.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance)!.DeclaringType;
                Assert.True(declaringType == typeof(Hook), $"{hook.GetType().Name} overrides {name}; opted-out subjects would reach it.");
            }
        }
    }

    private static HookContext<bool> CreateHookContext(bool optedOut)
        => new(
            "flag",
            false,
            FlagValueType.Boolean,
            new ClientMetadata(null, null),
            new Metadata("test"),
            EvaluationContext.Builder().SetTargetingKey("subject").Set("dd_evaluation_telemetry_opt_out", optedOut).Build());

    private static FlagEvaluationDetails<bool> CreateDetails() => new("flag", true, ErrorType.None, "static", "on");

    private sealed class ProbeHook : Hook
    {
        public static readonly EvaluationContext BeforeResult = EvaluationContext.Builder().Set("probe", true).Build();

        public ConcurrentQueue<string> Stages { get; } = new();

        public ConcurrentQueue<IReadOnlyDictionary<string, object>?> Hints { get; } = new();

        public ConcurrentQueue<CancellationToken> Tokens { get; } = new();

        public object? AfterDetails { get; private set; }

        public Exception? Error { get; private set; }

        public override ValueTask<EvaluationContext> BeforeAsync<T>(HookContext<T> context, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Record("Before", hints, cancellationToken);
            return new ValueTask<EvaluationContext>(BeforeResult);
        }

        public override ValueTask AfterAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Record("After", hints, cancellationToken);
            AfterDetails = details;
            return default;
        }

        public override ValueTask ErrorAsync<T>(HookContext<T> context, Exception error, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Record("Error", hints, cancellationToken);
            Error = error;
            return default;
        }

        public override ValueTask FinallyAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Record("Finally", hints, cancellationToken);
            return default;
        }

        private void Record(string stage, IReadOnlyDictionary<string, object>? hints, CancellationToken token)
        {
            Stages.Enqueue(stage);
            Hints.Enqueue(hints);
            Tokens.Enqueue(token);
        }
    }
}
