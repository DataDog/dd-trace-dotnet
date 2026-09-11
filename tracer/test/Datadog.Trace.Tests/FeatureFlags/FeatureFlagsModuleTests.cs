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

        // Through Create(), because that is what registers the Remote Configuration subscription
        // this test drives.
        var module = CreateModule(settings, rcmManager);

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
                        ?? throw new InvalidOperationException("Create did not register a Remote Configuration subscription.");

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
    public void Create_WithRemoteConfigSource_SubscribesImmediately()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var settings = CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config"));

        // Every tracer subscribes at startup when this source is selected, so the shared parametric
        // suite asserts an apply status without evaluating a flag first. Deferring the subscription
        // to Activate() would make .NET the only tracer that never advertises the capability until
        // the application resolves a flag.
        using var module = CreateModule(settings, rcmManager);

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

        var stopwatch = Stopwatch.StartNew();

        var initialization = module.InitializeAsync(CancellationToken.None);
        initialization.IsCompleted.Should().BeFalse();

        module.ApplyConfiguration(new ServerConfiguration()).Should().BeTrue();

        await initialization;

        stopwatch.Stop();

        // Bounded, because the wait also ends normally at the 60s timeout: without this, the test
        // would pass even if the arriving configuration never released it.
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void HasConfiguration_FollowsWhetherTheEvaluatorCanResolve()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        using var module = CreateModule(CreateSettings(), rcmManager);

        module.HasConfiguration.Should().BeFalse();

        var subscription = rcmManager.LastSubscription
                        ?? throw new InvalidOperationException("Create did not register a Remote Configuration subscription.");

        var configJson = JsonConvert.SerializeObject(new ServerConfiguration());
        var configPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test-config/config");

        subscription.Invoke(
            new Dictionary<string, List<RemoteConfiguration>>
            {
                [RcmProducts.FfeFlags] = [new RemoteConfiguration(configPath, System.Text.Encoding.UTF8.GetBytes(configJson), configJson.Length, new Dictionary<string, string> { { "sha256", "dummy" } }, 1)]
            },
            null);

        module.HasConfiguration.Should().BeTrue();

        // A withdrawal from Remote Configuration, which the provider reads to emit an error status
        // instead of leaving itself reported as ready while resolving nothing.
        subscription.Invoke(
            new Dictionary<string, List<RemoteConfiguration>>(),
            new Dictionary<string, List<RemoteConfigurationPath>>
            {
                [RcmProducts.FfeFlags] = [configPath]
            });

        module.HasConfiguration.Should().BeFalse();
    }

    [Fact]
    public void ApplyConfiguration_WhenTheEventHandlerThrows_ReportsTheConfigurationAsApplied()
    {
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager());

        module.RegisterOnNewConfigEventHandler(() => throw new InvalidOperationException("from application code"));

        // False would tell the agentless source the configuration was not applied, so it would hold
        // its ETag back and re-download the whole payload on every later poll.
        module.ApplyConfiguration(new ServerConfiguration()).Should().BeTrue();
        module.FirstConfigReceived.IsCompleted.Should().BeTrue();
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
    public async Task InitializeAsync_WhenADirectCallerCancels_ReturnsWithoutThrowing()
    {
        var settings = CreateInitializationSettings("60000");
        using var module = CreateModule(settings, new MockRcmSubscriptionManager());
        using var cancellation = new CancellationTokenSource();

        var stopwatch = Stopwatch.StartNew();

        var initialization = module.InitializeAsync(cancellation.Token);
        cancellation.Cancel();

        await initialization;

        stopwatch.Stop();

        // Bounded, because without the cancellation the wait would run to the 60s timeout and still
        // return normally, which would make this test pass for the wrong reason.
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
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
    public void Create_WithTheAgentlessSource_DoesNotStartDeliveryUntilActivated()
    {
        var source = new FakeDeliverySource();
        using var module = CreateModule(CreateAgentlessSettings(), new MockRcmSubscriptionManager(), _ => source);

        // Installing the tracer must not issue a billable request. Only application code that adopts
        // the provider may start the poller.
        source.Started.Should().Be(0);

        module.Activate();
        module.Activate();

        source.Started.Should().Be(1);
        source.Disposed.Should().Be(0);
    }

    [Fact]
    public void Dispose_WithTheAgentlessSource_DisposesTheStartedPoller()
    {
        var source = new FakeDeliverySource();
        var module = CreateModule(CreateAgentlessSettings(), new MockRcmSubscriptionManager(), _ => source);
        module.Activate();

        module.Dispose();

        // A poller that survives disposal keeps issuing billable requests for the rest of the process.
        source.Disposed.Should().Be(1);
    }

    [Fact]
    public void Activate_AfterDispose_DoesNotStartTheAgentlessPoller()
    {
        var source = new FakeDeliverySource();
        var module = CreateModule(CreateAgentlessSettings(), new MockRcmSubscriptionManager(), _ => source);
        module.Dispose();

        module.Activate();

        // Disposal has already run, so nothing would ever dispose a poller started now.
        source.Started.Should().Be(0);
    }

    [Fact]
    public void Activate_WhenCalledConcurrently_StartsExactlyOnePoller()
    {
        var created = 0;
        var source = new FakeDeliverySource();

        using var module = CreateModule(
            CreateAgentlessSettings(),
            new MockRcmSubscriptionManager(),
            _ =>
            {
                Interlocked.Increment(ref created);
                return source;
            });

        // Dedicated threads rather than Parallel.For: blocked pool items in a host that already runs
        // collections in parallel wait on thread-pool injection instead of on each other.
        const int callers = 8;
        using var start = new ManualResetEventSlim(false);
        var threads = new Thread[callers];
        for (var i = 0; i < callers; i++)
        {
            threads[i] = new Thread(() =>
            {
                start.Wait();
                module.Activate();
            })
            { IsBackground = true };

            threads[i].Start();
        }

        start.Set();

        foreach (var thread in threads)
        {
            thread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue("Activate must not block");
        }

        created.Should().Be(1);
        source.Started.Should().Be(1);
    }

    [Fact]
    public void Activate_AfterDispose_DoesNotSubscribeAgain()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var module = CreateModule(CreateSettings(), rcmManager);

        module.Dispose();
        module.Activate();

        // Disposal has already run its unsubscribe, so a subscription registered afterwards would
        // never be removed, leaving a delivery path active for the rest of the process.
        rcmManager.HasAnySubscription.Should().BeFalse();
    }

    [Fact]
    public void Dispose_AfterSubscribing_Unsubscribes()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var module = CreateModule(CreateSettings(), rcmManager);

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
    private static FeatureFlagsModule CreateModule(
        TracerSettings settings,
        IRcmSubscriptionManager rcmSubscriptionManager,
        Func<FeatureFlagsModule, IFeatureFlagsDeliverySource?>? agentlessSourceFactory = null)
        => FeatureFlagsModule.Create(settings, rcmSubscriptionManager, agentlessSourceFactory)
        ?? throw new InvalidOperationException("Feature Flags are enabled, but no module was created.");

    private static TracerSettings CreateSettings(params (string Key, string Value)[] settings)
    {
        var collection = new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "remote_config" },
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

    private static TracerSettings CreateAgentlessSettings()
        => CreateSettings((ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless"));

    // Records what the module does with a delivery source, which the real one cannot report without
    // issuing requests.
    private sealed class FakeDeliverySource : IFeatureFlagsDeliverySource
    {
        private int _started;
        private int _disposed;

        public int Started => Volatile.Read(ref _started);

        public int Disposed => Volatile.Read(ref _disposed);

        public void Start() => Interlocked.Increment(ref _started);

        public void Dispose() => Interlocked.Increment(ref _disposed);
    }
}
