// <copyright file="TransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class TransportTests : TestHelper
    {
        // Using Telemetry sample as it's simple
        public TransportTests(ITestOutputHelper output)
            : base("Telemetry", output)
        {
        }

        public static IEnumerable<object[]> Data =>
            Enum.GetValues(typeof(TracesTransportType))
                .Cast<TracesTransportType>()
                .Where(x => x != TracesTransportType.WindowsNamedPipe)
#if !NETCOREAPP3_1_OR_GREATER
                .Where(x => x != TracesTransportType.UnixDomainSocket)
#endif
                .Select(x => new object[] { x });

        [SkippableTheory]
        [MemberData(nameof(Data))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void TransportsWorkCorrectly(Enum transport)
        {
            const int expectedSpanCount = 2;
            var transportType = (TracesTransportType)transport;
            EnvironmentHelper.TransportType = GetTransport(transportType);

            using var telemetry = this.ConfigureTelemetry();
            using var agent = GetAgent(transportType);

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (var processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                processResult.ExitCode.Should().Be(0);
                var spans = agent.WaitForSpans(expectedSpanCount);

                spans.Count.Should().Be(expectedSpanCount);
            }

            telemetry.AssertConfiguration(ConfigTelemetryData.AgentTraceTransport, transportType.ToString());

            MockTracerAgent GetAgent(TracesTransportType type)
                => type switch
                {
                    TracesTransportType.Default => new MockTracerAgent(),
#if NETCOREAPP3_1_OR_GREATER
                    TracesTransportType.UnixDomainSocket
                        => new MockTracerAgent(new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null)),
#endif
                    _ => throw new InvalidOperationException("Unsupported transport type " + type),
                };

            TestTransports GetTransport(TracesTransportType type)
                => type switch
                {
                    TracesTransportType.Default => TestTransports.Tcp,
                    TracesTransportType.UnixDomainSocket => TestTransports.Uds,
                    TracesTransportType.WindowsNamedPipe => TestTransports.WindowsNamedPipe,
                    _ => throw new InvalidOperationException("Unsupported transport type " + type),
                };
        }
    }
}
