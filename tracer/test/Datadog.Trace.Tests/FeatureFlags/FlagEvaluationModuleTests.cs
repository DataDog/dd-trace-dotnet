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
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.FeatureFlags;

[Collection(nameof(WebRequestCollection))]
public class FlagEvaluationModuleTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData("remote_config")]
    [InlineData("agentless")]
    public async Task EventKillSwitchDoesNotDisableConfigurationOrExposure(string source)
    {
        var values = new NameValueCollection
        {
            [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource] = source,
            [ConfigurationKeys.FeatureFlags.FlaggingEvaluationCountsEnabled] = "false",
        };
        var delivery = new FakeDeliverySource();
        var rcm = new MockRcmSubscriptionManager();
        var module = FeatureFlagsModule.Create(new TracerSettings(new NameValueConfigurationSource(values)), rcm, _ => delivery)!;
        try
        {
            module.Should().NotBeNull();
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
        var module = FeatureFlagsModule.Create(Settings(agent.Port, source), new MockRcmSubscriptionManager(), _ => delivery)!;
        try
        {
            module.EvaluationWriter.Should().BeNull("provider adoption activates the writer, not tracer startup");
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
    public async Task ExporterAndServiceContextUpdatesApplyBeforeAndAfterActivation(string updateOrder)
    {
        using var original = MockTracerAgent.Create(_output);
        using var updated = MockTracerAgent.Create(_output);
        var originalRequests = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        var updatedRequests = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        original.EventPlatformProxyPayloadReceived += (_, args) => originalRequests.Enqueue(args.Value);
        updated.EventPlatformProxyPayloadReceived += (_, args) => updatedRequests.Enqueue(args.Value);
        var settings = Settings(original.Port, "remote_config");
        var module = FeatureFlagsModule.Create(settings, new MockRcmSubscriptionManager())!;
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

            UpdateSettings(settings, updated.Port, "before");
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

            UpdateSettings(settings, original.Port, "after");
            writer.TryEnqueue(Observation()).Should().BeTrue();
            await module.FlushAsync();
            originalRequests.Should().ContainSingle();
            var context = JObject.Parse(originalRequests.Single().BodyInJson)["context"]!;
            context["env"]!.Value<string>().Should().Be("after");
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
    public async Task ActivationRacingDisposalDoesNotLeaveAnAvailableWriter()
    {
        for (var i = 0; i < 20; i++)
        {
            var module = FeatureFlagsModule.Create(Settings(8126, "remote_config"), new MockRcmSubscriptionManager())!;
            await Task.WhenAll(Task.Run(module.Activate), Task.Run(module.Dispose));
            await module.DisposeAsync();
            module.EvaluationWriter.Should().BeNull();
            module.Activate();
            module.EvaluationWriter.Should().BeNull();
        }
    }

    [Fact]
    public async Task FailingConfigurationSourceFactoryDoesNotStartAnotherWriterOnReactivation()
    {
        var module = FeatureFlagsModule.Create(Settings(8126, "agentless"), new MockRcmSubscriptionManager(), _ => throw new InvalidOperationException("source-factory-failure"))!;
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

    private static void UpdateSettings(TracerSettings settings, int port, string env)
        => settings.Manager.UpdateManualConfigurationSettings(
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

    private static TracerSettings Settings(int port, string source) => new(new NameValueConfigurationSource(new NameValueCollection
    {
        [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource] = source,
        [ConfigurationKeys.AgentUri] = $"http://127.0.0.1:{port}",
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
