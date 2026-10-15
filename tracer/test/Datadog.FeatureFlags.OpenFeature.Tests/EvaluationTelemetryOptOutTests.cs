// <copyright file="EvaluationTelemetryOptOutTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NET6_0_OR_GREATER
extern alias Tracer;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;
using Xunit.Abstractions;
using ExposureEvent = Tracer::Datadog.Trace.FeatureFlags.Exposure.Model.ExposureEvent;
using TracerFlags = Tracer::Datadog.Trace.FeatureFlags;
using TracerModel = Tracer::Datadog.Trace.FeatureFlags.Rcm.Model;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

[Collection(nameof(EvaluationTelemetryOptOutCollection))]
public class EvaluationTelemetryOptOutTests(ITestOutputHelper output) : IAsyncLifetime
{
    private const string Parameter = "dd_evaluation_telemetry_opt_out";
    private const string EnvironmentVariable = "DD_FEATURE_FLAGS_EVALUATION_TELEMETRY_PER_SUBJECT_OPT_OUT_ENABLED";

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Api.Instance.ShutdownAsync();

    [Fact]
    public void ConstructorsRejectNullOptionsAndEvaluator()
    {
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new DatadogProvider((DatadogProviderOptions)null!)).ParamName);
        Assert.Equal("evaluate", Assert.Throws<ArgumentNullException>(() => new DatadogProvider((FeatureFlagsSdk.EvaluationCallback)null!)).ParamName);
    }

    [Fact]
    public async Task EnvironmentActivationPreservesResultAndExposureWithoutEvaluationTelemetry()
    {
        var previous = Environment.GetEnvironmentVariable(EnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(EnvironmentVariable, "true");
            using var harness = new Harness(new DatadogProviderOptions());
            var client = await harness.CreateClient();

            var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(true));

            output.WriteLine($"metrics={harness.Metrics.Count}, span_calls={harness.SpanCalls.Count}, exposures={harness.Exposures.Count}, value={result.Value}, variant={result.Variant}, reason={result.Reason}");
            Assert.True(result.Value);
            Assert.Equal("on", result.Variant);
            Assert.Equal("static", result.Reason);
            Assert.Single(harness.Exposures);
            Assert.Empty(harness.Metrics);
            Assert.Empty(harness.SpanCalls);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvironmentVariable, previous);
        }
    }

    [Theory]
    [InlineData(null, null, 1)]
    [InlineData("true", null, 0)]
    [InlineData("false", null, 1)]
    [InlineData("invalid", null, 1)]
    [InlineData("", null, 1)]
    [InlineData("true", false, 1)]
    [InlineData("false", true, 0)]
    [InlineData("invalid", true, 0)]
    public async Task EnvironmentConfigurationRespectsExplicitProviderOptions(string? value, bool? option, int emissions)
    {
        using var environment = new EnvironmentScope(value);
        using var harness = new Harness(new DatadogProviderOptions { EnableEvaluationTelemetryOptOutParameter = option });
        var client = await harness.CreateClient();

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(true));

        Assert.True(result.Value);
        Assert.Equal("on", result.Variant);
        Assert.Equal("static", result.Reason);
        Assert.Equal(emissions, harness.Metrics.Count);
        Assert.Equal(emissions, harness.SpanCalls.Count);
        Assert.Single(harness.Exposures);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("false")]
    [InlineData("string")]
    public async Task EnvironmentActivationAloneDoesNotSuppressTelemetry(string kind)
    {
        using var environment = new EnvironmentScope("true");
        using var harness = new Harness(new DatadogProviderOptions());
        var client = await harness.CreateClient();
        var builder = EvaluationContext.Builder().SetTargetingKey("subject");
        if (kind != "missing")
        {
            builder.Set(Parameter, kind == "string" ? new Value("true") : new Value(false));
        }

        Assert.True(await client.GetBooleanValueAsync(harness.FlagKey, false, builder.Build()));
        Assert.Single(harness.Metrics);
        Assert.Single(harness.SpanCalls);
        Assert.Single(harness.Exposures);
    }

    [Theory]
    [InlineData("true", "false", 0, 1)]
    [InlineData("false", "true", 1, 0)]
    public async Task EnvironmentIsCopiedAtConstruction(string initial, string replacement, int initialEmissions, int replacementEmissions)
    {
        using var environment = new EnvironmentScope(initial);
        using var original = new Harness(new DatadogProviderOptions());
        Environment.SetEnvironmentVariable(EnvironmentVariable, replacement);
        using var later = new Harness(new DatadogProviderOptions());
        var originalClient = await original.CreateClient();
        var laterClient = await later.CreateClient();

        Assert.True(await originalClient.GetBooleanValueAsync(original.FlagKey, false, Context(true)));
        Assert.True(await laterClient.GetBooleanValueAsync(later.FlagKey, false, Context(true)));
        Assert.Equal(initialEmissions, original.Metrics.Count);
        Assert.Equal(initialEmissions, original.SpanCalls.Count);
        Assert.Equal(replacementEmissions, later.Metrics.Count);
        Assert.Equal(replacementEmissions, later.SpanCalls.Count);
        Assert.Single(original.Exposures);
        Assert.Single(later.Exposures);
    }

    [Fact]
    public async Task ParameterlessProviderReadsEnvironmentConfiguration()
    {
        using var environment = new EnvironmentScope("true");
        using var harness = new Harness();
        using var provider = new DatadogProvider();
        var client = await harness.CreateClient(provider);

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(true));

        Assert.Equal(ErrorType.ProviderNotReady, result.ErrorType);
        Assert.Empty(harness.Metrics);
    }

    [Fact]
    public async Task OptedOutEvaluationPreservesResultAndExposureWithoutEvaluationMetrics()
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(true));

        output.WriteLine($"metrics={harness.Metrics.Count}, exposures={harness.Exposures.Count}, value={result.Value}, variant={result.Variant}, reason={result.Reason}");
        Assert.True(result.Value);
        Assert.Equal("on", result.Variant);
        Assert.Equal("static", result.Reason);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Single(harness.Exposures);
        Assert.Empty(harness.Metrics);
        Assert.Empty(harness.SpanCalls);
        Assert.False(harness.SpanState.BuildSpanTags().HasAny);
        Assert.Equal(true, harness.Exposures.Single().Subject.Attributes[Parameter]);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("string")]
    [InlineData("number")]
    [InlineData("list")]
    [InlineData("structure")]
    [InlineData("old-key")]
    [InlineData("wrong-case")]
    public async Task OnlyTheExactKeyAndBooleanTrueSuppressTelemetry(string kind)
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        var builder = EvaluationContext.Builder().SetTargetingKey("subject");
        var value = kind switch
        {
            "false" => new Value(false),
            "null" => new Value(),
            "string" => new Value("true"),
            "number" => new Value(1),
            "list" => new Value(new List<Value> { new(true) }),
            "structure" => new Value(new Structure(new Dictionary<string, Value> { ["value"] = new(true) })),
            _ => new Value(true),
        };
        if (kind != "missing")
        {
            builder.Set(kind == "old-key" ? "dd_telemetry_opt_out" : kind == "wrong-case" ? "DD_EVALUATION_TELEMETRY_OPT_OUT" : Parameter, value);
        }

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, builder.Build());

        Assert.True(result.Value);
        Assert.Single(harness.Metrics);
        Assert.Single(harness.SpanCalls);
        Assert.Single(harness.Exposures);
        Assert.True(harness.SpanState.BuildSpanTags().HasAny);
    }

    [Fact]
    public async Task DisabledOptionKeepsTelemetryAndCachedHooks()
    {
        using var harness = new Harness(new DatadogProviderOptions());
        var client = await harness.CreateClient();
        Assert.Same(harness.Provider.GetProviderHooks(), harness.Provider.GetProviderHooks());

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(true));

        Assert.True(result.Value);
        Assert.Single(harness.Metrics);
        Assert.Single(harness.SpanCalls);
        Assert.Single(harness.Exposures);
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 0)]
    public async Task PublicConstructorsRequireExplicitActivation(bool useOptions, bool enabled, int emissions)
    {
        using var harness = new Harness();
        using var provider = useOptions
            ? new DatadogProvider(new DatadogProviderOptions { EnableEvaluationTelemetryOptOutParameter = enabled })
            : new DatadogProvider();
        var client = await harness.CreateClient(provider);

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(true));

        Assert.False(result.Value);
        Assert.Equal(ErrorType.ProviderNotReady, result.ErrorType);
        Assert.Equal(emissions, harness.Metrics.Count);
    }

    [Fact]
    public async Task OptionsAreCopiedAtConstruction()
    {
        var options = new DatadogProviderOptions { EnableEvaluationTelemetryOptOutParameter = true };
        using var harness = new Harness(options);
        options.EnableEvaluationTelemetryOptOutParameter = false;
        var client = await harness.CreateClient();

        Assert.True(await client.GetBooleanValueAsync(harness.FlagKey, false, Context(true)));
        Assert.Empty(harness.Metrics);
        Assert.Empty(harness.SpanCalls);
        Assert.Single(harness.Exposures);
    }

    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 1)]
    public async Task BeforeHookOverridesTheEarlierContext(bool initial, bool final, int emissions)
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        client.SetContext(Context(initial));
        var hook = new RecordingHook(_ => Task.FromResult<EvaluationContext?>(Context(final)));
        client.AddHooks([hook]);

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false);

        Assert.True(result.Value);
        Assert.Equal(emissions, harness.Metrics.Count);
        Assert.Equal(emissions, harness.SpanCalls.Count);
        Assert.Single(harness.Exposures);
        Assert.Equal(final, harness.Exposures.Single().Subject.Attributes[Parameter]);
        Assert.Equal(new[] { "Before", "After", "Finally" }, hook.Stages);
    }

    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 1)]
    public async Task InvocationContextOverridesClientContext(bool clientValue, bool invocationValue, int emissions)
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        client.SetContext(Context(clientValue));

        Assert.True(await client.GetBooleanValueAsync(harness.FlagKey, false, Context(invocationValue)));
        Assert.Equal(emissions, harness.Metrics.Count);
        Assert.Equal(emissions, harness.SpanCalls.Count);
        Assert.Single(harness.Exposures);
    }

    [Theory]
    [InlineData(true, null, null, null, 0)]
    [InlineData(true, false, null, null, 1)]
    [InlineData(false, true, false, null, 1)]
    [InlineData(false, false, true, null, 0)]
    [InlineData(false, false, true, false, 1)]
    [InlineData(false, false, false, true, 0)]
    public async Task GlobalClientTransactionAndInvocationContextsUseOpenFeaturePrecedence(bool globalValue, bool? clientValue, bool? transactionValue, bool? invocationValue, int emissions)
    {
        Api.Instance.SetContext(Context(globalValue));
        Api.Instance.SetTransactionContextPropagator(new AsyncLocalTransactionContextPropagator());
        using var harness = new Harness();
        var client = await harness.CreateClient();
        if (clientValue.HasValue)
        {
            client.SetContext(Context(clientValue.Value));
        }

        if (transactionValue.HasValue)
        {
            Api.Instance.SetTransactionContext(Context(transactionValue.Value));
        }

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, invocationValue.HasValue ? Context(invocationValue.Value) : null);

        Assert.True(result.Value);
        Assert.Equal(emissions, harness.Metrics.Count);
        Assert.Equal(emissions, harness.SpanCalls.Count);
        Assert.Single(harness.Exposures);
    }

    [Theory]
    [InlineData("Before", 0)]
    [InlineData("After", 1)]
    public async Task HookFailuresPreserveDefaultsAndCustomerHooks(string stage, int exposures)
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        var hook = new RecordingHook(failureStage: stage);
        client.AddHooks([hook]);

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(true));

        Assert.False(result.Value);
        Assert.Equal(ErrorType.General, result.ErrorType);
        Assert.Equal(exposures, harness.Exposures.Count);
        Assert.Empty(harness.Metrics);
        Assert.Empty(harness.SpanCalls);
        Assert.Contains("Error", hook.Stages);
        Assert.Contains("Finally", hook.Stages);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task ErrorResultsPreserveDefaultsAndGateErrorTelemetry(bool optedOut, int emissions)
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();

        var result = await client.GetStringDetailsAsync(harness.FlagKey, "fallback", Context(optedOut));

        Assert.Equal("fallback", result.Value);
        Assert.Equal(ErrorType.TypeMismatch, result.ErrorType);
        Assert.Equal(emissions, harness.Metrics.Count);
        Assert.Equal(emissions, harness.SpanCalls.Count);
        Assert.Empty(harness.Exposures);
    }

    [Fact]
    public async Task OptOutPreservesEarlierEmissionsAndExposureDeduplication()
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        var allowed = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(false));
        var previousTags = harness.SpanState.BuildSpanTags();

        var optedOut = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(true));

        Assert.Equal(allowed.Value, optedOut.Value);
        Assert.Equal(allowed.Variant, optedOut.Variant);
        Assert.Equal(allowed.Reason, optedOut.Reason);
        Assert.Equal(allowed.ErrorType, optedOut.ErrorType);
        Assert.Single(harness.Metrics);
        Assert.Single(harness.SpanCalls);
        Assert.Equal(previousTags.FlagsEnc, harness.SpanState.BuildSpanTags().FlagsEnc);
        Assert.Equal(previousTags.SubjectsEnc, harness.SpanState.BuildSpanTags().SubjectsEnc);
        Assert.Equal(2, harness.Exposures.Count);
        Assert.Single(harness.DeduplicatedExposures);
    }

    [Fact]
    public async Task NestedEvaluationsHaveIndependentDecisions()
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        client.AddHooks([new RecordingHook(async context =>
        {
            if (context.TargetingKey == "outer")
            {
                Assert.True(await client.GetBooleanValueAsync(harness.FlagKey, false, Context(false, "inner")));
            }

            return null;
        })]);

        Assert.True(await client.GetBooleanValueAsync(harness.FlagKey, false, Context(true, "outer")));
        Assert.Single(harness.Metrics);
        Assert.Equal(new[] { "inner" }, harness.SpanCalls);
        Assert.Equal(2, harness.Exposures.Count);
        Assert.Equal(2, harness.DeduplicatedExposures.Count);
    }

    [Fact]
    public async Task ConcurrentEvaluationsCanShareImmutableContexts()
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        client.AddHooks([new RecordingHook(afterAsync: async () => await Task.Yield())]);
        var allowed = Context(false, "allowed");
        var optedOut = Context(true, "opted-out");

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(i =>
            client.GetBooleanValueAsync(harness.FlagKey, false, i % 2 == 0 ? allowed : optedOut)));

        Assert.All(results, Assert.True);
        Assert.Equal(20, harness.Metrics.Count);
        Assert.Equal(20, harness.SpanCalls.Count);
        Assert.All(harness.SpanCalls, subject => Assert.Equal("allowed", subject));
        Assert.Equal(40, harness.Exposures.Count);
        Assert.Equal(2, harness.DeduplicatedExposures.Count);
    }

    [Fact]
    public async Task NestedAfterEvaluationDoesNotReplaceTheOuterDecision()
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        var nested = false;
        client.AddHooks([new RecordingHook(afterAsync: async () =>
        {
            if (!nested)
            {
                nested = true;
                Assert.True(await client.GetBooleanValueAsync(harness.FlagKey, false, Context(false, "inner")));
            }
        })]);

        Assert.True(await client.GetBooleanValueAsync(harness.FlagKey, false, Context(true, "outer")));
        Assert.Single(harness.Metrics);
        Assert.Equal(new[] { "inner" }, harness.SpanCalls);
        Assert.Equal(2, harness.Exposures.Count);
    }

    [Fact]
    public async Task ProvidersKeepIndependentOptionsForTheSameContext()
    {
        using var enabled = new Harness();
        using var disabled = new Harness(new DatadogProviderOptions());
        var enabledClient = await enabled.CreateClient();
        var disabledClient = await disabled.CreateClient();
        var context = Context(true);

        Assert.True(await enabledClient.GetBooleanValueAsync(enabled.FlagKey, false, context));
        Assert.True(await disabledClient.GetBooleanValueAsync(disabled.FlagKey, false, context));
        Assert.Empty(enabled.Metrics);
        Assert.Empty(enabled.SpanCalls);
        Assert.Single(disabled.Metrics);
        Assert.Single(disabled.SpanCalls);
        Assert.Single(enabled.Exposures);
        Assert.Single(disabled.Exposures);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void SynchronousEvaluationUsesItsSuppliedContext(bool optedOut, int emissions)
    {
        using var harness = new Harness();
#pragma warning disable DDFF001 // Verify the existing experimental synchronous API.
        var result = harness.Provider.ResolveBooleanValue(harness.FlagKey, false, Context(optedOut));
#pragma warning restore DDFF001

        Assert.True(result.Value);
        Assert.Equal(emissions, harness.Metrics.Count);
        Assert.Equal(emissions, harness.SpanCalls.Count);
        Assert.Single(harness.Exposures);
    }

    [Fact]
    public async Task ReplacingTheCallerContextInAfterDoesNotChangeTheEvaluationDecision()
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        var builder = EvaluationContext.Builder().SetTargetingKey("subject").Set(Parameter, true);
        var context = builder.Build();
        client.AddHooks([new RecordingHook(after: () => builder.Set(Parameter, false))]);

        Assert.True(await client.GetBooleanValueAsync(harness.FlagKey, false, context));
        Assert.False(builder.Build().GetValue(Parameter).AsBoolean);
        Assert.True(context.GetValue(Parameter).AsBoolean);
        Assert.Empty(harness.Metrics);
        Assert.Empty(harness.SpanCalls);
        Assert.Single(harness.Exposures);
    }

    [Fact]
    public async Task BeforeFailureHidesEarlierReplacementContext()
    {
        using var harness = new Harness();
        var client = await harness.CreateClient();
        client.AddHooks([
            new RecordingHook(_ => Task.FromResult<EvaluationContext?>(Context(true))),
            new RecordingHook(failureStage: "Before"),
        ]);

        var result = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(false));

        output.WriteLine($"metrics={harness.Metrics.Count}, span_calls={harness.SpanCalls.Count}, exposures={harness.Exposures.Count}, error={result.ErrorType}");
        Assert.Equal(ErrorType.General, result.ErrorType);
        Assert.Equal(0, harness.EvaluationCount);
        Assert.Empty(harness.Exposures);
        // OpenFeature 2.3.0 does not expose the first hook's replacement context after the second
        // hook throws. Provider Before/resolution never run; Error/Finally receive the original
        // context. Set opt-out in the supplied context to suppress this aborted evaluation's
        // error metric and runtime-default annotation. This test records the upstream limitation.
        Assert.Single(harness.Metrics);
        Assert.Single(harness.SpanCalls);
        Assert.NotNull(harness.SpanState.BuildSpanTags().RuntimeDefaults);
    }

    [Fact]
    public async Task OptOutPreservesRandomizedAssignmentsAndExposureDeduplication()
    {
        using var harness = new Harness(randomized: true);
        var client = await harness.CreateClient();
        var variants = new HashSet<string?>();
        for (var i = 0; i < 16; i++)
        {
            var subject = $"subject-{i}";
            var allowed = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(false, subject));
            var optedOut = await client.GetBooleanDetailsAsync(harness.FlagKey, false, Context(true, subject));
            Assert.Equal(allowed.Value, optedOut.Value);
            Assert.Equal(allowed.Variant, optedOut.Variant);
            Assert.Equal("split", optedOut.Reason);
            Assert.Equal(ErrorType.None, optedOut.ErrorType);
            variants.Add(optedOut.Variant);
        }

        Assert.Equal(2, variants.Count);
        Assert.Equal(16, harness.Metrics.Count);
        Assert.Equal(16, harness.SpanCalls.Count);
        Assert.Equal(32, harness.Exposures.Count);
        Assert.Equal(16, harness.DeduplicatedExposures.Count);
    }

    private static EvaluationContext Context(bool optedOut, string subject = "subject")
        => EvaluationContext.Builder().SetTargetingKey(subject).Set(Parameter, optedOut).Build();

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly string? _previous = Environment.GetEnvironmentVariable(EnvironmentVariable);

        public EnvironmentScope(string? value) => Environment.SetEnvironmentVariable(EnvironmentVariable, value);

        public void Dispose() => Environment.SetEnvironmentVariable(EnvironmentVariable, _previous);
    }

    private sealed class Harness : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly TracerFlags.Exposure.ExposureCache _exposureCache = new(100);
        private int _evaluationCount;

        public Harness(DatadogProviderOptions? options = null, bool randomized = false)
        {
            var configuration = new TracerModel.ServerConfiguration
            {
                Flags = new TracerModel.FlagCollection
                {
                    [FlagKey] = new TracerModel.Flag
                    {
                        Key = FlagKey,
                        Enabled = true,
                        VariationType = TracerFlags.ValueType.Boolean,
                        Variations = new Dictionary<string, TracerModel.Variant> { ["on"] = new() { Key = "on", Value = true } },
                        Allocations = [new() { Key = "allocation", DoLog = true, Splits = [new() { VariationKey = "on", SerialId = 100 }] }],
                    },
                },
            };
            if (randomized)
            {
                var flag = configuration.Flags[FlagKey];
                flag.Variations!["off"] = new() { Key = "off", Value = false };
                flag.Allocations![0].Splits =
                [
                    new() { VariationKey = "on", SerialId = 100, Shards = [new() { Salt = "opt-out-assignment", TotalShards = 100, Ranges = [new() { Start = 0, End = 50 }] }] },
                    new() { VariationKey = "off", SerialId = 101, Shards = [new() { Salt = "opt-out-assignment", TotalShards = 100, Ranges = [new() { Start = 50, End = 100 }] }] },
                ];
            }

            var evaluator = new TracerFlags.FeatureFlagsEvaluator(RecordExposure, configuration, spanEnrichmentEnabled: true);
            Provider = new DatadogProvider(
                options ?? new DatadogProviderOptions { EnableEvaluationTelemetryOptOutParameter = true },
                (key, type, fallback, subject, attributes) =>
                {
                    Interlocked.Increment(ref _evaluationCount);
                    return new EvaluationAdapter(evaluator.Evaluate(key, (TracerFlags.ValueType)type, fallback, new TracerFlags.EvaluationContext(subject, attributes)));
                },
                (serialId, doLog, subject, hasVariant, key, value) =>
                {
                    SpanCalls.Enqueue(subject);
                    SpanState.Accumulate(serialId, doLog, subject, hasVariant, key, value);
                });
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == FlagEvalMetrics.MeterName && instrument.Name == FlagEvalMetrics.MetricName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, count, tags, _) =>
            {
                foreach (var tag in tags)
                {
                    if (tag.Key == FlagEvalMetrics.TagFlagKey && Equals(tag.Value, FlagKey))
                    {
                        Metrics.Enqueue(count);
                    }
                }
            });
            _listener.Start();
        }

        public string FlagKey { get; } = $"opt-out-{Guid.NewGuid():N}";

        public int EvaluationCount => Volatile.Read(ref _evaluationCount);

        public DatadogProvider Provider { get; }

        public ConcurrentQueue<long> Metrics { get; } = new();

        public ConcurrentQueue<ExposureEvent> Exposures { get; } = new();

        public ConcurrentQueue<ExposureEvent> DeduplicatedExposures { get; } = new();

        public ConcurrentQueue<string?> SpanCalls { get; } = new();

        public TracerFlags.SpanEnrichmentState SpanState { get; } = new();

        public async Task<IFeatureClient> CreateClient(DatadogProvider? provider = null)
        {
            var domain = Guid.NewGuid().ToString("N");
            await Api.Instance.SetProviderAsync(domain, provider ?? Provider);
            return Api.Instance.GetClient(domain);
        }

        public void Dispose()
        {
            _listener.Dispose();
            Provider.Dispose();
        }

        private void RecordExposure(in ExposureEvent exposure)
        {
            Exposures.Enqueue(exposure);
            if (_exposureCache.Add(exposure))
            {
                DeduplicatedExposures.Enqueue(exposure);
            }
        }
    }

    private sealed class RecordingHook(Func<EvaluationContext, Task<EvaluationContext?>>? before = null, string? failureStage = null, Action? after = null, Func<Task>? afterAsync = null) : Hook
    {
        public ConcurrentQueue<string> Stages { get; } = new();

        public override async ValueTask<EvaluationContext> BeforeAsync<T>(HookContext<T> context, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Record("Before");
            return (before is null ? null : await before(context.EvaluationContext))!;
        }

        public override async ValueTask AfterAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Record("After");
            after?.Invoke();
            if (afterAsync is not null)
            {
                await afterAsync();
            }
        }

        public override ValueTask ErrorAsync<T>(HookContext<T> context, Exception error, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Record("Error");
            return default;
        }

        public override ValueTask FinallyAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        {
            Record("Finally");
            return default;
        }

        private void Record(string stage)
        {
            Stages.Enqueue(stage);
            if (stage == failureStage)
            {
                throw new InvalidOperationException($"{stage} failed");
            }
        }
    }

    private sealed class EvaluationAdapter(TracerFlags.Evaluation evaluation) : IEvaluation
    {
        public string FlagKey => evaluation.FlagKey;

        public object? Value => evaluation.Value;

        public EvaluationReason Reason => (EvaluationReason)evaluation.Reason;

        public string? Variant => evaluation.Variant;

        public string? Error => evaluation.Error;

        public IDictionary<string, string>? FlagMetadata => evaluation.FlagMetadata;
    }
}
#endif
