// <copyright file="WebRequestTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition(nameof(WebRequestTests), DisableParallelization = true)]
    [Collection(nameof(WebRequestTests))]
    public class WebRequestTests : TracingIntegrationTest
    {
        public WebRequestTests(ITestOutputHelper output)
            : base("WebRequest", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_HTTP_CLIENT_ERROR_STATUSES", "410-499");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsWebRequest(metadataSchemaVersion);

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task SubmitsTracesV0() => RunTest("v0");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public Task SubmitsTracesV1() => RunTest("v1");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task TracingDisabled_DoesNotSubmitsTraces()
        {
            SetInstrumentationVerification();

            int httpPort = TcpPortProvider.GetOpenPort();

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"TracingDisabled Port={httpPort}"))
            {
                var spans = agent.Spans.Where(s => s.Type == SpanTypes.Http);
                Assert.Empty(spans);

                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);
                var tracingEnabled = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TracingEnabled);

                Assert.Null(traceId);
                Assert.Null(parentSpanId);
                Assert.Equal("false", tracingEnabled);
                telemetry.AssertIntegrationDisabled(IntegrationId.WebRequest);
                VerifyInstrumentation(processResult.Process);
            }
        }

        private async Task RunTest(string metadataSchemaVersion)
        {
            SetInstrumentationVerification();
            var expectedAllSpansCount = 130;
            var expectedSpanCount = 87;

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-http-client" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                var allSpans = agent.WaitForSpans(expectedAllSpansCount).OrderBy(s => s.Start).ToList();
                allSpans.Should().OnlyHaveUniqueItems(s => new { s.SpanId, s.TraceId });

                var spans = allSpans.Where(s => s.Type == SpanTypes.Http).ToList();
                spans.Should().HaveCount(expectedSpanCount);
                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                var okSpans = spans.Where(s => s.Tags[Tags.HttpStatusCode] == "200").ToList();
                var notFoundSpans = spans.Where(s => s.Tags[Tags.HttpStatusCode] == "404").ToList();
                var teapotSpans = spans.Where(s => s.Tags[Tags.HttpStatusCode] == "418").ToList();

                (okSpans.Count + notFoundSpans.Count + teapotSpans.Count).Should().Be(expectedSpanCount);
                okSpans.Should().OnlyContain(s => s.Error == 0);
                notFoundSpans.Should().OnlyContain(s => s.Error == 0);
                teapotSpans.Should().OnlyContain(s => s.Error == 1);

                var firstSpan = spans.First();
                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);

                Assert.Equal(firstSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                Assert.Equal(firstSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);
                telemetry.AssertIntegrationEnabled(IntegrationId.WebRequest);
                VerifyInstrumentation(processResult.Process);
            }
        }
    }
}
