// <copyright file="FeatureFlagsModuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

using FeatureFlagsValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagsModuleTests
{
    [Fact]
    public void UpdateRemoteConfig_WithEmptyList_InvokesCallbackAndReturnsProviderNotReady()
    {
        // Arrange
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings();
        var module = new FeatureFlagsModule(settings, rcmManager);

        var callbackInvoked = false;
        module.RegisterOnNewConfigEventHandler(() => callbackInvoked = true);

        // First, send a valid config so evaluator is created
        var configJson = JsonConvert.SerializeObject(new ServerConfiguration
        {
            Flags = new Dictionary<string, Flag>
            {
                ["test-flag"] = new Flag { Key = "test-flag", Enabled = true, VariationType = FeatureFlagsValueType.Boolean }
            }
        });
        var configPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test-config/config");

        rcmManager.LastSubscription!.Invoke(
            new Dictionary<string, List<RemoteConfiguration>>
            {
                [RcmProducts.FfeFlags] = [new RemoteConfiguration(configPath, System.Text.Encoding.UTF8.GetBytes(configJson), configJson.Length, new Dictionary<string, string> { { "sha256", "dummy" } }, 1)]
            },
            null);

        // Verify evaluator is working (not PROVIDER_NOT_READY)
        var initialResult = module.Evaluate("test-flag", FeatureFlagsValueType.Boolean, false, "user-1", null);
        initialResult.Error.Should().NotBe("PROVIDER_NOT_READY");
        callbackInvoked.Should().BeTrue("callback should be invoked when config is added");

        // Reset for the RC-reset test
        callbackInvoked = false;

        // Act: Remove the config (RC reset)
        rcmManager.LastSubscription!.Invoke(
            new Dictionary<string, List<RemoteConfiguration>>(),
            new Dictionary<string, List<RemoteConfigurationPath>>
            {
                [RcmProducts.FfeFlags] = [configPath]
            });

        // Assert
        callbackInvoked.Should().BeTrue("callback should be invoked when config is removed");

        var result = module.Evaluate("test-flag", FeatureFlagsValueType.Boolean, false, "user-1", null);
        result.Error.Should().Be("PROVIDER_NOT_READY");
        result.Reason.Should().Be(EvaluationReason.Error);
    }

    [Fact]
    public void UpdateRemoteConfig_ReplacesActiveAndRejectedFlagsForExistingPath()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var module = new FeatureFlagsModule(CreateSettings(), rcmManager);
        var configPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test-config/config");

        const string FirstConfig =
            """
            {
              "flags": {
                "rejected-then-active": {
                  "key": "rejected-then-active",
                  "enabled": true,
                  "variationType": "STRING",
                  "variations": { "on": { "key": "on", "value": "on" } },
                  "allocations": "invalid"
                },
                "removed-active": {
                  "key": "removed-active",
                  "enabled": false,
                  "variationType": "STRING",
                  "variations": {}
                }
              }
            }
            """;
        ApplyConfig(version: 1, FirstConfig);

        module.Evaluate("rejected-then-active", FeatureFlagsValueType.String, "fallback", "user-1", null).Error.Should().Be("PARSE_ERROR");
        module.Evaluate("removed-active", FeatureFlagsValueType.String, "fallback", "user-1", null).Reason.Should().Be(EvaluationReason.Disabled);

        const string SecondConfig =
            """
            {
              "flags": {
                "rejected-then-active": {
                  "key": "rejected-then-active",
                  "enabled": false,
                  "variationType": "STRING",
                  "variations": {}
                },
                "newly-rejected": {
                  "key": "newly-rejected",
                  "enabled": true,
                  "variationType": "STRING",
                  "variations": { "on": { "key": "on", "value": "on" } },
                  "allocations": "invalid"
                }
              }
            }
            """;
        ApplyConfig(version: 2, SecondConfig);

        module.Evaluate("rejected-then-active", FeatureFlagsValueType.String, "fallback", "user-1", null).Reason.Should().Be(EvaluationReason.Disabled);
        module.Evaluate("newly-rejected", FeatureFlagsValueType.String, "fallback", "user-1", null).Error.Should().Be("PARSE_ERROR");
        module.Evaluate("removed-active", FeatureFlagsValueType.String, "fallback", "user-1", null).Error.Should().Be("FLAG_NOT_FOUND");

        void ApplyConfig(long version, string configJson)
        {
            var contents = Encoding.UTF8.GetBytes(configJson);
            rcmManager.LastSubscription!.Invoke(
                new Dictionary<string, List<RemoteConfiguration>>
                {
                    [RcmProducts.FfeFlags] = [new RemoteConfiguration(configPath, contents, contents.Length, new Dictionary<string, string> { { "sha256", "dummy" } }, version)]
                },
                null);
        }
    }

    private static TracerSettings CreateSettings()
    {
        var collection = new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled, "true" }
        };

        return new TracerSettings(new NameValueConfigurationSource(collection));
    }
}
