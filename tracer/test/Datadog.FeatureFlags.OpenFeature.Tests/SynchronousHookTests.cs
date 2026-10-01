// <copyright file="SynchronousHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
#if NET6_0_OR_GREATER
using System.Diagnostics.Metrics;
#endif
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

#pragma warning disable DDFF001

public class SynchronousHookTests
{
    [Theory]
    [InlineData(ErrorType.None, "After")]
    [InlineData(ErrorType.FlagNotFound, "Error")]
    public void SyncHooksBlockUntilEveryStageCompletes(ErrorType errorType, string resultStage)
    {
        var hook = new SlowRecordingHook();
        var context = new HookContext<bool>("flag", false, FlagValueType.Boolean, new ClientMetadata(null, null), new Metadata("test"), EvaluationContext.Empty);
        var resolution = new ResolutionDetails<bool>("flag", false, errorType);

        DatadogProvider.RunProviderHooks(ImmutableList.Create<Hook>(hook), context, resolution);

        // Each stage records after a 50 ms delay. A runner that does not wait normally returns before any stage records.
        hook.Completed.Should().Equal(resultStage, "Finally");
    }

    [Fact]
    public void SyncAfterHookFailureReturnsDefaultWithError()
    {
        var hook = new SlowRecordingHook { ThrowInAfter = true };
        var context = new HookContext<bool>("flag", false, FlagValueType.Boolean, new ClientMetadata(null, null), new Metadata("test"), EvaluationContext.Empty);

        var result = DatadogProvider.RunProviderHooks(ImmutableList.Create<Hook>(hook), context, new ResolutionDetails<bool>("flag", true, ErrorType.None, "static"));

        // Same as FeatureClient: the default value, a general error, then the Error and Finally hooks.
        result.Should().BeEquivalentTo(new { FlagKey = "flag", Value = false, ErrorType = ErrorType.General, Reason = Reason.Error, ErrorMessage = "after failed" }, o => o.ExcludingMissingMembers());
        hook.Completed.Should().Equal("Error", "Finally");
    }

    [Fact]
    public void SyncFinallyHookFailureDoesNotStopOtherHooks()
    {
        var recording = new SlowRecordingHook();
        var failing = new SlowRecordingHook { ThrowInFinally = true };
        var context = new HookContext<bool>("flag", false, FlagValueType.Boolean, new ClientMetadata(null, null), new Metadata("test"), EvaluationContext.Empty);
        var resolution = new ResolutionDetails<bool>("flag", true, ErrorType.None, "static");

        // Hooks run in reverse order, so the failing hook runs first.
        var result = DatadogProvider.RunProviderHooks(ImmutableList.Create<Hook>(recording, failing), context, resolution);

        result.Should().BeSameAs(resolution);
        recording.Completed.Should().Equal("After", "Finally");
    }

    [Fact]
    public void SyncErrorHookFailureDoesNotStopOtherHooks()
    {
        var recording = new SlowRecordingHook();
        var failing = new SlowRecordingHook { ThrowInError = true };
        var context = new HookContext<bool>("flag", false, FlagValueType.Boolean, new ClientMetadata(null, null), new Metadata("test"), EvaluationContext.Empty);
        var resolution = new ResolutionDetails<bool>("flag", false, ErrorType.FlagNotFound);

        var result = DatadogProvider.RunProviderHooks(ImmutableList.Create<Hook>(recording, failing), context, resolution);

        result.Should().BeSameAs(resolution);
        recording.Completed.Should().Equal("Error", "Finally");
        failing.Completed.Should().Equal("Finally");
    }

