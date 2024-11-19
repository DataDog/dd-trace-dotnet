// <copyright file="SendTracesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.IntegrationTests.LibDatadog;

public class SendTracesTests
{
    [Fact]
    public async Task SendsTracesUsingDataPipeline()
    {
        using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());

        var settings = new TracerSettings
        {
            StatsComputationEnabled = true,
            ServiceName = "default-service",
            ServiceVersion = "v1",
            Environment = "test",
            Exporter = new ExporterSettings
            {
                AgentUri = new Uri($"http://localhost:{agent.Port}"),
            }
        };

        var discovery = DiscoveryService.Create(settings.Build().Exporter);
        var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null, discoveryService: discovery);

        using var span = tracer.StartSpan("operationName");
        span.ResourceName = "resourceName";
        span.Type = "test";
        span.Finish();

        await tracer.TracerManager.ShutdownAsync();
        var recordedSpans = agent.WaitForSpans(1);
        Assert.Equal(1, recordedSpans.Count);

        var recordedSpan = recordedSpans[0];
        Assert.Equal("operationName", recordedSpan.Name);
        Assert.Equal("resourceName", recordedSpan.Resource);
        Assert.Equal("default-service", recordedSpan.Service);
    }
}
