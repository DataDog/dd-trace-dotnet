// <copyright file="FeatureFlagsPrivacyConsentTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers;
using Xunit;
using ValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagsPrivacyConsentTests
{
    public static IEnumerable<object?[]> TerminalCases()
    {
        foreach (var consent in new[] { false, true })
        {
            yield return new object?[] { consent, "static", "Static", null };
            yield return new object?[] { consent, "disabled", "Disabled", null };
            yield return new object?[] { consent, "null-allocations", "Default", null };
            yield return new object?[] { consent, "empty-allocations", "Default", null };
            yield return new object?[] { consent, "no-match", "Default", null };
            yield return new object?[] { consent, "missing", "Error", "FLAG_NOT_FOUND" };
            yield return new object?[] { consent, "invalid", "Error", "PARSE_ERROR" };
            yield return new object?[] { consent, "type-mismatch", "Error", "TYPE_MISMATCH" };
            yield return new object?[] { consent, "missing-variant", "Error", "PARSE_ERROR" };
            yield return new object?[] { consent, "format-exception", "Error", "PARSE_ERROR" };
            yield return new object?[] { consent, "general-exception", "Error", "GENERAL" };
            yield return new object?[] { consent, "missing-target", "Error", "TARGETING_KEY_MISSING" };
        }
    }

    [Theory]
    [MemberData(nameof(TerminalCases))]
    public void EveryTerminalPathRetainsEvaluationConsent(bool consent, string scenario, string reason, string? errorCode)
    {
        var config = Configuration(consent);
        var flag = config.Flags!["flag"];
        var key = "flag";
        switch (scenario)
        {
            case "disabled":
                flag.Enabled = false;
                break;
            case "null-allocations":
                flag.Allocations = null;
                break;
            case "empty-allocations":
                flag.Allocations = new List<Allocation>();
                break;
            case "no-match":
                flag.Allocations![0].EndAt = "2000-01-01T00:00:00Z";
                break;
            case "missing":
                key = "missing";
                break;
            case "invalid":
                config.Flags.MarkInvalid("flag");
                break;
            case "type-mismatch":
                flag.VariationType = ValueType.Boolean;
                break;
            case "missing-variant":
                flag.Variations!.Clear();
                break;
            case "format-exception":
                flag.Variations!["tracked"].Value = new CallbackValue(() => throw new FormatException("private-format-error"));
                break;
            case "general-exception":
                flag.Variations!["tracked"].Value = new CallbackValue(() => throw new InvalidOperationException("private-general-error"));
                break;
            case "missing-target":
                flag.Allocations![0].Splits![0].Shards = new List<Shard> { new() { Salt = ".", TotalShards = 100, Ranges = new List<ShardRange> { new() { Start = 0, End = 100 } } } };
                break;
        }

        var result = new FeatureFlagsEvaluator(null, config).Evaluate(key, ValueType.String, "fallback", new EvaluationContext(scenario == "missing-target" ? null : "user"));

        Assert.Equal(reason, result.Reason.ToString());
        Assert.Equal(scenario == "static" ? "tracked-value" : "fallback", result.Value);
        if (errorCode is not null)
        {
            Assert.Equal(errorCode, result.FlagMetadata!["errorCode"]);
        }

        Assert.Equal(consent ? "true" : "false", result.FlagMetadata!["__dd_observe_full_evaluation_data"]);
    }

    [Fact]
    public void MissingConfigurationIsProtectedInBothEvaluatorAndModule()
    {
        using var module = Module();
        var direct = new FeatureFlagsEvaluator(null, null).Evaluate("flag", ValueType.String, "fallback", null);
        var throughModule = module.Evaluate("flag", ValueType.String, "fallback", null, null);
        foreach (var result in new[] { direct, throughModule })
        {
            Assert.Equal("PROVIDER_NOT_READY", result.Error);
            Assert.Equal("fallback", result.Value);
            Assert.Equal("false", result.FlagMetadata!["__dd_observe_full_evaluation_data"]);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ConfigurationSwapDuringEvaluationCannotChangeItsConsent(bool originalConsent, bool throwAfterSwap)
    {
        using var module = Module();
        var original = Configuration(originalConsent);
        var replacement = Configuration(!originalConsent);
        original.Flags!["flag"].Variations!["tracked"].Value = new CallbackValue(() =>
        {
            Assert.True(module.ApplyConfiguration(replacement));
            if (throwAfterSwap)
            {
                throw new InvalidOperationException("private-error-after-swap");
            }
        });
        Assert.True(module.ApplyConfiguration(original));

        var first = module.Evaluate("flag", ValueType.String, "fallback", "user", null);
        var second = module.Evaluate("flag", ValueType.String, "fallback", "user", null);

        Assert.Equal(throwAfterSwap ? "fallback" : "callback-value", first.Value);
        Assert.Equal(originalConsent ? "true" : "false", first.FlagMetadata!["__dd_observe_full_evaluation_data"]);
        Assert.Equal("tracked-value", second.Value);
        Assert.Equal(originalConsent ? "false" : "true", second.FlagMetadata!["__dd_observe_full_evaluation_data"]);
        if (throwAfterSwap)
        {
            Assert.Equal("GENERAL", first.FlagMetadata["errorCode"]);
            Assert.Equal("private-error-after-swap", first.Error);
        }
    }

    [Fact]
    public void MergedFlagsKeepSourceConsentWithoutMutatingTheirOriginalConfigurations()
    {
        var consented = Configuration(true);
        var protectedConfig = Configuration(false);
        protectedConfig.Flags!.Add("protected", protectedConfig.Flags["flag"]);
        protectedConfig.Flags.MarkInvalid("invalid");
        var merged = new ServerConfiguration();
        merged.Merge(protectedConfig);
        merged.Merge(consented);
        var evaluator = new FeatureFlagsEvaluator(null, merged);

        Assert.Equal("true", Evaluate("flag").FlagMetadata!["__dd_observe_full_evaluation_data"]);
        Assert.Equal("false", Evaluate("protected").FlagMetadata!["__dd_observe_full_evaluation_data"]);
        Assert.Equal("false", Evaluate("missing").FlagMetadata!["__dd_observe_full_evaluation_data"]);
        Assert.Equal("false", Evaluate("invalid").FlagMetadata!["__dd_observe_full_evaluation_data"]);
        Assert.Equal("true", new FeatureFlagsEvaluator(null, consented).Evaluate("flag", ValueType.String, "fallback", new EvaluationContext("user")).FlagMetadata!["__dd_observe_full_evaluation_data"]);
        Assert.Equal("false", new FeatureFlagsEvaluator(null, protectedConfig).Evaluate("flag", ValueType.String, "fallback", new EvaluationContext("user")).FlagMetadata!["__dd_observe_full_evaluation_data"]);

        Evaluation Evaluate(string key) => evaluator.Evaluate(key, ValueType.String, "fallback", new EvaluationContext("user"));
    }

    private static ServerConfiguration Configuration(bool consent)
    {
        var flag = FeatureFlagsHelpers.CreateExposureFlag();
        flag.Allocations![0].DoLog = false;
        return new ServerConfiguration
        {
            ObserveFullEvaluationData = consent,
            Flags = new FlagCollection { ["flag"] = flag },
        };
    }

    private static FeatureFlagsModule Module() => FeatureFlagsModule.Create(
        new TracerSettings(new NameValueConfigurationSource(new NameValueCollection { { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config" } })),
        new MockRcmSubscriptionManager())!;

    private sealed class CallbackValue(Action callback)
    {
        public override string ToString()
        {
            callback();
            return "callback-value";
        }
    }
}
