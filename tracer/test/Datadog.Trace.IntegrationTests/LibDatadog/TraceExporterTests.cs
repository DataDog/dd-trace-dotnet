// <copyright file="TraceExporterTests.cs" company="Datadog">
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

public class TraceExporterTests
{
    [Fact]
    public async Task SendsTracesUsingDataPipeline()
    {
        using var agent = MockTracerAgent.Create(null, TcpPortProvider.GetOpenPort());

        agent.CustomResponses[MockTracerResponseType.Traces] = new MockTracerResponse
        {
            StatusCode = 200,
            ContentType = "application/msgpack",
            Response = """
                       {
                           "rate_by_service": {
                               "service:default-service,env:test": 1.0,
                               "service:,env:": 0.8
                           }
                       }
                       """
        };

        var settings = TracerSettings.Create(new()
        {
            { ConfigurationKeys.StatsComputationEnabled, true },
            { ConfigurationKeys.ServiceName, "default-service" },
            { ConfigurationKeys.ServiceVersion, "v1" },
            { ConfigurationKeys.Environment, "test" },
            { ConfigurationKeys.AgentUri, $"http://localhost:{agent.Port}" },
            { ConfigurationKeys.DataPipelineEnabled, "true" },
        });

        var discovery = DiscoveryService.Create(settings.Exporter);
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
