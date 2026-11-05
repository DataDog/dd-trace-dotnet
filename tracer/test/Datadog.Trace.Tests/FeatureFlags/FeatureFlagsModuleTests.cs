// <copyright file="FeatureFlagsModuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.Internal.Sinks;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Formatting;
using FluentAssertions;
using Moq;
using Xunit;

using FeatureFlagsValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FeatureFlagsModuleTests
{
    [Fact]
    public async Task AgentlessModuleUsesSharedDiscoveryForEventDelivery()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        var discovery = new DiscoveryServiceMock();
        var source = new FakeDeliverySource();
        var collection = new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled, "true" },
            { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, "agentless" },
            { ConfigurationKeys.ApiKey, "test-api-key" },
            { ConfigurationKeys.Site, "datadoghq.com" },
        };
        var settings = new TracerSettings(new NameValueConfigurationSource(collection));

        using (var module = FeatureFlagsModule.Create(settings, rcmManager, _ => source, discovery))
        {
            module.Should().NotBeNull();
            source.Started.Should().Be(0);
            rcmManager.HasAnySubscription.Should().BeFalse();
            discovery.Callbacks.Should().BeEmpty("event discovery subscriptions start with the first exposure");
            module!.GetExposureApi().Should().NotBeNull();
            discovery.Callbacks.Should().ContainSingle();

            module.Activate();
            module.Activate();
            source.Started.Should().Be(1);
            discovery.Callbacks.Should().HaveCount(2, "exposures and evaluations own separate transports");
            await module.DisposeAsync();
        }

        discovery.Callbacks.Should().BeEmpty();
        source.Disposed.Should().Be(1);
    }

    [Fact]
    public void DefaultModuleDoesNotCreateExposureTransportUntilFirstUse()
    {
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection()));
        using var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager());
        var transportField = typeof(FeatureFlagsModule).GetField("_evpTransport", BindingFlags.Instance | BindingFlags.NonPublic)!;

        settings.FeatureFlags.Enabled.Should().BeTrue();
        module.Should().NotBeNull();
        transportField.GetValue(module).Should().BeNull("applications that never use exposures need no HTTP client or settings subscription");

        var exposureApi = module!.GetExposureApi();
        exposureApi.Should().NotBeNull();
        transportField.GetValue(module).Should().NotBeNull();
        module.GetExposureApi().Should().BeSameAs(exposureApi);

        module.Dispose();
        transportField.GetValue(module).Should().BeNull();
        module.GetExposureApi().Should().BeNull();
    }

    [Fact]
    public void DisposingUnusedModuleDoesNotCreateExposureTransport()
    {
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection()));
        using var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager());

        module!.Dispose();

        module.GetExposureApi().Should().BeNull();
        typeof(FeatureFlagsModule).GetField("_evpTransport", BindingFlags.Instance | BindingFlags.NonPublic)!
                                  .GetValue(module).Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExposureSettingsUpdateUntilModuleDisposalRemovesSubscriptions(bool activateEvaluationWriter)
    {
        var settings = CreateSettings();
        var subscribers = (ICollection)typeof(TracerSettings.SettingsManager)
                                     .GetField("_subscribers", BindingFlags.Instance | BindingFlags.NonPublic)!
                                     .GetValue(settings.Manager)!;
        var initialSubscriptions = subscribers.Count;
        using var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), discoveryService: FlagEvaluationTestDiscovery.Available(settings))!;
        if (activateEvaluationWriter)
        {
            module.Activate();
            module.EvaluationWriter.Should().NotBeNull();
        }

        var api = module.GetExposureApi()!;
        var transport = (FeatureFlagsEvpTransport)typeof(FeatureFlagsModule)
                                                .GetField("_evpTransport", BindingFlags.Instance | BindingFlags.NonPublic)!
                                                .GetValue(module)!;
        var endpointField = typeof(FeatureFlagsEvpTransport).GetField("_localEndpoint", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var factoryProperty = endpointField.FieldType.GetProperty("Factory")!;
        var contextField = typeof(ExposureApi).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!;

        subscribers.Count.Should().Be(initialSubscriptions + 2 + (activateEvaluationWriter ? 2 : 0), "each track subscribes for its transport and context/admission");
        UpdateSettings("before-disposal", new Uri("http://127.0.0.1:18126/updated/"));

        var factory = (IApiRequestFactory)factoryProperty.GetValue(endpointField.GetValue(transport))!;
        factory.GetEndpoint("evp_proxy/v2/api/v2/exposures").Should().Be(new Uri("http://127.0.0.1:18126/updated/evp_proxy/v2/api/v2/exposures"));
        var context = (Dictionary<string, string>)contextField.GetValue(api)!;
        context["service"].Should().Be("before-disposal");

        await module.DisposeAsync();

        subscribers.Count.Should().Be(initialSubscriptions, "module disposal must remove all event settings subscriptions");
        module.EvaluationWriter.Should().BeNull();
        UpdateSettings("after-disposal", new Uri("http://127.0.0.1:28126/ignored/"));
        factoryProperty.GetValue(endpointField.GetValue(transport)).Should().BeSameAs(factory);
        contextField.GetValue(api).Should().BeSameAs(context);

        void UpdateSettings(string service, Uri agentUri)
        {
            settings.Manager.UpdateManualConfigurationSettings(
                new ManualInstrumentationConfigurationSource(
                    new Dictionary<string, object?>
                    {
                        { TracerSettingKeyConstants.ServiceNameKey, service },
                        { TracerSettingKeyConstants.AgentUriKey, agentUri },
                    },
                    useDefaultSources: false),
                NullConfigurationTelemetry.Instance).Should().BeTrue();
        }
    }

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
    public void UpdateRemoteConfig_WhenAModifiedFileDropsAFlag_TheFlagIsNoLongerFound()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        using var module = CreateModule(CreateSettings(), rcmManager);
        var subscription = rcmManager.LastSubscription
                        ?? throw new InvalidOperationException("Create did not register a Remote Configuration subscription.");
        var configPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test-config/config");

        subscription.Invoke(ConfigUpdate(configPath, "kept-flag", "dropped-flag"), null);
        module.Evaluate("dropped-flag", FeatureFlagsValueType.Boolean, false, "user-1", null)
              .Error.Should().NotBe("FLAG_NOT_FOUND");

        // A modified file keeps its path and never appears in the removed set.
        subscription.Invoke(ConfigUpdate(configPath, "kept-flag"), null);

        module.Evaluate("dropped-flag", FeatureFlagsValueType.Boolean, false, "user-1", null)
              .Error.Should().Be("FLAG_NOT_FOUND");
        module.Evaluate("kept-flag", FeatureFlagsValueType.Boolean, false, "user-1", null)
              .Error.Should().NotBe("FLAG_NOT_FOUND");

        static Dictionary<string, List<RemoteConfiguration>> ConfigUpdate(RemoteConfigurationPath path, params string[] flagKeys)
        {
            var flags = new FlagCollection();
            foreach (var key in flagKeys)
            {
                flags[key] = new Flag { Key = key, Enabled = true, VariationType = FeatureFlagsValueType.Boolean };
            }

            var json = JsonConvert.SerializeObject(new ServerConfiguration { Flags = flags });
            return new Dictionary<string, List<RemoteConfiguration>>
            {
                [RcmProducts.FfeFlags] = [new RemoteConfiguration(path, System.Text.Encoding.UTF8.GetBytes(json), json.Length, new Dictionary<string, string> { { "sha256", "dummy" } }, 1)]
            };
        }
    }

    [Fact]
    public void UpdateRemoteConfig_RecomputesConsentFromActiveFilesAfterUpdatesAndRemovals()
    {
        var rcmManager = new MockRcmSubscriptionManager();
        using var module = CreateModule(CreateSettings(), rcmManager);
        var subscription = rcmManager.LastSubscription
                        ?? throw new InvalidOperationException("Create did not register a Remote Configuration subscription.");
        var firstPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/first/config");
        var secondPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/second/config");

        AssertConsent("false");
        Update(firstPath, false);
        Update(secondPath, true);
        AssertConsent("false");

        // Refreshing either path must not change consent just by changing merge order.
        Update(firstPath, false);
        AssertConsent("false");
        Update(secondPath, true);
        AssertConsent("false");

        // Replacing the nonconsenting file must discard its old consent, not latch false.
        Update(firstPath, true);
        AssertConsent("true");

        Update(firstPath, false);
        AssertConsent("false");
        Remove(firstPath);
        AssertConsent("true");
        Remove(secondPath);
        AssertConsent("false");
        module.Evaluate("flag", FeatureFlagsValueType.String, "fallback", "user", null)
              .Error.Should().Be("PROVIDER_NOT_READY");

        void AssertConsent(string expected)
            => module.Evaluate("flag", FeatureFlagsValueType.String, "fallback", "user", null)
                     .FlagMetadata![FeatureFlagMetadataKeys.ObserveFullEvaluationData].Should().Be(expected);

        void Update(RemoteConfigurationPath path, bool consent)
        {
            var flag = FeatureFlagsHelpers.CreateExposureFlag();
            flag.Allocations![0].DoLog = false;
            var json = JsonConvert.SerializeObject(new ServerConfiguration
            {
                ObserveFullEvaluationData = consent,
                Flags = new FlagCollection { ["flag"] = flag },
            });
            subscription.Invoke(
                new Dictionary<string, List<RemoteConfiguration>>
                {
                    [RcmProducts.FfeFlags] = [new RemoteConfiguration(path, System.Text.Encoding.UTF8.GetBytes(json), json.Length, new Dictionary<string, string> { { "sha256", "dummy" } }, 1)],
                },
                null);
            module.Evaluate("flag", FeatureFlagsValueType.String, "fallback", "user", null)
                  .Value.Should().Be("tracked-value");
        }

        void Remove(RemoteConfigurationPath path)
            => subscription.Invoke([], new Dictionary<string, List<RemoteConfigurationPath>> { [RcmProducts.FfeFlags] = [path] });
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
    public void RegisterOnNewConfigEventHandler_WhenConfigurationAlreadyArrived_InvokesTheHandler()
    {
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager());

        module.ApplyConfiguration(new ServerConfiguration()).Should().BeTrue();

        // The handler registers after the configuration landed, which is the ordering an application
        // gets whenever Remote Configuration delivers before it builds a provider.
        var callbackInvoked = false;
        module.RegisterOnNewConfigEventHandler(() => callbackInvoked = true);

        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void RegisterOnNewConfigEventHandler_WhenNoConfigurationYet_DoesNotInvokeTheHandler()
    {
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager());

        var callbackInvoked = false;
        module.RegisterOnNewConfigEventHandler(() => callbackInvoked = true);

        callbackInvoked.Should().BeFalse();
    }

    [Fact]
    public void RegisterOnNewConfigEventHandler_WhenTheHandlerThrowsOnReplay_DoesNotPropagate()
    {
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager());

        module.ApplyConfiguration(new ServerConfiguration()).Should().BeTrue();

        Action register = () => module.RegisterOnNewConfigEventHandler(() => throw new InvalidOperationException("from application code"));

        register.Should().NotThrow();
    }

    [Fact]
    public void RegisterOnNewConfigEventHandler_AfterAConfigurationWasDelivered_DoesNotDeliverItAgain()
    {
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager());

        var invocations = 0;
        Action handler = () => invocations++;
        module.RegisterOnNewConfigEventHandler(handler);

        module.ApplyConfiguration(new ServerConfiguration()).Should().BeTrue();

        // The handler already has this configuration from the apply, so registering the same handler
        // again must not replay it. Each handler gets each configuration exactly once.
        module.RegisterOnNewConfigEventHandler(handler);

        invocations.Should().Be(1);
    }

    [Fact]
    public async Task RegisterOnNewConfigEventHandler_WhenRegistrationRacesWithApply_DeliversEachConfigurationOnce()
    {
        // Registration and delivery both look at the handler and the held configuration. One pass
        // rarely interleaves them, so repeat: an interleaving that hands the same configuration to
        // both paths invokes the handler twice and fails here.
        for (var i = 0; i < 200; i++)
        {
            using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager());

            var invocations = 0;
            using var start = new ManualResetEventSlim(false);

            var register = Task.Run(() =>
            {
                start.Wait();
                module.RegisterOnNewConfigEventHandler(() => Interlocked.Increment(ref invocations));
            });

            var apply = Task.Run(() =>
            {
                start.Wait();
                module.ApplyConfiguration(new ServerConfiguration());
            });

            start.Set();
            await Task.WhenAll(register, apply);

            invocations.Should().BeLessOrEqualTo(1, "iteration {0} delivered one configuration more than once", i);
        }
    }

    [Theory]
    [InlineData(FeatureFlagsValueType.String, "fallback")]
    [InlineData(FeatureFlagsValueType.Boolean, true)]
    [InlineData(FeatureFlagsValueType.Integer, 42)]
    [InlineData(FeatureFlagsValueType.Numeric, 1.5)]
    public void Evaluate_WhenNoConfigurationYet_ReturnsTheDefaultValue(FeatureFlagsValueType type, object defaultValue)
    {
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager());

        var result = module.Evaluate("test-flag", type, defaultValue, "user-1", null);

        result.Error.Should().Be("PROVIDER_NOT_READY");
        result.Value.Should().Be(defaultValue);
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

    [Fact]
    public void Evaluate_LogsTheRawInputsAndOutputsOfTheResolution()
    {
        var logger = CreateDebugLogger();
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);
        module.ApplyConfiguration(CreatePlanFlagConfiguration()).Should().BeTrue();

        var result = module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user-42", new Dictionary<string, object?> { ["plan"] = "premium", ["seats"] = 3L });

        result.Value.Should().Be(true);
        var line = EvaluationLine(logger);
        line.Should().Contain("plan-flag")
            .And.Contain("true", "the resolved value is logged")
            .And.Contain("false", "the default value is logged")
            .And.Contain("on")
            .And.Contain(EvaluationReason.TargetingMatch)
            .And.Contain("\"user-42\"")
            .And.Contain("{\"plan\":\"premium\",\"seats\":3}")
            .And.Contain("2026-10-20T00:00:00Z");
        line.OfType<string>().Should().Contain(v => v.Contains("plan-allocation"), "the flag metadata is logged");
    }

    [Fact]
    public void Evaluate_BeforeConfiguration_LogsTheDefaultAndTheContext()
    {
        var logger = CreateDebugLogger();
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);

        var result = module.Evaluate("plan-flag", FeatureFlagsValueType.String, "fallback", "user-42", new Dictionary<string, object?> { ["plan"] = "premium" });

        result.Error.Should().Be("PROVIDER_NOT_READY");
        EvaluationLine(logger).Should().Contain("PROVIDER_NOT_READY")
                              .And.Contain("\"fallback\"")
                              .And.Contain("\"user-42\"")
                              .And.Contain("{\"plan\":\"premium\"}")
                              .And.Contain("none (no configuration is applied)");
    }

    [Fact]
    public void Evaluate_WhenAContextValueCannotBeFormatted_ReturnsTheSameResult()
    {
        var logger = CreateDebugLogger();
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);
        module.ApplyConfiguration(CreatePlanFlagConfiguration()).Should().BeTrue();

        var result = module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user-42", new Dictionary<string, object?> { ["plan"] = "premium", ["broken"] = new UnserializableAttribute() });

        result.Value.Should().Be(true);
        result.Error.Should().BeNull();
        EvaluationLine(logger).OfType<string>().Should().Contain(v => v.Contains("\"plan\":\"premium\"") && v.Contains($"<not formatted: {typeof(UnserializableAttribute).FullName}>"));
    }

    [Fact]
    public void Evaluate_WhenTheLoggerThrows_ReturnsTheSameResult()
    {
        var logger = CreateDebugLogger();
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);
        module.ApplyConfiguration(CreatePlanFlagConfiguration()).Should().BeTrue();
        logger.Setup(l => l.Debug(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<int>(), It.IsAny<string>()))
              .Throws(new InvalidOperationException("logger failed"));

        var result = module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user-42", new Dictionary<string, object?> { ["plan"] = "premium" });

        result.Value.Should().Be(true);
        result.Variant.Should().Be("on");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Evaluate_WithDebugDisabled_DoesNotFormatTheContext(bool hasConfiguration)
    {
        var logger = new Mock<IDatadogLogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);
        if (hasConfiguration)
        {
            module.ApplyConfiguration(CreatePlanFlagConfiguration()).Should().BeTrue();
        }

        var counting = new CountingAttribute();

        module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user-42", new Dictionary<string, object?> { ["plan"] = "premium", ["counted"] = counting });
        module.Evaluate("missing-flag", FeatureFlagsValueType.Boolean, false, "user-42", new Dictionary<string, object?> { ["counted"] = counting });

        // A missing flag never reads attributes during evaluation, so enumeration here would only
        // come from formatting the diagnostic, even if the formatter avoids property getters.
        var attributes = new Mock<IDictionary<string, object?>>();
        module.Evaluate("missing-flag", FeatureFlagsValueType.Boolean, false, "user-42", attributes.Object);

        attributes.Verify(a => a.GetEnumerator(), Times.Never);

        counting.Reads.Should().Be(0);
        logger.Invocations.Should().NotContain(i => i.Arguments.OfType<string>().Any(a => a.Contains("Evaluator resolved")));
    }

    [Fact]
    public async Task ApplyConfiguration_WhenUpdatesRace_DoesNotReportRecoveryFromWithdrawal()
    {
        for (var i = 0; i < 100; i++)
        {
            var logger = CreateDebugLogger();
            using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);
            using var start = new ManualResetEventSlim(false);
            var first = Task.Run(Apply);
            var second = Task.Run(Apply);

            start.Set();
            (await Task.WhenAll(first, second)).Should().OnlyContain(applied => applied);

            module.FirstConfigReceived.IsCompleted.Should().BeTrue();
            LoggedTemplates(logger).Should().NotContain(t => t.Contains("available again after it was removed"));

            bool Apply()
            {
                start.Wait();
                return module.ApplyConfiguration(new ServerConfiguration());
            }
        }
    }

    [Fact]
    public void ApplyRemoteConfiguration_ReportsUsableAndRejectedFlags()
    {
        var logger = CreateDebugLogger();
        var rcmManager = new MockRcmSubscriptionManager();
        using var module = CreateModule(CreateSettings(), rcmManager, logger: logger.Object);
        var subscription = rcmManager.LastSubscription
                        ?? throw new InvalidOperationException("Create did not register a Remote Configuration subscription.");
        var configPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test-config/config");
        // Written as valid, so it is the parser that rejects the flag with no splits.
        var json = JsonConvert.SerializeObject(new ServerConfiguration
        {
            CreatedAt = "2026-10-20T00:00:00Z",
            Environment = new Datadog.Trace.FeatureFlags.Rcm.Model.Environment { Name = "staging" },
            Flags = new FlagCollection
            {
                ["valid-flag"] = new Flag { Key = "valid-flag", Enabled = true, VariationType = FeatureFlagsValueType.Boolean },
                ["broken-flag"] = new Flag { Key = "broken-flag", Enabled = true, VariationType = FeatureFlagsValueType.Boolean, Allocations = [new Allocation { Key = "a1" }] },
            }
        });

        subscription.Invoke(
            new Dictionary<string, List<RemoteConfiguration>>
            {
                [RcmProducts.FfeFlags] = [new RemoteConfiguration(configPath, System.Text.Encoding.UTF8.GetBytes(json), json.Length, new Dictionary<string, string> { { "sha256", "dummy" } }, 7)]
            },
            null);

        ArgumentsOf(logger, "received (version").Should().Equal(configPath.Path, 7L, (long)json.Length);
        ArgumentsOf(logger, "applied successfully").Should().Equal(1, 1, "staging", "2026-10-20T00:00:00Z", configPath.Path);
        ArgumentsOf(logger, "were rejected").Should().Equal(1, "remote_config", "broken-flag");
        module.Evaluate("broken-flag", FeatureFlagsValueType.Boolean, false, "user-1", null).Error.Should().Be("PARSE_ERROR");
    }

    [Fact]
    public async Task ApplyConfiguration_AfterTheInitializationTimeout_ReportsTheRecovery()
    {
        var logger = CreateDebugLogger();
        using var module = CreateModule(CreateInitializationSettings("1"), new MockRcmSubscriptionManager(), logger: logger.Object);
        await module.InitializeAsync(CancellationToken.None);

        module.ApplyConfiguration(CreatePlanFlagConfiguration()).Should().BeTrue();

        logger.Invocations.Should().Contain(i => i.Method.Name == nameof(IDatadogLogger.Information)
                                              && i.Arguments.OfType<string>().Any(a => a.Contains("after the initialization timeout")));
    }

    [Fact]
    public void Dispose_ReportsShutdownCompletion()
    {
        var logger = CreateDebugLogger();
        var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);

        module.Dispose();

        LoggedTemplates(logger).Should().Contain(t => t.Contains("shutdown complete"));
    }

    [Fact]
    public void Activate_WithDebugLogging_WarnsOncePerProcessThatLogsIncludeRawEvaluationData()
    {
        FeatureFlagsModule.ResetRawEvaluationDataWarning();
        var logger = CreateDebugLogger();
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);
        RawEvaluationDataWarnings(logger).Should().Be(0, "Feature Flags are enabled by default, so an application that never initializes a provider is not warned");

        module.Activate();
        RawEvaluationDataWarnings(logger).Should().Be(1, "the warning is written when the application initializes the provider, before any evaluation");
        module.ApplyConfiguration(CreatePlanFlagConfiguration()).Should().BeTrue();

        module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user-42", null);
        module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user-42", null);
        var secondLogger = CreateDebugLogger();
        using var secondModule = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: secondLogger.Object);
        secondModule.Activate();

        RawEvaluationDataWarnings(logger).Should().Be(1);
        RawEvaluationDataWarnings(secondLogger).Should().Be(0, "a provider initialized later in the same process does not warn again");
    }

    [Fact]
    public void Evaluate_WithDebugDisabled_DoesNotWarnThatLogsIncludeRawEvaluationData()
    {
        FeatureFlagsModule.ResetRawEvaluationDataWarning();
        var logger = new Mock<IDatadogLogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);
        module.ApplyConfiguration(CreatePlanFlagConfiguration()).Should().BeTrue();

        module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user-42", null);

        RawEvaluationDataWarnings(logger).Should().Be(0);
    }

    [Fact]
    public void Evaluate_WhenDebugLoggingIsEnabledAfterStartup_WarnsOnceBeforeTheFirstEvaluationLine()
    {
        FeatureFlagsModule.ResetRawEvaluationDataWarning();
        var logger = new Mock<IDatadogLogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(false);
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger.Object);
        module.ApplyConfiguration(CreatePlanFlagConfiguration()).Should().BeTrue();

        // Switched on at runtime, as a tracer flare does.
        logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user-42", null);
        module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user-42", null);

        RawEvaluationDataWarnings(logger).Should().Be(1);
        var templates = LoggedTemplates(logger);
        templates.FindIndex(t => t.Contains("include raw evaluation data"))
                 .Should().BeLessThan(templates.FindIndex(t => t.Contains("Evaluator resolved")));
    }

    [Theory]
    [InlineData(FeatureFlagsValueType.Boolean, true, false, "true", "false")]
    [InlineData(FeatureFlagsValueType.Integer, 42, -1, "42", "-1")]
    [InlineData(FeatureFlagsValueType.Numeric, 12.5, -1.5, "12.5", "-1.5")]
    [InlineData(FeatureFlagsValueType.String, "resolved", "fallback", "\"resolved\"", "\"fallback\"")]
    [InlineData(FeatureFlagsValueType.Json, null, null, "", "")]
    public void EvaluationDiagnosticsPreserveRawValues(FeatureFlagsValueType type, object? value, object? fallback, string expectedValue, string expectedDefault)
    {
        var large = new string('x', 20000) + "END-MARKER";
        if (type == FeatureFlagsValueType.Json)
        {
            value = new Dictionary<string, object?> { ["nested"] = new object[] { large, 42 } };
            fallback = new Dictionary<string, object?> { ["fallback"] = large };
            expectedValue = "{\"nested\":[\"" + large + "\",42]}";
            expectedDefault = "{\"fallback\":\"" + large + "\"}";
        }

        var lines = new List<string>();
        var sink = new Mock<ILogEventSink>();
        sink.Setup(x => x.Emit(It.IsAny<LogEvent>())).Callback<LogEvent>(e => lines.Add(e.RenderMessage()));
        using var serilog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink.Object).CreateLogger();
        var logger = new DatadogSerilogLogger(serilog, new NullLogRateLimiter(), null);
        using var module = CreateModule(CreateSettings(), new MockRcmSubscriptionManager(), logger: logger);
        var config = CreatePlanFlagConfiguration();
        config.Flags!["plan-flag"].VariationType = type;
        config.Flags["plan-flag"].Variations!["on"].Value = value;
        module.ApplyConfiguration(config).Should().BeTrue();
        var attributes = new Dictionary<string, object?> { ["plan"] = "premium", ["nested"] = new object[] { large } };

        module.Evaluate("plan-flag", type, fallback, "user-42", attributes).Value.Should().BeEquivalentTo(value);
        var line = lines.Single(x => x.Contains("Evaluator resolved"));
        line.Should().Contain(large).And.Contain("user-42").And.Contain("variant=\"on\"").And.Contain("reason=TargetingMatch");
        line.Should().Contain("metadata=").And.Contain("plan-allocation").And.Contain("before provider conversion");
        line.Should().Contain("value=\"" + expectedValue.Replace("\"", "\\\"") + "\"");
        line.Should().Contain("default=\"" + expectedDefault.Replace("\"", "\\\"") + "\"");
        lines.Clear();
        module.Evaluate("missing", type, fallback, "user-42", attributes).Value.Should().BeEquivalentTo(fallback);
        lines.Single(x => x.Contains("Evaluator resolved")).Should().Contain("FLAG_NOT_FOUND").And.Contain(large);
        lines.Clear();
        var wrongType = type == FeatureFlagsValueType.Boolean ? FeatureFlagsValueType.String : FeatureFlagsValueType.Boolean;
        module.Evaluate("plan-flag", wrongType, fallback, "user-42", attributes).Error.Should().Be("TYPE_MISMATCH");
        lines.Single(x => x.Contains("Evaluator resolved")).Should().Contain("TYPE_MISMATCH").And.Contain("default=");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteConfigurationDiagnosticsDistinguishEmptyAndRejectedFlags(bool invalid)
    {
        var logger = CreateDebugLogger();
        var manager = new MockRcmSubscriptionManager();
        using var module = CreateModule(CreateSettings(), manager, logger: logger.Object);
        var path = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test/config");
        var json = invalid ? "{\"flags\":{\"broken\":null}}" : "{\"flags\":{}}";
        foreach (var body in new[] { json, "not-json" })
        {
            await manager.LastSubscription!.Invoke(
                new Dictionary<string, List<RemoteConfiguration>>
                {
                    [RcmProducts.FfeFlags] = [new RemoteConfiguration(path, System.Text.Encoding.UTF8.GetBytes(body), body.Length, new Dictionary<string, string>(), 1)],
                },
                null);
        }

        module.HasConfiguration.Should().BeTrue();
        module.Evaluate("broken", FeatureFlagsValueType.Boolean, false, null, null).Error.Should().Be(invalid ? "PARSE_ERROR" : "FLAG_NOT_FOUND");
        ArgumentsOf(logger, "applied successfully").Take(2).Should().Equal(0, invalid ? 1 : 0);
        LoggedTemplates(logger).Should().Contain(x => x.Contains("Error while updating FFE RCM config"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RealLoggerFailuresPreserveFeatureFlagsLifecycle(bool agentless, bool throwFromSink)
    {
        var lines = new ConcurrentQueue<string>();
        var sink = new Mock<ILogEventSink>();
        sink.Setup(x => x.Emit(It.IsAny<LogEvent>())).Callback<LogEvent>(e =>
        {
            lines.Enqueue(e.RenderMessage());
            if (throwFromSink)
            {
                throw new InvalidOperationException("sink failed");
            }
        });
        var formatter = new Mock<ITextFormatter>();
        formatter.Setup(x => x.Format(It.IsAny<LogEvent>(), It.IsAny<TextWriter>())).Throws(new InvalidOperationException("deferred formatter failed"));
        var asyncSink = new AsyncTextWriterSink(formatter.Object, new StringWriter(), 100);
        using var serilog = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink.Object).WriteTo.Sink(asyncSink).CreateLogger();
        var logger = new DatadogSerilogLogger(serilog, new NullLogRateLimiter(), null);
        var settings = agentless ? CreateAgentlessSettings() : CreateSettings();
        var manager = new MockRcmSubscriptionManager();
        var delivery = new FakeDeliverySource();
        using var module = CreateModule(settings, manager, _ => delivery, logger);
        var requests = new TestRequestFactory();
        using var transport = new FeatureFlagsEvpTransport(FeatureFlagsSource.RemoteConfig, requests, null, new DiscoveryServiceMock(), logger: logger);
        var exposures = new ExposureApi(settings, transport, shutdownTimeout: TimeSpan.FromSeconds(5), logger: logger);
        typeof(FeatureFlagsModule).GetField("_exposureApi", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(module, exposures);
        var config = CreatePlanFlagConfiguration();
        config.Flags!["plan-flag"].Allocations![0].DoLog = true;
        config.Format = "SERVER";
        config.Environment = new Datadog.Trace.FeatureFlags.Rcm.Model.Environment { Name = "test" };
        var json = JsonConvert.SerializeObject(config);
        var notifications = 0;
        module.RegisterOnNewConfigEventHandler(() => notifications++);
        module.Activate();
        if (agentless)
        {
            AgentlessEndpoint.TryCreate(null, "http://localhost/config", out var endpoint, out _).Should().BeTrue();
            var factory = new TestRequestFactory(uri => new TestApiRequest(uri, responseContent: "{\"data\":{\"type\":\"universal-flag-configuration\",\"attributes\":" + json + "}}"));
            using var source = new AgentlessConfigurationSource(endpoint!, factory, TimeSpan.FromSeconds(30), module.ApplyConfiguration, logger: logger);
            await source.PollAsync();
        }
        else
        {
            var path = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test/config");
            await manager.LastSubscription!.Invoke(
                new Dictionary<string, List<RemoteConfiguration>>
                {
                    [RcmProducts.FfeFlags] = [new RemoteConfiguration(path, System.Text.Encoding.UTF8.GetBytes(json), json.Length, new Dictionary<string, string>(), 1)],
                },
                null);
        }

        await module.InitializeAsync(CancellationToken.None);
        notifications.Should().Be(1);
        module.FirstConfigReceived.IsCompleted.Should().BeTrue();
        module.Evaluate("plan-flag", FeatureFlagsValueType.Boolean, false, "user", new Dictionary<string, object?> { ["plan"] = "premium", ["broken"] = new UnserializableAttribute() }).Value.Should().Be(true);
        // Dispose returns before an agentless evaluation writer finishes closing.
        await module.DisposeAsync();
        requests.RequestsSent.Should().ContainSingle();
        manager.HasAnySubscription.Should().BeFalse();
        delivery.Disposed.Should().Be(agentless ? 1 : 0);
        lines.Should().Contain(x => x.Contains("<not formatted: "));
        lines.Should().Contain(x => x.Contains("shutdown complete"));
        await asyncSink.FlushAsync();
        formatter.Verify(x => x.Format(It.IsAny<LogEvent>(), It.IsAny<TextWriter>()), Times.AtLeastOnce);
    }

    // Creates the module for settings that enable Feature Flags, and returns it as non-nullable so
    // tests can use it directly. Throwing rather than asserting keeps the compiler's nullable analysis
    // satisfied without a null-forgiving operator.
    private static FeatureFlagsModule CreateModule(
        TracerSettings settings,
        IRcmSubscriptionManager rcmSubscriptionManager,
        Func<FeatureFlagsModule, IFeatureFlagsDeliverySource?>? agentlessSourceFactory = null,
        IDatadogLogger? logger = null)
        => FeatureFlagsModule.Create(settings, rcmSubscriptionManager, agentlessSourceFactory, logger: logger)
        ?? throw new InvalidOperationException("Feature Flags are enabled, but no module was created.");

    private static Mock<IDatadogLogger> CreateDebugLogger()
    {
        var logger = new Mock<IDatadogLogger>();
        logger.Setup(l => l.IsEnabled(It.IsAny<LogEventLevel>())).Returns(true);
        return logger;
    }

    private static List<string> LoggedTemplates(Mock<IDatadogLogger> logger)
        => logger.Invocations
                 .SelectMany(i => i.Method.GetParameters().Zip(i.Arguments, (parameter, argument) => (parameter.Name, argument)))
                 .Where(p => p.Name == "messageTemplate")
                 .Select(p => (string)p.argument!)
                 .ToList();

    // The values handed to the single log call whose message contains the fragment.
    private static List<object?> ArgumentsOf(Mock<IDatadogLogger> logger, string templateFragment)
        => logger.Invocations
                 .Select(i => i.Method.GetParameters().Zip(i.Arguments, (parameter, argument) => (parameter.Name, argument)).ToList())
                 .Where(call => call.Any(p => p.Name == "messageTemplate" && ((string)p.argument!).Contains(templateFragment)))
                 .Select(call => call.Where(p => p.Name is not ("sourceLine" or "sourceFile" or "messageTemplate"))
                                     .SelectMany(p => p.argument is object?[] args ? args : [p.argument])
                                     .ToList())
                 .Should().ContainSingle().Subject;

    private static List<object?> EvaluationLine(Mock<IDatadogLogger> logger) => ArgumentsOf(logger, "Evaluator resolved");

    private static int RawEvaluationDataWarnings(Mock<IDatadogLogger> logger)
        => logger.Invocations.Count(i => i.Method.Name == nameof(IDatadogLogger.Warning)
                                      && i.Arguments.OfType<string>().Any(a => a.Contains("include raw evaluation data")));

    private static ServerConfiguration CreatePlanFlagConfiguration()
    {
        var conditions = new List<ConditionConfiguration>
        {
            new ConditionConfiguration { Operator = ConditionOperator.ONE_OF, Attribute = "plan", Value = new[] { "premium" } },
        };
        var allocation = new Allocation
        {
            Key = "plan-allocation",
            Rules = [new Rule(conditions)],
            Splits = [new Split { Shards = [], VariationKey = "on" }],
            DoLog = false,
        };

        return new ServerConfiguration
        {
            CreatedAt = "2026-10-20T00:00:00Z",
            Flags = new FlagCollection
            {
                ["plan-flag"] = new Flag
                {
                    Key = "plan-flag",
                    Enabled = true,
                    VariationType = FeatureFlagsValueType.Boolean,
                    Variations = new Dictionary<string, Variant> { ["on"] = new Variant { Key = "on", Value = true } },
                    Allocations = [allocation],
                }
            }
        };
    }

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

    // An attribute that cannot be serialized, as a customer object with a failing getter would be.
    private sealed class UnserializableAttribute
    {
        public string Value => throw new InvalidOperationException("getter failed");
    }

    // Counts every read of its value, which serializing it for a log line would do.
    private sealed class CountingAttribute
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public string Value
        {
            get
            {
                Interlocked.Increment(ref _reads);
                return "counted";
            }
        }
    }

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