    [Theory]
    [InlineData(ErrorType.None, "After")]
    [InlineData(ErrorType.FlagNotFound, "Error")]
    public void SyncHooksRunInReverseOrderForEachStage(ErrorType errorType, string resultStage)
    {
        var completed = new List<string>();
        var first = new SlowRecordingHook("first-", completed);
        var second = new SlowRecordingHook("second-", completed);
        var context = new HookContext<bool>("flag", false, FlagValueType.Boolean, new ClientMetadata(null, null), new Metadata("test"), EvaluationContext.Empty);

        DatadogProvider.RunProviderHooks(ImmutableList.Create<Hook>(first, second), context, new ResolutionDetails<bool>("flag", false, errorType));

        completed.Should().Equal($"second-{resultStage}", $"first-{resultStage}", "second-Finally", "first-Finally");
    }

#if NET6_0_OR_GREATER
    [Theory]
    [InlineData("Boolean")]
    [InlineData("Double")]
    [InlineData("Integer")]
    [InlineData("String")]
    [InlineData("Structure")]
    public void SyncResolutionRecordsOneEvaluationMetricBeforeReturning(string type)
    {
        var flagKey = $"sync-{Guid.NewGuid():N}";
        using var metrics = new EvaluationMetrics(flagKey);
        using var provider = new DatadogProvider();

        _ = type switch
        {
            "Boolean" => (object)provider.ResolveBooleanValue(flagKey, true, EvaluationContext.Empty),
            "Double" => provider.ResolveDoubleValue(flagKey, 12.5, EvaluationContext.Empty),
            "Integer" => provider.ResolveIntegerValue(flagKey, 42, EvaluationContext.Empty),
            "String" => provider.ResolveStringValue(flagKey, "fallback", EvaluationContext.Empty),
            "Structure" => provider.ResolveStructureValue(flagKey, new Value(), EvaluationContext.Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        metrics.ErrorTypes.Should().Equal("provider_not_ready");
    }

    [Theory]
    [InlineData("Boolean")]
    [InlineData("Double")]
    [InlineData("Integer")]
    [InlineData("String")]
    [InlineData("Structure")]
    public async Task ClientAsyncPathStillRecordsOneEvaluationMetric(string type)
    {
        var flagKey = $"async-{Guid.NewGuid():N}";
        var domain = $"domain-{Guid.NewGuid():N}";
        using var metrics = new EvaluationMetrics(flagKey);
        using var provider = new DatadogProvider();
        await Api.Instance.SetProviderAsync(domain, provider);

        var client = Api.Instance.GetClient(domain);
        _ = type switch
        {
            "Boolean" => (object)await client.GetBooleanValueAsync(flagKey, true),
            "Double" => await client.GetDoubleValueAsync(flagKey, 12.5),
            "Integer" => await client.GetIntegerValueAsync(flagKey, 42),
            "String" => await client.GetStringValueAsync(flagKey, "fallback"),
            "Structure" => await client.GetObjectValueAsync(flagKey, new Value()),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        // The client runs the provider hooks; the async provider method must not run them again.
        metrics.ErrorTypes.Should().Equal("provider_not_ready");
    }

    private sealed class EvaluationMetrics : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly List<string?> _errorTypes = [];

        public EvaluationMetrics(string flagKey)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "Datadog.FeatureFlags.OpenFeature" && instrument.Name == "feature_flag.evaluations")
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                string? key = null, errorType = null;
                foreach (var tag in tags)
                {
                    key = tag.Key == "feature_flag.key" ? tag.Value as string : key;
                    errorType = tag.Key == "error.type" ? tag.Value as string : errorType;
                }

                if (key == flagKey)
                {
                    lock (_errorTypes)
                    {
                        _errorTypes.Add(errorType);
                    }
                }
            });
            _listener.Start();
        }

        public IReadOnlyList<string?> ErrorTypes
        {
            get
            {
                lock (_errorTypes)
                {
                    return _errorTypes.ToArray();
                }
            }
        }

        public void Dispose() => _listener.Dispose();
    }
#endif

    private sealed class SlowRecordingHook(string name = "", List<string>? completed = null) : Hook
    {
        public List<string> Completed { get; } = completed ?? [];

        public bool ThrowInAfter { get; set; }

        public bool ThrowInFinally { get; set; }

        public bool ThrowInError { get; set; }

        // The failures throw synchronously, before any task exists, which is the case a runner can miss.
        public override ValueTask AfterAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
            => ThrowInAfter ? throw new InvalidOperationException("after failed") : RecordAfterDelayAsync("After");

        public override ValueTask ErrorAsync<T>(HookContext<T> context, Exception error, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
            => ThrowInError ? throw new InvalidOperationException("error failed") : RecordAfterDelayAsync("Error");

        public override ValueTask FinallyAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
            => ThrowInFinally ? throw new InvalidOperationException("finally failed") : RecordAfterDelayAsync("Finally");

        private async ValueTask RecordAfterDelayAsync(string stage)
        {
            await Task.Delay(50).ConfigureAwait(false);
            Completed.Add(name + stage);
        }
    }
}

#pragma warning restore DDFF001
