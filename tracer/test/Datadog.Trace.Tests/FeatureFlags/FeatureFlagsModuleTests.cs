// <copyright file="FeatureFlagsModuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
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
    public void Create_WhenDisabled_ReturnsNull()
    {
        var settings = CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsEnabled, "false"));

        FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager()).Should().BeNull();
    }

    [Fact]
    public void Create_ByDefault_SelectsAgentlessWithoutRemoteConfiguration()
    {
        var rcmManager = new MockRcmSubscriptionManager();

        using var module = FeatureFlagsModule.Create(CreateSettings(), rcmManager);

        module.Should().NotBeNull();
        module!.Settings.Source.Should().Be(FeatureFlagsSource.Agentless);

        // Subscribing would advertise the FFE capability and start a billed Remote Configuration
        // subscription, which must not happen for a source that never uses it.
        rcmManager.HasAnySubscription.Should().BeFalse();
        rcmManager.ProductKeys.Should().NotContain(RcmProducts.FfeFlags);
        rcmManager.Capabilities.Should().NotContainKey(RcmCapabilitiesIndices.FfeFlagConfigurationRules);
    }

    [Fact]
    public void Create_WithRemoteConfigSource_SubscribesAndAdvertisesCapability()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config"));

        using var module = FeatureFlagsModule.Create(settings, rcmManager);

        module.Should().NotBeNull();
        module!.Settings.Source.Should().Be(FeatureFlagsSource.RemoteConfig);
        rcmManager.ProductKeys.Should().Contain(RcmProducts.FfeFlags);
        rcmManager.Capabilities.Should().ContainKey(RcmCapabilitiesIndices.FfeFlagConfigurationRules)
                  .WhoseValue.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WithRemoteConfigSource_Unsubscribes()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config"));

        var module = FeatureFlagsModule.Create(settings, rcmManager);
        rcmManager.HasAnySubscription.Should().BeTrue();

        module!.Dispose();

        rcmManager.HasAnySubscription.Should().BeFalse();
    }

    [Fact]
    public void Activate_IsIdempotent()
    {
        using var module = FeatureFlagsModule.Create(CreateSettings(), new MockRcmSubscriptionManager());

        module!.Activate();
        module.Activate();

        // Nothing is delivered yet in agentless mode, so the provider cannot be ready.
        module.FirstConfigReceived.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void FirstConfigReceived_CompletesWhenConfigurationApplied()
    {
        using var module = FeatureFlagsModule.Create(CreateSettings(), new MockRcmSubscriptionManager());

        module!.FirstConfigReceived.IsCompleted.Should().BeFalse();

        module.ApplyConfiguration(new ServerConfiguration
        {
            Flags = new Dictionary<string, Flag>
            {
                ["test-flag"] = new Flag { Key = "test-flag", Enabled = true, VariationType = FeatureFlagsValueType.Boolean }
            }
        }).Should().BeTrue();

        module.FirstConfigReceived.IsCompleted.Should().BeTrue();
        module.Evaluate("test-flag", FeatureFlagsValueType.Boolean, false, "user-1", null)
              .Error.Should().NotBe("PROVIDER_NOT_READY");
    }

    [Fact]
    public async Task InitializeAsync_WhenConfigurationAlreadyApplied_ReturnsImmediately()
    {
        var settings = CreateInitializationSettings("60000");
        using var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager());

        module!.ApplyConfiguration(new ServerConfiguration()).Should().BeTrue();

        // Would block for the initialization timeout if the already-received configuration
        // was not honoured.
        await module.InitializeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task InitializeAsync_WhenConfigurationArrivesWhileWaiting_Returns()
    {
        var settings = CreateInitializationSettings("60000");
        using var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager());

        var initialization = module!.InitializeAsync(CancellationToken.None);
        initialization.IsCompleted.Should().BeFalse();

        module.ApplyConfiguration(new ServerConfiguration()).Should().BeTrue();

        await initialization;
    }

    [Fact]
    public async Task InitializeAsync_OnTimeout_ReturnsWithoutThrowing()
    {
        var settings = CreateInitializationSettings("1");
        using var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager());

        // Initialization commonly runs at application startup, where throwing would take the
        // application down, so a missing configuration is only reported through evaluations.
        await module!.InitializeAsync(CancellationToken.None);

        module.FirstConfigReceived.IsCompleted.Should().BeFalse();
        module.Evaluate("test-flag", FeatureFlagsValueType.Boolean, false, "user-1", null)
              .Error.Should().Be("PROVIDER_NOT_READY");
    }

    [Fact]
    public async Task InitializeAsync_OnCancellation_ReturnsWithoutThrowing()
    {
        var settings = CreateInitializationSettings("60000");
        using var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager());
        using var cancellation = new CancellationTokenSource();

        var initialization = module!.InitializeAsync(cancellation.Token);
        cancellation.Cancel();

        await initialization;
    }

    [Fact]
    public void UpdateRemoteConfig_WithEmptyList_InvokesCallbackAndReturnsProviderNotReady()
    {
        // Arrange
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config"));
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

    // Remote Configuration is picked so that activation cannot issue an agentless request: these
    // tests cover the wait, not the delivery source.
    private static TracerSettings CreateInitializationSettings(string timeoutMs)
        => CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config"),
            (ConfigurationKeys.FeatureFlags.FlaggingProviderInitializationTimeoutMs, timeoutMs));

    private static TracerSettings CreateSettings(params (string Key, string Value)[] settings)
    {
        var collection = new NameValueCollection();
        foreach (var (key, value) in settings)
        {
            collection[key] = value;
        }

        return new TracerSettings(new NameValueConfigurationSource(collection));
    }
}
