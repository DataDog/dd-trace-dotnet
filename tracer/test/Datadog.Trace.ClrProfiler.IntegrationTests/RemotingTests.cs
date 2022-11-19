// <copyright file="RemotingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class RemotingTests : TracingIntegrationTest
    {
        public RemotingTests(ITestOutputHelper output)
            : base("Remoting", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span) => span.Name switch
            {
                "http.request" => span.IsWebRequest(),
                "remoting.request" => span.IsRemoting(),
                _ => Result.DefaultSuccess,
            };

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public async Task SubmitTracesOverHttp()
        {
            int remotingPort = TcpPortProvider.GetOpenPort();

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"Port={remotingPort} Protocol=http"))
            {
                const int expectedSpanCount = 6;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);
                ValidateIntegrationSpans(spans, expectedServiceName: "Samples.Remoting");

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(RemotingTests) + ".http");

                telemetry.AssertIntegrationEnabled(IntegrationId.Remoting);
            }
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public async Task SubmitTracesOverTcp()
        {
            int remotingPort = TcpPortProvider.GetOpenPort();

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"Port={remotingPort} Protocol=tcp"))
            {
                const int expectedSpanCount = 4;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);
                // ValidateIntegrationSpans(spans, expectedServiceName: "Samples.Remoting");

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(RemotingTests) + ".tcp");

                telemetry.AssertIntegrationEnabled(IntegrationId.Remoting);
            }
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableFact]
        public async Task SubmitTracesOverIpc()
        {
            int remotingPort = TcpPortProvider.GetOpenPort();

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"Port={remotingPort} Protocol=ipc"))
            {
                const int expectedSpanCount = 4;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);
                // ValidateIntegrationSpans(spans, expectedServiceName: "Samples.Remoting");

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(RemotingTests) + ".ipc");

                telemetry.AssertIntegrationEnabled(IntegrationId.Remoting);
            }
        }
    }
}
#endif
