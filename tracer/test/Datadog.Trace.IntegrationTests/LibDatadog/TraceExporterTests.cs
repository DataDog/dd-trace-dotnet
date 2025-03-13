// <copyright file="TraceExporterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.IntegrationTests.LibDatadog;

public class TraceExporterTests
{
    [SkippableTheory]
    [InlineData(TestTransports.Tcp)]
    [InlineData(TestTransports.Uds)]
    [InlineData(TestTransports.WindowsNamedPipe)]
    public async Task SendsTracesUsingDataPipeline(TestTransports transport)
    {
        if (transport == TestTransports.WindowsNamedPipe && !EnvironmentTools.IsWindows())
        {
            throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
        }

        if (transport == TestTransports.Uds && !EnvironmentTools.IsLinux())
        {
            throw new SkipException("Can't use Unix Domain Sockets on non-Linux with data pipeline enabled");
        }

        var settings = GetSettings(transport);
        var tracerSettings = TracerSettings.Create(settings);

        using var agent = GetAgent(transport, settings);
        agent.CustomResponses[MockTracerResponseType.Traces] = new MockTracerResponse
        {
            StatusCode = 200,
            ContentType = "application/json",
            Response = """
                       {
                           "rate_by_service": {
                               "service:default-service,env:test": 1.0,
                               "service:,env:": 0.8
                           }
                       }
                       """
        };

        var discovery = DiscoveryService.Create(tracerSettings.Exporter);
        var tracer = TracerHelper.Create(tracerSettings, discoveryService: discovery);

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

        Dictionary<string, object> GetSettings(TestTransports type)
        {
            var settings = new Dictionary<string, object>
            {
                { ConfigurationKeys.StatsComputationEnabled, true },
                { ConfigurationKeys.ServiceName, "default-service" },
                { ConfigurationKeys.ServiceVersion, "v1" },
                { ConfigurationKeys.Environment, "test" },
                { ConfigurationKeys.TraceDataPipelineEnabled, "true" },
            };

            switch (type)
            {
                case TestTransports.Tcp:
                    settings[ConfigurationKeys.AgentPort] = TcpPortProvider.GetOpenPort();
                    break;
                case TestTransports.WindowsNamedPipe:
                    settings[ConfigurationKeys.TracesPipeName] = $"trace-{Guid.NewGuid()}";
                    break;
                case TestTransports.Uds:
                    settings[ConfigurationKeys.TracesUnixDomainSocketPath] = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    break;
                default:
                    throw new InvalidOperationException("Unsupported transport type " + type);
            }

            return settings;
        }

        MockTracerAgent GetAgent(TestTransports type, Dictionary<string, object> settings)
            => type switch
            {
                TestTransports.Tcp => MockTracerAgent.Create(null, int.Parse(settings[ConfigurationKeys.AgentPort].ToString())),
                TestTransports.WindowsNamedPipe => MockTracerAgent.Create(null, new WindowsPipesConfig(settings[ConfigurationKeys.TracesPipeName].ToString(), null)),
#if NETCOREAPP3_1_OR_GREATER
                TestTransports.Uds
                    => MockTracerAgent.Create(null, new UnixDomainSocketConfig(settings[ConfigurationKeys.TracesUnixDomainSocketPath].ToString(), null)),
#endif
                _ => throw new InvalidOperationException("Unsupported transport type " + type),
            };
    }
}
