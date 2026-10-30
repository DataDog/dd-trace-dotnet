// <copyright file="FlagEvaluationModuleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

using FeatureFlagsValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

[Collection(nameof(WebRequestCollection))]
public class FlagEvaluationModuleTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task AgentlessActivationAcceptsObservationsBeforeDiscovery()
    {
        var settings = Settings(8126, "agentless");
        var discovery = new FlagEvaluationTestDiscovery();
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), _ => new FakeDeliverySource(), discovery)!;
        try
        {
            module.EvaluationWriter.Should().BeNull("events remain lazy until provider adoption");
            module.Activate();
            module.EvaluationWriter.Should().NotBeNull("Agentless delivery can use direct intake without discovering an Agent");
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("remote_config", "evp_proxy/v2")]
    public async Task DiscoveryStartsOneWriterAfterActivation(string source, string endpoint)
    {
        using var agent = MockTracerAgent.Create(_output);
        var received = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) => received.Enqueue(args.Value);
        var settings = Settings(agent.Port, source);
        var discovery = new FlagEvaluationTestDiscovery();
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), _ => new FakeDeliverySource(), discovery)!;
        try
        {
            module.Activate();
            module.EvaluationWriter.Should().BeNull();
            discovery.Publish(settings.Manager.InitialExporterSettings, endpoint: null);
            module.EvaluationWriter.Should().BeNull("an Agent without EVP support cannot deliver these events");

            discovery.Publish(settings.Manager.InitialExporterSettings, endpoint);
            var writer = module.EvaluationWriter!;
            writer.Should().NotBeNull();
            discovery.Publish(settings.Manager.InitialExporterSettings, endpoint);
            module.EvaluationWriter.Should().BeSameAs(writer);
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await module.FlushAsync();
            received.Should().ContainSingle("only observations admitted after discovery are retained");

            await module.DisposeAsync();
            discovery.Publish(settings.Manager.InitialExporterSettings, endpoint);
            module.EvaluationWriter.Should().BeNull("late discovery must not restart a disposed module");
            writer.TryEnqueue(Observation()).Should().BeFalse();
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Fact]
    public async Task ChangingAgentRequiresDiscoveryForThatEndpointButKeepsWriterContext()
    {
        using var original = MockTracerAgent.Create(_output);
        using var updated = MockTracerAgent.Create(_output);
        var received = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        updated.EventPlatformProxyPayloadReceived += (_, args) => received.Enqueue(args.Value);
        var settings = Settings(original.Port, "remote_config");
        var discovery = FlagEvaluationTestDiscovery.Available(settings);
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), discoveryService: discovery)!;
        try
        {
            module.Activate();
            var writer = module.EvaluationWriter!;
            writer.Should().NotBeNull();
            var updatedExporter = UpdateSettings(settings, updated.Port, "new-env");
            module.EvaluationWriter.Should().BeNull("the original Agent's capabilities do not apply at the new address");
            discovery.Publish(settings.Manager.InitialExporterSettings);
            module.EvaluationWriter.Should().BeNull("an in-flight old discovery response must not reopen admission");
            discovery.Publish(updatedExporter);
            module.EvaluationWriter.Should().BeSameAs(writer, "changing the Agent must not relabel the writer's events");
            discovery.Publish(settings.Manager.InitialExporterSettings);
            module.EvaluationWriter.Should().BeSameAs(writer, "late responses from the old Agent must not invalidate the new Agent's discovery");
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await module.FlushAsync();
            JObject.Parse(received.Single().BodyInJson)["context"]!["env"]!.Value<string>().Should().Be("unknown");

            discovery.Publish(updatedExporter, endpoint: null);
            module.EvaluationWriter.Should().BeNull("withdrawn EVP support closes admission");
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoveryForNewAgentCanArriveBeforeSettingsCallback(bool initiallyAvailable)
    {
        using var original = MockTracerAgent.Create(_output);
        using var updated = MockTracerAgent.Create(_output);
        var received = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        updated.EventPlatformProxyPayloadReceived += (_, args) => received.Enqueue(args.Value);
        var settings = Settings(original.Port, "remote_config");
        var discovery = initiallyAvailable ? FlagEvaluationTestDiscovery.Available(settings) : new FlagEvaluationTestDiscovery();
        // Shared discovery subscribes before this module. Its poll can finish between
        // its settings callback and this module's callback for the same update.
        using var discoverySettings = settings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                discovery.Publish(exporter);
            }
        });
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), discoveryService: discovery)!;
        try
        {
            module.Activate();
            var originalWriter = module.EvaluationWriter;
            UpdateSettings(settings, updated.Port, "new-env");
            var writer = module.EvaluationWriter!;
            writer.Should().NotBeNull("discovery must work regardless of the order its response and settings callbacks arrive");
            if (initiallyAvailable)
            {
                writer.Should().BeSameAs(originalWriter);
            }

            writer.TryEnqueue(Observation()).Should().BeTrue();
            await module.FlushAsync();
            JObject.Parse(received.Single().BodyInJson)["context"]!["env"]!.Value<string>().Should().Be(initiallyAvailable ? "unknown" : "new-env");
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Fact]
    public async Task ChangingAgentBackDoesNotReuseEarlierDiscovery()
    {
        var settings = Settings(8126, "remote_config");
        var discovery = FlagEvaluationTestDiscovery.Available(settings);
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), discoveryService: discovery)!;
        try
        {
            module.Activate();
            module.EvaluationWriter.Should().NotBeNull();
            UpdateSettings(settings, 18126, "other-agent");
            module.EvaluationWriter.Should().BeNull();
            UpdateSettings(settings, 8126, "original-agent");
            module.EvaluationWriter.Should().BeNull("returning to an earlier address still requires discovery for the new settings");
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DelayedDiscoveryFromEarlierVisitCannotChangeCurrentAdmission(bool currentAgentSupportsEvp)
    {
        var settings = Settings(8126, "remote_config");
        var originalExporter = settings.Manager.InitialExporterSettings;
        var discovery = new FlagEvaluationTestDiscovery();
        var staleEndpoint = currentAgentSupportsEvp ? null : "evp_proxy/v2";
        discovery.Publish(originalExporter, staleEndpoint);
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), discoveryService: discovery)!;
        try
        {
            module.Activate();
            UpdateSettings(settings, 18126, "other-agent");
            var currentExporter = UpdateSettings(settings, 8126, "original-agent");
            discovery.Publish(currentExporter, currentAgentSupportsEvp ? "evp_proxy/v2" : null);
            (module.EvaluationWriter is not null).Should().Be(currentAgentSupportsEvp);

            // Replay a callback captured during the first visit after the new discovery completed.
            discovery.Publish(originalExporter, staleEndpoint);
            (module.EvaluationWriter is not null).Should().Be(currentAgentSupportsEvp, "the same address does not make an old exporter generation current");
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("remote_config")]
    public async Task NoAgentDoesNotStartEvaluationWriterOrDisableConfiguration(string source)
    {
        var delivery = new FakeDeliverySource();
        var rcm = new MockRcmSubscriptionManager();
        var module = FeatureFlagsModule.Create(Settings(8126, source), rcm, _ => delivery)!;
        try
        {
            module.Activate();

            module.EvaluationEventsEnabled.Should().BeTrue("configuration enablement must not latch Agent availability at provider construction");
            module.EvaluationWriter.Should().BeNull("without discovered Agent support, no events should be prepared or queued");
            delivery.Started.Should().Be(source == "agentless" ? 1 : 0);
            rcm.HasAnySubscription.Should().Be(source == "remote_config");
            module.GetExposureApi().Should().NotBeNull("the existing exposure lifecycle is outside this gate");
            var result = module.Evaluate("missing", FeatureFlagsValueType.String, "fallback", "subject", null);
            result.Value.Should().Be("fallback");
            result.Error.Should().Be("PROVIDER_NOT_READY");
            var flag = FeatureFlagsHelpers.CreateExposureFlag();
            flag.Allocations![0].DoLog = false;
            module.ApplyConfiguration(new ServerConfiguration { Flags = new FlagCollection { ["flag"] = flag } }).Should().BeTrue();
            var evaluated = module.Evaluate("flag", FeatureFlagsValueType.String, "fallback", "subject", null);
            evaluated.Value.Should().Be("tracked-value");
            evaluated.Error.Should().BeNull();
            module.EvaluationWriter.Should().BeNull("configuration delivery does not imply an Agent is available for events");
            await module.FlushAsync();
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("remote_config")]
    [InlineData("agentless")]
    public async Task EventKillSwitchDoesNotDisableConfigurationOrExposure(string source)
    {
        var values = new NameValueCollection
        {
            [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource] = source,
            [ConfigurationKeys.FeatureFlags.EvaluationCountsEnabled] = "false",
        };
        var delivery = new FakeDeliverySource();
        var rcm = new MockRcmSubscriptionManager();
        var settings = new TracerSettings(new NameValueConfigurationSource(values));
        var module = FeatureFlagsModule.Create(settings, rcm, _ => delivery, FlagEvaluationTestDiscovery.Available(settings))!;
        try
        {
            module.Should().NotBeNull();
            module.EvaluationEventsEnabled.Should().BeFalse();
            module.Activate();
            module.EvaluationWriter.Should().BeNull("disabled events must not allocate a sender or background consumer");
            delivery.Started.Should().Be(source == "agentless" ? 1 : 0);
            rcm.HasAnySubscription.Should().Be(source == "remote_config");
            module.GetExposureApi().Should().NotBeNull("explicit experiment exposures have a separate lifecycle");
            await module.FlushAsync();
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("remote_config")]
    [InlineData("agentless")]
    public async Task ActivationOwnsOneWriterIndependentOfConfigurationSource(string source)
    {
        using var agent = MockTracerAgent.Create(_output);
        var received = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) => received.Enqueue(args.Value);
        var delivery = new FakeDeliverySource();
        var settings = Settings(agent.Port, source);
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), _ => delivery, FlagEvaluationTestDiscovery.Available(settings))!;
        try
        {
            module.EvaluationWriter.Should().BeNull("provider adoption activates the writer, not tracer startup");
            module.EvaluationEventsEnabled.Should().BeTrue("the provider reads the setting before lazy writer activation");
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(module.Activate)));
            var writer = module.EvaluationWriter!;
            writer.Should().NotBeNull();
            module.Activate();
            module.EvaluationWriter.Should().BeSameAs(writer);
            delivery.Started.Should().Be(source == "agentless" ? 1 : 0);
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await module.FlushAsync();
            received.Should().ContainSingle();
            var request = received.Single();
            request.PathAndQuery.Should().Be("/evp_proxy/v2/api/v2/flagevaluation");
            request.Headers["DD-API-KEY"].Should().BeNull();
            request.BodyInJson.Should().NotContain("private-subject");

            writer.TryEnqueue(Observation()).Should().BeTrue();
            var close = module.DisposeAsync();
            module.EvaluationWriter.Should().BeNull();
            await close;
            received.Should().HaveCount(2, "shutdown drains the accepted evaluation");
            writer.TryEnqueue(Observation()).Should().BeFalse();
            module.Activate();
            module.EvaluationWriter.Should().BeNull();
            await module.DisposeAsync();
            delivery.Disposed.Should().Be(source == "agentless" ? 1 : 0);
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("together")]
    [InlineData("exporter-first")]
    [InlineData("context-first")]
    public async Task WriterCapturesLatestServiceContextAtActivationButKeepsExporterUpdates(string updateOrder)
    {
        using var original = MockTracerAgent.Create(_output);
        using var updated = MockTracerAgent.Create(_output);
        var originalRequests = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        var updatedRequests = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        original.EventPlatformProxyPayloadReceived += (_, args) => originalRequests.Enqueue(args.Value);
        updated.EventPlatformProxyPayloadReceived += (_, args) => updatedRequests.Enqueue(args.Value);
        var settings = Settings(original.Port, "remote_config");
        var discovery = FlagEvaluationTestDiscovery.Available(settings);
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), discoveryService: discovery)!;
        try
        {
            if (updateOrder == "exporter-first")
            {
                settings.Manager.UpdateManualConfigurationSettings(
                    new ManualInstrumentationConfigurationSource(
                        new Dictionary<string, object?>
                        {
                            [TracerSettingKeyConstants.AgentUriKey] = new Uri($"http://127.0.0.1:{updated.Port}"),
                        },
                        useDefaultSources: true),
                    NullConfigurationTelemetry.Instance);
            }
            else if (updateOrder == "context-first")
            {
                UpdateSettings(settings, original.Port, "before");
            }

            var updatedExporter = UpdateSettings(settings, updated.Port, "before");
            discovery.Publish(updatedExporter);
            module.Activate();
            var writer = module.EvaluationWriter!;
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await module.FlushAsync();
            originalRequests.Should().BeEmpty();
            updatedRequests.Should().ContainSingle();
            var initialContext = JObject.Parse(updatedRequests.Single().BodyInJson)["context"]!;
            initialContext["env"]!.Value<string>().Should().Be("before");
            initialContext["service"]!.Value<string>().Should().Be("updated-service");
            initialContext["version"]!.Value<string>().Should().Be("2");

            var returnedExporter = UpdateSettings(settings, original.Port, "after");
            discovery.Publish(returnedExporter);
            module.EvaluationWriter.Should().BeSameAs(writer);
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await module.FlushAsync();
            originalRequests.Should().ContainSingle();
            var context = JObject.Parse(originalRequests.Single().BodyInJson)["context"]!;
            context["env"]!.Value<string>().Should().Be("before", "service tags are fixed for the writer lifetime even when its Agent address changes");
            context["service"]!.Value<string>().Should().Be("updated-service");
            context["version"]!.Value<string>().Should().Be("2");
            await module.DisposeAsync();
            UpdateSettings(settings, updated.Port, "disposed");
            writer.TryEnqueue(Observation()).Should().BeFalse();
            await module.FlushAsync();
            updatedRequests.Should().ContainSingle();
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    [Fact]
    public async Task ServiceContextIsFixedBeforeTheFirstEventAndAcrossFlushes()
    {
        using var agent = MockTracerAgent.Create(_output);
        var received = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) => received.Enqueue(args.Value);
        var settings = Settings(agent.Port, "remote_config");
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), discoveryService: FlagEvaluationTestDiscovery.Available(settings))!;
        try
        {
            module.Activate();
            var writer = module.EvaluationWriter!;
            UpdateSettings(settings, agent.Port, "after-activation").Should().BeSameAs(settings.Manager.InitialExporterSettings);
            module.EvaluationWriter.Should().BeSameAs(writer, "service-only updates do not invalidate Agent discovery");
            writer.TryEnqueue(Observation()).Should().BeTrue();
            UpdateSettings(settings, agent.Port, "while-buffered");
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await module.FlushAsync();
            received.Should().ContainSingle();
            AssertOriginalContext(received.Single(), 2);

            UpdateSettings(settings, agent.Port, "after-flush");
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await module.FlushAsync();
            received.Should().HaveCount(2);
            AssertOriginalContext(received.Last(), 1);
        }
        finally
        {
            await module.DisposeAsync();
        }

        static void AssertOriginalContext(MockTracerAgent.EvpProxyPayload request, int count)
        {
            var payload = JObject.Parse(request.BodyInJson);
            payload["context"]!["service"]!.Value<string>().Should().Be("module-test");
            payload["context"]!["env"]!.Value<string>().Should().Be("unknown");
            payload["context"]!["version"]!.Value<string>().Should().Be("unknown");
            payload["flagEvaluations"]![0]!["evaluation_count"]!.Value<int>().Should().Be(count);
        }
    }

    [Fact]
    public async Task ActivationRacingDisposalDoesNotLeaveAnAvailableWriter()
    {
        for (var i = 0; i < 20; i++)
        {
            var settings = Settings(8126, "remote_config");
            var discovery = FlagEvaluationTestDiscovery.Available(settings);
            var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), discoveryService: discovery)!;
            await Task.WhenAll(Task.Run(module.Activate), Task.Run(module.Dispose), Task.Run(() => discovery.Publish(settings.Manager.InitialExporterSettings)));
            await module.DisposeAsync();
            module.EvaluationWriter.Should().BeNull();
            module.Activate();
            module.EvaluationWriter.Should().BeNull();
        }
    }

    [Fact]
    public async Task FailingConfigurationSourceFactoryDoesNotStartAnotherWriterOnReactivation()
    {
        var settings = Settings(8126, "agentless");
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager(), _ => throw new InvalidOperationException("source-factory-failure"), FlagEvaluationTestDiscovery.Available(settings))!;
        try
        {
            Action activate = module.Activate;
            activate.Should().Throw<InvalidOperationException>();
            var writer = module.EvaluationWriter;
            writer.Should().NotBeNull();
            activate.Should().NotThrow("activation was already claimed, even if configuration-source startup failed");
            module.EvaluationWriter.Should().BeSameAs(writer);
        }
        finally
        {
            await module.DisposeAsync();
        }
    }

    private static ExporterSettings UpdateSettings(TracerSettings settings, int port, string env)
    {
        var exporter = settings.Manager.InitialExporterSettings;
        using var subscription = settings.Manager.SubscribeToChanges(changes => exporter = changes.UpdatedExporter ?? changes.PreviousExporter);
        settings.Manager.UpdateManualConfigurationSettings(
            new ManualInstrumentationConfigurationSource(
                new Dictionary<string, object?>
                {
                    [TracerSettingKeyConstants.AgentUriKey] = new Uri($"http://127.0.0.1:{port}"),
                    [TracerSettingKeyConstants.EnvironmentKey] = env,
                    [TracerSettingKeyConstants.ServiceNameKey] = "updated-service",
                    [TracerSettingKeyConstants.ServiceVersionKey] = "2",
                },
                useDefaultSources: true),
            NullConfigurationTelemetry.Instance);
        return exporter;
    }

    private static TracerSettings Settings(int port, string source) => new(new NameValueConfigurationSource(new NameValueCollection
    {
        [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource] = source,
        [ConfigurationKeys.AgentUri] = $"http://127.0.0.1:{port}/",
        [ConfigurationKeys.ApiKey] = "config-only-secret",
        [ConfigurationKeys.ServiceName] = "module-test",
    }));

    private static FlagEvalEvent Observation() => new("flag", "variant", null, "private-subject", 1790000000000, null);

    private sealed class FakeDeliverySource : IFeatureFlagsDeliverySource
    {
        public int Started { get; private set; }

        public int Disposed { get; private set; }

        public void Start() => Started++;

        public void Dispose() => Disposed++;
    }
}
