// <copyright file="TransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
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
#if !NETCOREAPP3_1_OR_GREATER
                .Where(x => x != TracesTransportType.UnixDomainSocket)
#endif
                .Select(x => new object[] { x });

        [SkippableTheory]
        [MemberData(nameof(Data))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task TransportsWorkCorrectly(Enum transport)
        {
            var transportType = (TracesTransportType)transport;
            if (transportType != TracesTransportType.WindowsNamedPipe)
            {
                await RunTest(transportType);
                return;
            }

            // The server implementation of named pipes is flaky so have 3 attempts
            var attemptsRemaining = 3;
            while (true)
            {
                try
                {
                    attemptsRemaining--;
                    await RunTest(transportType);
                    return;
                }
                catch (Exception ex) when (attemptsRemaining > 0 && ex is not SkipException)
                {
                    Output.WriteLine($"Error executing test. {attemptsRemaining} attempts remaining. {ex}");
                }
            }
        }

        private async Task RunTest(TracesTransportType transportType)
        {
            const int expectedSpanCount = 2;

            if (transportType == TracesTransportType.WindowsNamedPipe && !EnvironmentTools.IsWindows())
            {
                throw new SkipException("Can't use WindowsNamedPipes on non-Windows");
            }

            EnvironmentHelper.EnableTransport(GetTransport(transportType));

            using var telemetry = this.ConfigureTelemetry();
            using var agent = GetAgent(transportType);
            agent.Output = Output;

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");
            using (var processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                processResult.ExitCode.Should().Be(0);
                var spans = agent.WaitForSpans(expectedSpanCount);

                await VerifyHelper.VerifySpans(spans, VerifyHelper.GetSpanVerifierSettings())
                                  .DisableRequireUniquePrefix()
                                  .UseFileName("TransportTests");
            }

            telemetry.AssertConfiguration(ConfigTelemetryData.AgentTraceTransport, transportType.ToString());

            MockTracerAgent GetAgent(TracesTransportType type)
                => type switch
                {
                    TracesTransportType.Default => MockTracerAgent.Create(),
                    TracesTransportType.WindowsNamedPipe => MockTracerAgent.Create(new WindowsPipesConfig($"trace-{Guid.NewGuid()}", $"metrics-{Guid.NewGuid()}")),
#if NETCOREAPP3_1_OR_GREATER
                    TracesTransportType.UnixDomainSocket
                        => MockTracerAgent.Create(new UnixDomainSocketConfig(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), null)),
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
