// <copyright file="RemotingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Linq;
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

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.Name switch
        {
            "http.request" => span.IsWebRequest(metadataSchemaVersion),
            "remoting.request" => span.Tags["span.kind"] switch
            {
                SpanKinds.Client => span.IsRemotingClient(metadataSchemaVersion),
                SpanKinds.Server => span.IsRemotingServer(metadataSchemaVersion),
                _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the Remoting integration: {span.Tags["span.kind"]}", nameof(span)),
            },
            _ => Result.DefaultSuccess,
        };

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableTheory]
        [InlineData("v0")]
        [InlineData("v1")]
        public async Task SubmitTracesOverHttp(string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

            int remotingPort = TcpPortProvider.GetOpenPort();

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"Port={remotingPort} Protocol=http"))
            {
                const int expectedSpanCount = 6;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                var rpcClientSpans = spans.Where(IsRpcClientSpan);
                var rpcServerSpans = spans.Where(IsRpcServerSpan);
                var httpRequestSpans = spans.Where(IsHttpRequestSpan);

                var isExternalSpan = metadataSchemaVersion == "v0";
                var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-remoting" : EnvironmentHelper.FullSampleName;
                var httpClientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-http-client" : EnvironmentHelper.FullSampleName;

                ValidateIntegrationSpans(rpcServerSpans, metadataSchemaVersion, expectedServiceName: EnvironmentHelper.FullSampleName, isExternalSpan: false);
                ValidateIntegrationSpans(rpcClientSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan: isExternalSpan);
                ValidateIntegrationSpans(httpRequestSpans, metadataSchemaVersion, expectedServiceName: httpClientSpanServiceName, isExternalSpan: isExternalSpan);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(RemotingTests) + ".http" + $".Schema{metadataSchemaVersion.ToUpper()}");

                telemetry.AssertIntegrationEnabled(IntegrationId.Remoting);
            }
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableTheory]
        [InlineData("v0")]
        [InlineData("v1")]
        public async Task SubmitTracesOverTcp(string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

            int remotingPort = TcpPortProvider.GetOpenPort();

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"Port={remotingPort} Protocol=tcp"))
            {
                const int expectedSpanCount = 4;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                var rpcClientSpans = spans.Where(IsRpcClientSpan);
                var rpcServerSpans = spans.Where(IsRpcServerSpan);

                var isExternalSpan = metadataSchemaVersion == "v0";
                var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-remoting" : EnvironmentHelper.FullSampleName;

                ValidateIntegrationSpans(rpcServerSpans, metadataSchemaVersion, expectedServiceName: EnvironmentHelper.FullSampleName, isExternalSpan: false);
                ValidateIntegrationSpans(rpcClientSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan: isExternalSpan);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(RemotingTests) + ".tcp" + $".Schema{metadataSchemaVersion.ToUpper()}");

                telemetry.AssertIntegrationEnabled(IntegrationId.Remoting);
            }
        }

        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [SkippableTheory]
        [InlineData("v0")]
        [InlineData("v1")]
        public async Task SubmitTracesOverIpc(string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

            int remotingPort = TcpPortProvider.GetOpenPort();

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"Port={remotingPort} Protocol=ipc"))
            {
                const int expectedSpanCount = 4;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                var rpcClientSpans = spans.Where(IsRpcClientSpan);
                var rpcServerSpans = spans.Where(IsRpcServerSpan);

                var isExternalSpan = metadataSchemaVersion == "v0";
                var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-remoting" : EnvironmentHelper.FullSampleName;

                ValidateIntegrationSpans(rpcServerSpans, metadataSchemaVersion, expectedServiceName: EnvironmentHelper.FullSampleName, isExternalSpan: false);
                ValidateIntegrationSpans(rpcClientSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan: isExternalSpan);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(RemotingTests) + ".ipc" + $".Schema{metadataSchemaVersion.ToUpper()}");

                telemetry.AssertIntegrationEnabled(IntegrationId.Remoting);
            }
        }

        private static bool IsRpcClientSpan(MockSpan span)
        {
            return string.Equals(span.GetTag("component"), "Remoting", StringComparison.OrdinalIgnoreCase)
                && string.Equals(span.GetTag("span.kind"), "client", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRpcServerSpan(MockSpan span)
        {
            return string.Equals(span.GetTag("component"), "Remoting", StringComparison.OrdinalIgnoreCase)
                && string.Equals(span.GetTag("span.kind"), "server", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHttpRequestSpan(MockSpan span)
        {
            return string.Equals(span.Name, "http.request", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
