// <copyright file="DataStreamsMonitoringTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DataStreamsMonitoring.Aggregation;
using Datadog.Trace.DataStreamsMonitoring.Hashes;
using Datadog.Trace.DataStreamsMonitoring.Transport;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tests.Agent;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.DataStreamsMonitoring;

public class DataStreamsMonitoringTransportTests
{
    private readonly ITestOutputHelper _output;

    public DataStreamsMonitoringTransportTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static IEnumerable<object[]> Data =>
        Enum.GetValues(typeof(TracesTransportType))
            .Cast<TracesTransportType>()
#if !NETCOREAPP3_1_OR_GREATER
            .Where(x => x != TracesTransportType.UnixDomainSocket)
#endif
             // Run named pipes tests only on Windows
            .Where(x => EnvironmentTools.IsWindows() || x != TracesTransportType.WindowsNamedPipe)
            .Select(x => new object[] { x });

    [SkippableTheory]
    [MemberData(nameof(Data))]
    [Trait("Category", "EndToEnd")]
    [Trait("RunOnWindows", "True")]
    public async Task TransportsWorkCorrectly(Enum transport)
    {
        using var agent = Create((TracesTransportType)transport);

        // We don't want to trigger a flush based on the timer, only based on the disposal of the writer
        // That ensures we only get a single payload
        var bucketDurationMs = (int)TimeSpan.FromMinutes(60).TotalMilliseconds;
        var tracerSettings = new TracerSettings { Exporter = GetExporterSettings(agent) };
        var api = new DataStreamsApi(
            DataStreamsTransportStrategy.GetAgentIntakeFactory(tracerSettings.Build().Exporter));

        var discovery = new DiscoveryServiceMock();
        var writer = new DataStreamsWriter(
            new DataStreamsAggregator(
                new DataStreamsMessagePackFormatter("env", "service"),
                bucketDurationMs),
            api,
            bucketDurationMs: bucketDurationMs,
            discovery);

        discovery.TriggerChange();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeNanoseconds();
        writer.Add(CreateStatsPoint(timestamp));
        writer.AddBacklog(CreateBacklogPoint(timestamp));

        await writer.DisposeAsync();

        var result = agent.WaitForDataStreams(1);

        // we can't guarantee only having a single payload due to race conditions in the flushing code
        result.Should().OnlyContain(payload => payload.Env == "env");
        result.Should().OnlyContain(payload => payload.Service == "service");
        agent.DataStreamsRequestHeaders
             .Should()
             .OnlyContain(
                  headers => headers.AllKeys.Contains("Content-Encoding")
                          && headers["Content-Encoding"] == "gzip");

        // should only have a single backlog across all payloads, but we don't know which payload it will be in
        result
           .SelectMany(x => x.Stats)
           .Where(x => x.Backlogs is not null)
           .Should()
           .ContainSingle()
           .Which.Backlogs.Should()
           .ContainSingle();
    }

    private StatsPoint CreateStatsPoint(long timestamp = 0)
        => new StatsPoint(
            edgeTags: new[] { "direction:out", "type:kafka" },
            hash: new PathwayHash((ulong)Math.Abs(ThreadSafeRandom.Shared.Next(int.MaxValue))),
            parentHash: new PathwayHash((ulong)Math.Abs(ThreadSafeRandom.Shared.Next(int.MaxValue))),
            timestampNs: timestamp != 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeNanoseconds(),
            pathwayLatencyNs: 5_000_000_000,
            edgeLatencyNs: 2_000_000_000,
            payloadSizeBytes: 1024);

    private BacklogPoint CreateBacklogPoint(long timestamp = 0)
        => new BacklogPoint("type:produce", 100, timestamp != 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeNanoseconds());

    private MockTracerAgent Create(TracesTransportType transportType)
        => transportType switch
        {
            TracesTransportType.Default => MockTracerAgent.Create(_output),
            TracesTransportType.WindowsNamedPipe => MockTracerAgent.Create(_output, new WindowsPipesConfig($"trace-{Guid.NewGuid()}", $"metrics-{Guid.NewGuid()}")),
#if NETCOREAPP3_1_OR_GREATER
            TracesTransportType.UnixDomainSocket => MockTracerAgent.Create(
                _output,
                new UnixDomainSocketConfig(
                    Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                    Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()))),
#endif
            _ => throw new InvalidOperationException("Unknown transport type " + transportType),
        };

    private ExporterSettings GetExporterSettings(MockTracerAgent agent)
        => agent switch
        {
            MockTracerAgent.TcpUdpAgent x => new ExporterSettings { AgentUri = new Uri($"http://localhost:{x.Port}") },
            MockTracerAgent.NamedPipeAgent x => new ExporterSettings(new NameValueConfigurationSource(new() { { ConfigurationKeys.TracesPipeName, x.TracesWindowsPipeName } })),
#if NETCOREAPP3_1_OR_GREATER
            MockTracerAgent.UdsAgent x =>  new ExporterSettings { AgentUri = new Uri(ExporterSettings.UnixDomainSocketPrefix + x.TracesUdsPath) },
#endif
            _ => throw new InvalidOperationException("Unknown agent type " + agent.GetType()),
        };
}
