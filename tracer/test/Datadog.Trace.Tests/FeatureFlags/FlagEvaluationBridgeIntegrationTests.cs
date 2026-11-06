// <copyright file="FlagEvaluationBridgeIntegrationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.FeatureFlags;

// Calls the CallTarget integrations the way the native tracer does, against the global tracer.
[Collection(nameof(WebRequestCollection))]
[TracerRestorer]
public class FlagEvaluationBridgeIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task EnqueueWithFeaturesIntegrationKeepsAttributesAndFeaturesApart()
    {
        using var agent = MockTracerAgent.Create(output);
        var received = new ConcurrentQueue<MockTracerAgent.EvpProxyPayload>();
        agent.EventPlatformProxyPayloadReceived += (_, args) => received.Enqueue(args.Value);
        var traceWriter = new Mock<IAgentWriter>();
        traceWriter.Setup(w => w.FlushTracesAsync()).Returns(Task.CompletedTask);
        traceWriter.Setup(w => w.FlushAndCloseAsync()).Returns(Task.CompletedTask);
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection
        {
            [ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource] = "remote_config",
            [ConfigurationKeys.AgentUri] = $"http://127.0.0.1:{agent.Port}",
            [ConfigurationKeys.ServiceName] = "bridge-probe",
        }));
        await using var tracer = TracerHelper.Create(settings, agentWriter: traceWriter.Object, discoveryService: FlagEvaluationTestDiscovery.Available(settings));
        TracerRestorerAttribute.SetTracer(tracer);
        tracer.TracerManager.FeatureFlags!.Activate();
        // Attributes without consent (flags 0) must never be sent; a swap with the features would send them.
        IReadOnlyDictionary<string, object?> attrs = new Dictionary<string, object?> { ["email"] = "attr-canary@example.test" };
        IReadOnlyDictionary<string, object?> features = new Dictionary<string, object?> { ["holdout.key"] = "h1" };

        OpenFeatureSdkEnqueueEVPWithFeaturesIntegration.OnMethodBegin<object, string, string?, string?, string?, long, string?, IReadOnlyDictionary<string, object?>, IReadOnlyDictionary<string, object?>, int>(
            "flag", "on", "allocation", "subject", 1790000000000L, null, attrs, features, 0);
        await tracer.FlushAsync();

        var body = received.Should().ContainSingle().Subject.BodyInJson;
        output.WriteLine(body);
        body.Should().NotContain("attr-canary", "unconsented attributes must never travel as features");
        JObject.Parse(body)["flagEvaluations"]![0]!["features"]!["holdout.key"]!.Value<string>().Should().Be("h1");
    }
}
