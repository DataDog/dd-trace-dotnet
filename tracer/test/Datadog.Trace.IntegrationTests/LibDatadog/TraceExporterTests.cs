// <copyright file="TraceExporterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.LibDatadog;

public class TraceExporterTests
{
    [SkippableTheory]
    [InlineData(TestTransports.Tcp)]
#if NETCOREAPP3_1_OR_GREATER
    [InlineData(TestTransports.Uds)]
#endif
    [InlineData(TestTransports.WindowsNamedPipe)]
    public async Task SendsTracesUsingDataPipeline(TestTransports transport)
    {
        if (transport == TestTransports.WindowsNamedPipe && !EnvironmentTools.IsWindows())
        {
            throw new SkipException("WindowsNamedPipe transport is only supported on Windows");
        }

        if (transport == TestTransports.Uds && EnvironmentTools.IsWindows())
        {
            throw new SkipException("Unix Domain Sockets (UDS) transport is only supported on Linux and OSX when data pipeline is enabled");
        }

        var pipeName = $"trace-{Guid.NewGuid()}";
        var udsPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        using var agent = GetAgent();
        var settings = GetSettings();
        var tracerSettings = TracerSettings.Create(settings);

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
        await using var tracer = TracerHelper.Create(tracerSettings, discoveryService: discovery);

        var testMetaStruct = new TestMetaStruct
        {
            Foo = "foo",
            Bar = 1,
        };
        var metaStructBytes = MetaStructHelper.ObjectToByteArray(testMetaStruct.ToDictionary());
        using (var span = tracer.StartSpan("operationName"))
        {
            span.ResourceName = "resourceName";
            span.Type = "test";
            span.SetMetaStruct("test-meta-struct", metaStructBytes);
        }

        await tracer.TracerManager.ShutdownAsync();
        var recordedSpans = await agent.WaitForSpansAsync(1);
        recordedSpans.Should().ContainSingle();

        var recordedSpan = recordedSpans.Should().ContainSingle().Subject;
        recordedSpan.Name.Should().Be("operationName");
        recordedSpan.Resource.Should().Be("resourceName");
        recordedSpan.Service.Should().Be("default-service");

        recordedSpan.MetaStruct.Should().ContainSingle();
        var recordedMetaStructBytes = recordedSpan.MetaStruct["test-meta-struct"];
        recordedMetaStructBytes.Should().BeEquivalentTo(metaStructBytes);

        Dictionary<string, object> GetSettings()
        {
            var settingsMap = new Dictionary<string, object>
            {
                { ConfigurationKeys.StatsComputationEnabled, true },
                { ConfigurationKeys.ServiceName, "default-service" },
                { ConfigurationKeys.ServiceVersion, "v1" },
                { ConfigurationKeys.Environment, "test" },
                { ConfigurationKeys.TraceDataPipelineEnabled, "true" },
            };

            switch (transport)
            {
                case TestTransports.Tcp:
                    if (agent is MockTracerAgent.TcpUdpAgent tcpAgent)
                    {
                        settingsMap[ConfigurationKeys.AgentPort] = tcpAgent.Port;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported agent type " + agent.GetType());
                    }

                    break;
                case TestTransports.WindowsNamedPipe:
                    settingsMap[ConfigurationKeys.TracesPipeName] = pipeName;
                    break;
                case TestTransports.Uds:
                    settingsMap[ConfigurationKeys.TracesUnixDomainSocketPath] = udsPath;
                    break;
                default:
                    throw new InvalidOperationException("Unsupported transport type " + transport);
            }

            return settingsMap;
        }

        MockTracerAgent GetAgent()
            => transport switch
            {
                TestTransports.Tcp => MockTracerAgent.Create(null),
                TestTransports.WindowsNamedPipe => MockTracerAgent.Create(null, new WindowsPipesConfig(pipeName, null)),
#if NETCOREAPP3_1_OR_GREATER
                TestTransports.Uds
                    => MockTracerAgent.Create(null, new UnixDomainSocketConfig(udsPath, null)),
#endif
                _ => throw new InvalidOperationException("Unsupported transport type " + transport),
            };
    }

    internal class TestMetaStruct
    {
        public string Foo { get; set; }

        public int Bar { get; set; }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                {
                    "foo", Foo
                },
                {
                    "bar", Bar
                }
            };
        }
    }
}
