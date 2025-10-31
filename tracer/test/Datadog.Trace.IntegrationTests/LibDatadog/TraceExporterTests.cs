// <copyright file="TraceExporterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec.Rasp;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.LibDatadog;
using Datadog.Trace.LibDatadog.DataPipeline;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
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
        var tracerSettings = TracerSettings.Create(settings, isLibDatadogAvailable: new LibDatadogAvailableResult(true));
        tracerSettings.DataPipelineEnabled.Should().BeTrue();

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

        var testMetaStruct = new TestMetaStruct
        {
            Foo = "foo",
            Bar = 1,
        };
        var metaStructBytes = MetaStructHelper.ObjectToByteArray(testMetaStruct.ToDictionary());

        var sampleRateResponses = new ConcurrentQueue<Dictionary<string, float>>();

        var discovery = DiscoveryService.CreateUnmanaged(tracerSettings.Manager.InitialExporterSettings);
        var statsd = new NoOpStatsd();

        // We have to replace the agent writer so that we can intercept the sample rate responses
        ManagedTraceExporter.TryCreateTraceExporter(
            tracerSettings,
            rates => sampleRateResponses.Enqueue(rates),
            TelemetrySettings.FromSource(NullConfigurationSource.Instance, new ConfigurationTelemetry(), tracerSettings, isAgentAvailable: null),
            out var exporter).Should().BeTrue();
        exporter.Should().NotBeNull();

        var agentWriter = new AgentWriter(exporter, new NullStatsAggregator(), statsd, tracerSettings);

        await using (var tracer = TracerHelper.Create(tracerSettings, agentWriter: agentWriter, statsd: statsd, discoveryService: discovery))
        {
            using (var span = tracer.StartSpan("operationName"))
            {
                span.ResourceName = "resourceName";
                span.Type = "test";
                span.SetMetaStruct("test-meta-struct", metaStructBytes);
            }

            using (var span2 = tracer.StartSpan("operationName2"))
            {
                span2.ResourceName = "resourceName2";
                span2.Type = "test";
                span2.SetMetaStruct("test-meta-struct", metaStructBytes);
            }
        }

        var recordedSpans = await agent.WaitForSpansAsync(2);
        recordedSpans.Should().HaveCount(2);

        var firstSpan = recordedSpans.Should()
                                        .Contain(s => s.Name == "operationName")
                                        .Subject;
        firstSpan.Resource.Should().Be("resourceName");
        firstSpan.Service.Should().Be("default-service");

        firstSpan.MetaStruct.Should().ContainSingle();
        var recordedMetaStructBytes = firstSpan.MetaStruct["test-meta-struct"];
        recordedMetaStructBytes.Should().BeEquivalentTo(metaStructBytes);

        var secondSpan = recordedSpans.Should()
                                      .Contain(s => s.Name == "operationName2")
                                      .Subject;
        secondSpan.Resource.Should().Be("resourceName2");
        secondSpan.Service.Should().Be("default-service");

        var recordedMetaStructBytes2 = secondSpan.MetaStruct["test-meta-struct"];
        recordedMetaStructBytes2.Should().BeEquivalentTo(metaStructBytes);

        var expectedRates = new Dictionary<string, float>
        {
            { "service:default-service,env:test", 1.0f },
            { "service:,env:", 0.8f },
        };
        sampleRateResponses.Should()
                           .NotBeEmpty()
                           .And.AllSatisfy(rates => rates.Should().BeEquivalentTo(expectedRates));
        sampleRateResponses.Should().ContainSingle();

        Dictionary<string, object> GetSettings()
        {
            var settingsMap = new Dictionary<string, object>
            {
                { ConfigurationKeys.StatsComputationEnabled, false },
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
