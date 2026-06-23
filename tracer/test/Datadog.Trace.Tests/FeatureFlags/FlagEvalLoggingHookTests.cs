// <copyright file="FlagEvalLoggingHookTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Tests the Datadog.Trace-side EVP flag-evaluation bridge: the FeatureFlagsModule wiring and the
// DD_FLAGGING_EVALUATION_COUNTS_ENABLED killswitch routed through the tracer configuration system.
// (The FinallyAsync hook itself lives in the Datadog.FeatureFlags.OpenFeature package and is
// exercised end-to-end by the OpenFeature integration tests.)

#nullable enable

using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FlagEvalLoggingHookTests
{
    [Fact]
    public void KillswitchDefault_FlagEvalEVPApiIsWired()
    {
        // Default (killswitch unset): the EVP path is on, so the module exposes a FlagEvaluationApi.
        var module = new FeatureFlagsModule(CreateSettings(killswitch: null), new MockRcmSubscriptionManager());
        module.GetFlagEvalEVPApi().Should().NotBeNull("the EVP path defaults to enabled");
    }

    [Fact]
    public void KillswitchTrue_FlagEvalEVPApiIsWired()
    {
        var module = new FeatureFlagsModule(CreateSettings(killswitch: "true"), new MockRcmSubscriptionManager());
        module.GetFlagEvalEVPApi().Should().NotBeNull();
    }

    [Fact]
    public void KillswitchFalse_FlagEvalEVPApiIsNotWired()
    {
        // Killswitch off: the module must NOT create the EVP writer (OTel path is unaffected).
        var module = new FeatureFlagsModule(CreateSettings(killswitch: "false"), new MockRcmSubscriptionManager());
        module.GetFlagEvalEVPApi().Should().BeNull("DD_FLAGGING_EVALUATION_COUNTS_ENABLED=false disables only the EVP path");
    }

    [Fact]
    public void Killswitch_IsReadThroughTheTracerConfigurationSystem()
    {
        // The killswitch must flow through TracerSettings (parsed, telemetry-reported), not a raw
        // environment read. Assert the strongly-typed setting reflects the configured value.
        CreateSettings(killswitch: null).IsFlaggingEvaluationCountsEnabled.Should().BeTrue("defaults to enabled");
        CreateSettings(killswitch: "true").IsFlaggingEvaluationCountsEnabled.Should().BeTrue();
        CreateSettings(killswitch: "false").IsFlaggingEvaluationCountsEnabled.Should().BeFalse();
    }

    [Fact]
    public void FlagEvaluationPath_IsCorrect()
    {
        Datadog.Trace.FeatureFlags.FlagEvaluation.FlagEvaluationApi.FlagEvaluationPath
            .Should().Be("evp_proxy/v2/api/v2/flagevaluation");
    }

    private static TracerSettings CreateSettings(string? killswitch)
    {
        var collection = new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled, "true" }
        };

        if (killswitch is not null)
        {
            collection.Add(ConfigurationKeys.FeatureFlags.FlaggingEvaluationCountsEnabled, killswitch);
        }

        return new TracerSettings(new NameValueConfigurationSource(collection));
    }
}
