// <copyright file="FeatureFlagsModuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.Specialized;
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
            Flags = new FlagCollection
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
    public void IsReady_WhenNoEvaluatorInstalled_ReturnsFalse()
    {
        // IsReady() depends only on evaluator presence, not on RC availability flag.
        // When RC is disabled, no evaluator ever arrives, so IsReady() returns false.
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings(remoteConfigurationEnabled: false);
        var module = new FeatureFlagsModule(settings, rcmManager);

        module.IsReady().Should().BeFalse();
    }

    [Fact]
    public void IsReady_WhenEvaluatorInstalledAndRCEnabled_ReturnsTrue()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings(remoteConfigurationEnabled: true);
        var module = new FeatureFlagsModule(settings, rcmManager);

        // Before config arrives, not ready.
        module.IsReady().Should().BeFalse();

        // Push a config so the evaluator is installed.
        var configJson = JsonConvert.SerializeObject(new ServerConfiguration
        {
            Flags = new FlagCollection
            {
                ["flag"] = new Flag { Key = "flag", Enabled = true, VariationType = FeatureFlagsValueType.Boolean }
            }
        });
        var configPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/cfg/config");
        rcmManager.LastSubscription!.Invoke(
            new Dictionary<string, List<RemoteConfiguration>>
            {
                [RcmProducts.FfeFlags] = [new RemoteConfiguration(configPath, System.Text.Encoding.UTF8.GetBytes(configJson), configJson.Length, new Dictionary<string, string> { { "sha256", "dummy" } }, 1)]
            },
            null);

        module.IsReady().Should().BeTrue();
    }

    [Fact]
    public void RegisterOnNewConfigEventHandler_ReplaysFiredWhenEvaluatorAlreadyPresent()
    {
        // Verifies the replay path: if an evaluator is already installed when the handler is
        // registered, the handler is invoked immediately.
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings();
        var module = new FeatureFlagsModule(settings, rcmManager);

        // Install evaluator before registering the handler.
        var configJson = JsonConvert.SerializeObject(new ServerConfiguration
        {
            Flags = new FlagCollection
            {
                ["flag"] = new Flag { Key = "flag", Enabled = true, VariationType = FeatureFlagsValueType.Boolean }
            }
        });
        var configPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/cfg/config");
        rcmManager.LastSubscription!.Invoke(
            new Dictionary<string, List<RemoteConfiguration>>
            {
                [RcmProducts.FfeFlags] = [new RemoteConfiguration(configPath, System.Text.Encoding.UTF8.GetBytes(configJson), configJson.Length, new Dictionary<string, string> { { "sha256", "dummy" } }, 1)]
            },
            null);

        var callbackInvoked = false;
        module.RegisterOnNewConfigEventHandler(() => callbackInvoked = true);

        callbackInvoked.Should().BeTrue("handler should be replayed immediately when evaluator already exists");
    }

    private static TracerSettings CreateSettings(bool remoteConfigurationEnabled = true)
    {
        var collection = new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled, "true" },
            { ConfigurationKeys.Rcm.RemoteConfigurationEnabled, remoteConfigurationEnabled ? "true" : "false" },
        };

        return new TracerSettings(new NameValueConfigurationSource(collection));
    }
}
