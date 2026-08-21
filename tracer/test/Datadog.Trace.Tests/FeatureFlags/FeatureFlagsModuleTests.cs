// <copyright file="FeatureFlagsModuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
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
    public void UpdateRemoteConfig_WithEmptyList_InvokesCallbackAndReturnsProviderNotReady()
    {
        // Arrange
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings();
        var module = new FeatureFlagsModule(settings, rcmManager);
        module.Activate();

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
        var subscription = rcmManager.LastSubscription
                        ?? throw new InvalidOperationException("Activation did not register a Remote Configuration subscription.");

        subscription.Invoke(
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
        subscription.Invoke(
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
    public void Create_WithAgentlessSource_DoesNotSubscribeToRc()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"));

        // CreateModule throws when no module is created, which is the assertion that it was.
        using var module = CreateModule(settings, rcmManager);

        module.Settings.Source.Should().Be(FeatureFlagsSource.Agentless);

        // Subscribing would advertise the FFE capability and start a billed Remote Configuration
        // subscription, which must not happen for a source that never uses it.
        rcmManager.HasAnySubscription.Should().BeFalse();
        rcmManager.ProductKeys.Should().NotContain(RcmProducts.FfeFlags);
    }

    [Fact]
    public void Create_WithRemoteConfigSource_DoesNotSubscribeUntilActivated()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config"));

        using var module = CreateModule(settings, rcmManager);

        // Subscription is deferred to Activate() so merely enabling Feature Flags does not start
        // a billed RC subscription.
        rcmManager.HasAnySubscription.Should().BeFalse();

        module.Activate();

        rcmManager.HasAnySubscription.Should().BeTrue();
        rcmManager.ProductKeys.Should().Contain(RcmProducts.FfeFlags);
    }

    [Fact]
    public void Create_WhenDisabled_ReturnsNull()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsEnabled, "false"));

        var module = FeatureFlagsModule.Create(settings, rcmManager);

        module.Should().BeNull();
    }

    [Fact]
    public async Task InitializeAsync_WhenConfigurationAlreadyApplied_ReturnsImmediately()
    {
        var settings = CreateInitializationSettings("60000");
        using var module = CreateModule(settings, new MockRcmSubscriptionManager());

        module.ApplyConfiguration(new ServerConfiguration
        {
            Flags = new FlagCollection
            {
                ["test-flag"] = new Flag { Key = "test-flag", Enabled = true, VariationType = FeatureFlagsValueType.Boolean }
            }
        }).Should().BeTrue();

        module.FirstConfigReceived.IsCompleted.Should().BeTrue();

        await module.InitializeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task InitializeAsync_WhenConfigurationArrivesWhileWaiting_Returns()
    {
        var settings = CreateInitializationSettings("60000");
        using var module = CreateModule(settings, new MockRcmSubscriptionManager());

        var initialization = module.InitializeAsync(CancellationToken.None);
        initialization.IsCompleted.Should().BeFalse();

        module.ApplyConfiguration(new ServerConfiguration()).Should().BeTrue();

        await initialization;
    }

    [Fact]
    public async Task InitializeAsync_OnTimeout_ReturnsWithoutThrowing()
    {
        var settings = CreateInitializationSettings("1");
        using var module = CreateModule(settings, new MockRcmSubscriptionManager());

        // Delivery has started, so the configuration still arrives after this point and promotes the
        // provider. Only the wait is abandoned, which is why the timeout does not fail initialization.
        await module.InitializeAsync(CancellationToken.None);

        module.FirstConfigReceived.IsCompleted.Should().BeFalse();
        module.Evaluate("test-flag", FeatureFlagsValueType.Boolean, false, "user-1", null)
              .Error.Should().Be("PROVIDER_NOT_READY");
    }

    [Fact]
    public async Task InitializeAsync_OnCancellation_ReturnsWithoutThrowing()
    {
        var settings = CreateInitializationSettings("60000");
        using var module = CreateModule(settings, new MockRcmSubscriptionManager());
        using var cancellation = new CancellationTokenSource();

        var initialization = module.InitializeAsync(cancellation.Token);
        cancellation.Cancel();

        await initialization;
    }

    [Fact]
    public async Task InitializeAsync_WhenAgentlessSourceCannotStart_FailsImmediately()
    {
        // Agentless without an API key cannot start the poller, so no configuration will ever arrive.
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"),
            (ConfigurationKeys.FeatureFlags.FlaggingProviderInitializationTimeoutMs, "60000"));
        using var module = CreateModule(settings, new MockRcmSubscriptionManager());

        var stopwatch = Stopwatch.StartNew();

        // Completing normally would report the provider as ready, while every evaluation would keep
        // returning its default value. Failing is what makes the SDK record an error status instead.
        await module.Invoking(m => m.InitializeAsync(CancellationToken.None))
                    .Should().ThrowAsync<FeatureFlagsDeliveryUnavailableException>();

        stopwatch.Stop();

        // Immediately, rather than after the 60s timeout: the configuration cannot arrive by waiting.
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
        module.FirstConfigReceived.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenRemoteConfigurationIsUnavailable_FailsWithTheReason()
    {
        var settings = CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config"),
            (ConfigurationKeys.Rcm.RemoteConfigurationEnabled, "false"));
        using var module = CreateModule(settings, new MockRcmSubscriptionManager());

        var failure = await module.Invoking(m => m.InitializeAsync(CancellationToken.None))
                                  .Should().ThrowAsync<FeatureFlagsDeliveryUnavailableException>();

        // The message reaches the application through the SDK's error event, so it names the cause
        // without echoing configuration back.
        failure.Which.Message.Should().Contain("Remote Configuration is not available");
    }

    [Fact]
    public void Activate_WhenCalledConcurrently_SubscribesOnce()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        using var module = CreateModule(CreateSettings(), rcmManager);

        // Activation claims its flag and completes its setup atomically, so a second caller cannot
        // subscribe a second time, nor observe the module as activated while setup is still running.
        const int callers = 16;
        using var start = new Barrier(callers);
        Parallel.For(
            0,
            callers,
            _ =>
            {
                start.SignalAndWait();
                module.Activate();
            });

        rcmManager.ProductKeys.Should().ContainSingle().Which.Should().Be(RcmProducts.FfeFlags);
    }

    [Fact]
    public void Activate_AfterDispose_DoesNotSubscribe()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var module = CreateModule(CreateSettings(), rcmManager);

        module.Dispose();
        module.Activate();

        // Disposal has already run its unsubscribe, so a subscription registered afterwards would
        // never be removed, leaving a billed delivery path active for the rest of the process.
        rcmManager.HasAnySubscription.Should().BeFalse();
        rcmManager.ProductKeys.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_AfterSubscribing_Unsubscribes()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var module = CreateModule(CreateSettings(), rcmManager);

        module.Activate();
        rcmManager.HasAnySubscription.Should().BeTrue();

        module.Dispose();

        rcmManager.HasAnySubscription.Should().BeFalse();
    }

    [Fact]
    public void GetExposureApi_AfterDispose_DoesNotCreateOne()
    {
        var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager());

        module.GetExposureApi().Should().NotBeNull();

        module.Dispose();

        // An exposure API created after disposal would keep its send loop running for the rest of
        // the process, because nothing disposes it.
        module.GetExposureApi().Should().BeNull();
    }

    // Creates the module for settings that enable Feature Flags, and returns it as non-nullable so
    // tests can use it directly. Throwing rather than asserting keeps the compiler's nullable analysis
    // satisfied without a null-forgiving operator.
    private static FeatureFlagsModule CreateModule(TracerSettings settings, IRcmSubscriptionManager rcmSubscriptionManager)
        => FeatureFlagsModule.Create(settings, rcmSubscriptionManager)
        ?? throw new InvalidOperationException("Feature Flags are enabled, but no module was created.");

    private static TracerSettings CreateSettings(params (string Key, string Value)[] settings)
    {
        var collection = new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config" },
#pragma warning disable 618 // superseded, but still honoured for existing adopters
            { ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled, "true" },
#pragma warning restore 618
        };

        foreach (var (key, value) in settings)
        {
            collection[key] = value;
        }

        return new TracerSettings(new NameValueConfigurationSource(collection));
    }

    private static TracerSettings CreateInitializationSettings(string timeoutMs)
        => CreateSettings(
            (ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config"),
            (ConfigurationKeys.FeatureFlags.FlaggingProviderInitializationTimeoutMs, timeoutMs));
}
