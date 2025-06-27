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
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
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

                var traceId = HeadersUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = HeadersUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);
                var tracingEnabled = HeadersUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TracingEnabled);

                Assert.Null(traceId);
                Assert.Null(parentSpanId);
                Assert.Equal("false", tracingEnabled);
                await telemetry.AssertIntegrationDisabledAsync(IntegrationId.WebRequest);
                VerifyInstrumentation(processResult.Process);
            }
        }

        private async Task RunTest(string metadataSchemaVersion)
        {
            SetInstrumentationVerification();

            var expectedAllSpansCount = 134;

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-http-client" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}");

            var allSpans = (await agent.WaitForSpansAsync(expectedAllSpansCount, assertExpectedCount: false)).OrderBy(s => s.Start).ToList();

            var settings = VerifyHelper.GetSpanVerifierSettings();
#if NET9_0_OR_GREATER
            // .NET 9.0 changed the behaviour when AllowWriteStreamBuffering=false
            // The net result is that we end up creating a "WebRequest" span instead
            // of an "HttpClient" span in one of the cases. Rather than creating a whole
            // separate set of snapshots for .NET 9+, just "fixing" that one span instead.
            var rogueSpan = allSpans.SingleOrDefault(
                s => s.Tags.TryGetValue(Tags.HttpUrl, out var tag)
                  && tag.EndsWith("?BeginGetResponseAsync_NoBuffering"));

            // it should never be null, but fall through to fail the snapshots for easier debuggability if it is
            if (rogueSpan is not null)
            {
                Output.WriteLine("Updating span with HttpClient tags");
                rogueSpan.Tags["component"] = "HttpMessageHandler"; // previously "WebRequest"
                rogueSpan.Tags["http-client-handler-type"] = "System.Net.Http.SocketsHttpHandler"; // previously not set
            }
#endif
#if NETCOREAPP
            // different TFMs use different underlying handlers, which we don't really care about for the snapshots
            settings.AddSimpleScrubber("System.Net.Http.HttpClientHandler", "System.Net.Http.SocketsHttpHandler");
#endif
            var suffix = EnvironmentHelper.IsCoreClr() ? string.Empty : "_netfx";
            await VerifyHelper.VerifySpans(
                                   allSpans,
                                   settings,
                                   spans =>
                                       spans.OrderBy(x => VerifyHelper.GetRootSpanResourceName(x, spans))
                                            .ThenBy(x => VerifyHelper.GetSpanDepth(x, spans))
                                            .ThenBy(x => x.Tags.TryGetValue("http.url", out var url) ? url : string.Empty)
                                            .ThenBy(x => x.Start)
                                            .ThenBy(x => x.Duration))
                              .UseFileName($"{nameof(WebRequestTests)}{suffix}_{metadataSchemaVersion}");

            allSpans.Should().OnlyHaveUniqueItems(s => new { s.SpanId, s.TraceId });
            var httpSpans = allSpans.Where(s => s.Type == SpanTypes.Http).ToList();
            ValidateIntegrationSpans(httpSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

            await telemetry.AssertIntegrationEnabledAsync(IntegrationId.WebRequest);
            VerifyInstrumentation(processResult.Process);
        }
    }
}
