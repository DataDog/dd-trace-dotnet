// <copyright file="ExposureDiagnosticsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using FluentAssertions;
using OpenFeature.Constant;
using OpenFeature.Model;
using Xunit;

namespace Datadog.FeatureFlags.OpenFeature.Tests;

// Trace listeners and the provider's static flags are process-wide.
[Collection(nameof(OpenFeatureApiHooksCollection))]
public class ExposureDiagnosticsTests
{
    [Fact]
    public async Task WithoutALoggerOnlyTheFirstCallbackFailureIsWrittenToTrace()
    {
        using var listener = RecordingTraceListener.Install();
        var hook = new ExposureHook(_ => throw new InvalidOperationException("callback-canary"));

        for (var i = 0; i < 3; i++)
        {
            await hook.FinallyAsync(HookContext(), Details(deferred: false));
        }

        listener.Count("callback-canary").Should().Be(1);
    }

    [Fact]
    public async Task TheCommitHookWritesOnlyTheFirstSwallowedFailure()
    {
        using var listener = RecordingTraceListener.Install();
        var hook = new ExposureCommitHook(_ => throw new InvalidOperationException("commit-canary"));

        for (var i = 0; i < 3; i++)
        {
            // Each result owes its own commit, so every call reaches the throwing commit.
            await hook.FinallyAsync(HookContext(), Details(deferred: true));
        }

        listener.Count("commit-canary").Should().Be(1);
    }

    [Fact]
    public void TheEvaluatedContextIsRecordedOnlyOnceAnExposureHookExists()
    {
        var flag = typeof(FeatureFlagsSdk).GetField("_exposureHookCreated", BindingFlags.NonPublic | BindingFlags.Static)!;
        var previous = flag.GetValue(null);
        try
        {
            flag.SetValue(null, false);
            FeatureFlagsSdk.GetEvaluatedContext(Resolve().FlagMetadata).Should().BeNull("no ExposureHook reads it yet");

            _ = new ExposureHook(_ => { });
            FeatureFlagsSdk.GetEvaluatedContext(Resolve().FlagMetadata)!.TargetingKey.Should().Be("user");
        }
        finally
        {
            flag.SetValue(null, previous);
        }
    }

    private static ResolutionDetails<bool> Resolve()
        => FeatureFlagsSdk.Resolve(
            "flag",
            Datadog.Trace.FeatureFlags.ValueType.Boolean,
            false,
            DeferredExposureTests.Context(),
            (f, t, d, k, a) => new ExposureHookTests.TestEvaluation(Metadata()),
            null,
            deferredExposure: false);

    private static FlagEvaluationDetails<bool> Details(bool deferred)
    {
        var resolution = FeatureFlagsSdk.GetResolutionDetails("flag", false, new ExposureHookTests.TestEvaluation(Metadata()), null, deferredExposure: deferred);
        return new FlagEvaluationDetails<bool>(resolution.FlagKey, resolution.Value, resolution.ErrorType, resolution.Reason, resolution.Variant, resolution.ErrorMessage, resolution.FlagMetadata);
    }

    private static Dictionary<string, string> Metadata()
        => new() { [FeatureFlagMetadataKeys.AllocationKey] = "alloc", [FeatureFlagMetadataKeys.DoLog] = "true" };

    private static HookContext<bool> HookContext()
        => new("flag", false, FlagValueType.Boolean, new ClientMetadata(null, null), new Metadata("test"), EvaluationContext.Builder().SetTargetingKey("user").Build());

    private sealed class RecordingTraceListener : System.Diagnostics.TraceListener
    {
        private readonly List<string> _messages = [];

        public static RecordingTraceListener Install()
        {
            var listener = new RecordingTraceListener();
            System.Diagnostics.Trace.Listeners.Add(listener);
            return listener;
        }

        public int Count(string text)
        {
            lock (_messages)
            {
                return _messages.Count(m => m.Contains(text));
            }
        }

        public override void Write(string? message) => Record(message);

        public override void WriteLine(string? message) => Record(message);

        protected override void Dispose(bool disposing)
        {
            System.Diagnostics.Trace.Listeners.Remove(this);
            base.Dispose(disposing);
        }

        private void Record(string? message)
        {
            lock (_messages)
            {
                _messages.Add(message ?? string.Empty);
            }
        }
    }
}
