// <copyright file="HttpMessageHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;
using Datadog.Trace.SemanticConventions;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition(nameof(HttpMessageHandlerTests), DisableParallelization = true)]
    public class HttpMessageHandlerTests : TracingIntegrationTest
    {
        public HttpMessageHandlerTests(ITestOutputHelper output)
            : base("HttpMessageHandler", output)
        {
            SetEnvironmentVariable("DD_HTTP_CLIENT_ERROR_STATUSES", "400-499, 502,-343,11-53, 500-500-200");
            SetServiceVersion("1.0.0");
        }

        public static InstrumentationOptions[] GetInstrumentationOptions()
        {
            return
            [
                new InstrumentationOptions(instrumentSocketHandler: false, instrumentWinHttpOrCurlHandler: false),
                new InstrumentationOptions(instrumentSocketHandler: false, instrumentWinHttpOrCurlHandler: true),
                new InstrumentationOptions(instrumentSocketHandler: true, instrumentWinHttpOrCurlHandler: false),
                new InstrumentationOptions(instrumentSocketHandler: true, instrumentWinHttpOrCurlHandler: true),
            ];
        }

        public static StringSizeExpectation[] GetStringSizeAndExpectation()
        {
            return
            [
                new StringSizeExpectation(null, "?key1=value1&<redacted>"),
                new StringSizeExpectation(200, "?key1=value1&<redacted>"),
                new StringSizeExpectation(2, "?k")
            ];
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsHttpMessageHandler(metadataSchemaVersion);

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        [CombinatorialOrPairwiseData]
        [Flaky("This test often fails with an ObjectDisposedException on shutdown. It seems tied to the HttpListener/WebServer implementation, but I couldn't figure out why")]
        public async Task HttpClient_SubmitsTraces(
            [CombinatorialMemberData(nameof(GetInstrumentationOptions))] InstrumentationOptions instrumentation,
            bool socketsHandlerEnabled,
            bool queryStringCaptureEnabled,
            [CombinatorialMemberData(nameof(GetStringSizeAndExpectation))] StringSizeExpectation queryStringSizeAndExpected,
            [MetadataSchemaVersionData] string metadataSchemaVersion,
            bool traceId128Enabled)
        {
            try
            {
                SetInstrumentationVerification();
                ConfigureInstrumentation(instrumentation, socketsHandlerEnabled);
                SetEnvironmentVariable("DD_HTTP_SERVER_TAG_QUERY_STRING", queryStringCaptureEnabled ? "true" : "false");
                SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_GENERATION_ENABLED", traceId128Enabled ? "true" : "false");
                SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

                int? queryStringSize = queryStringSizeAndExpected.Size;
                string expectedQueryString = queryStringSizeAndExpected.Expectation;

                if (queryStringSize.HasValue)
                {
                    SetEnvironmentVariable("DD_HTTP_SERVER_TAG_QUERY_STRING_SIZE", queryStringSize.ToString());
                }

                var expectedAsyncCount = CalculateExpectedAsyncSpans(instrumentation);
                var expectedSyncCount = CalculateExpectedSyncSpans(instrumentation);

                var expectedSpanCount = expectedAsyncCount + expectedSyncCount;

                int httpPort = TcpPortProvider.GetOpenPort();
                Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

                // metadata schema version
                var isExternalSpan = metadataSchemaVersion == "v0";
                var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-http-client" : EnvironmentHelper.FullSampleName;

                using var telemetry = this.ConfigureTelemetry();
                using var agent = EnvironmentHelper.GetMockAgent();
                using var processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}");

                agent.SpanFilters.Add(s => s.Type == SpanTypes.Http);
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);
                spans.Should().HaveCount(expectedSpanCount);
                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                foreach (var span in spans)
                {
                    string value;
                    if ((span.Tags.TryGetValue(Tags.HttpStatusCode, out value) || span.Tags.TryGetValue(OpenTelemetrySemanticConventions.HttpStatusCode, out value))
                        && value  == "502")
                    {
                        span.Error.Should().Be(1);
                    }

                    string url;
                    if (span.Tags.TryGetValue(Tags.HttpUrl, out url) || span.Tags.TryGetValue(OpenTelemetrySemanticConventions.HttpUrl, out url))
                    {
                        if (queryStringCaptureEnabled)
                        {
                            url.Should().EndWith(expectedQueryString);
                        }
                        else
                        {
                            new Uri(url).Query.Should().BeNullOrEmpty();
                        }
                    }
                }

                // parse http headers from stdout
                var headers = HeadersUtil.GetAllHeaders(processResult.StandardOutput).ToList();

                var firstSpan = spans.First();
                headers.FirstOrDefault(h => h.Key == HttpHeaderNames.TraceId).Value.Should().Be(firstSpan.TraceId.ToString(CultureInfo.InvariantCulture));
                headers.FirstOrDefault(h => h.Key == HttpHeaderNames.ParentId).Value.Should().Be(firstSpan.SpanId.ToString(CultureInfo.InvariantCulture));

                var propagatedTags = headers.FirstOrDefault(h => h.Key == HttpHeaderNames.PropagatedTags);
                var traceTags = TagPropagation.ParseHeader(propagatedTags.Value);
                var traceIdUpperTagFromHeader = traceTags.GetTag(Tags.Propagated.TraceIdUpper);
                var traceIdUpperTagFromSpan = firstSpan.GetTag(Tags.Propagated.TraceIdUpper);

                if (traceId128Enabled)
                {
                    // assert that "_dd.p.tid" was added to the "x-datadog-tags" header (horizontal propagation)
                    // note this assumes Datadog propagation headers are enabled (which is the default).
                    traceIdUpperTagFromHeader.Should().NotBeNull();

                    // not all spans will have this tag, but if it is present,
                    // it should match the value in the "x-datadog-tags" header
                    if (traceIdUpperTagFromSpan != null)
                    {
                        traceIdUpperTagFromSpan.Should().Be(traceIdUpperTagFromHeader);
                    }
                }
                else
                {
                    // assert that "_dd.p.tid" was NOT added
                    traceIdUpperTagFromHeader.Should().BeNull();
                    traceIdUpperTagFromSpan.Should().BeNull();
                }

                using var scope = new AssertionScope();
                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.HttpMessageHandler);
                // ignore for now auto enabled for simplicity
                await telemetry.AssertIntegrationAsync(IntegrationId.HttpSocketsHandler, enabled: IsUsingSocketHandler(instrumentation), autoEnabled: null);
                await telemetry.AssertIntegrationAsync(IntegrationId.WinHttpHandler, enabled: IsUsingWinHttpHandler(instrumentation), autoEnabled: null);
                await telemetry.AssertIntegrationAsync(IntegrationId.CurlHandler, enabled: IsUsingCurlHandler(instrumentation), autoEnabled: null);
                VerifyInstrumentation(processResult.Process);
            }
            catch (ExitCodeException)
            {
                if (EnvironmentHelper.IsCoreClr() && EnvironmentHelper.GetTargetFramework() == "netcoreapp2.1")
                {
                    throw new SkipException("Exit code exception. This test is flaky on netcoreapp 2.1 so skipping it.");
                }

                throw;
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        [CombinatorialOrPairwiseData]
        public async Task TracingDisabled_DoesNotSubmitsTraces(
            [CombinatorialMemberData(nameof(GetInstrumentationOptions))] InstrumentationOptions instrumentation,
            bool enableSocketsHandler)
        {
            try
            {
                SetInstrumentationVerification();
                ConfigureInstrumentation(instrumentation, enableSocketsHandler);

                using var telemetry = this.ConfigureTelemetry();
                var httpPort = TcpPortProvider.GetOpenPort();

                using var agent = EnvironmentHelper.GetMockAgent();
                using var processResult = await RunSampleAndWaitForExit(agent, arguments: $"TracingDisabled Port={httpPort}");

                agent.Spans.Should().NotContain(s => s.Type == SpanTypes.Http);

                // parse http headers from stdout
                var headers = HeadersUtil.GetAllHeaders(processResult.StandardOutput).ToList();
                headers.Where(h => h.Key == HttpHeaderNames.TracingEnabled).Should().AllSatisfy(h => h.Value.Should().Be("false"));

                // when tracing is disabled, we should not see any trace context or baggage headers
                using (_ = new AssertionScope())
                {
                    // Datadog trace context headers
                    headers.Should().NotContainKey(HttpHeaderNames.TraceId);
                    headers.Should().NotContainKey(HttpHeaderNames.ParentId);
                    headers.Should().NotContainKey(HttpHeaderNames.SamplingPriority);
                    headers.Should().NotContainKey(HttpHeaderNames.Origin);
                    headers.Should().NotContainKey(HttpHeaderNames.PropagatedTags);

                    // W3C trace context headers
                    headers.Should().NotContainKey(W3CTraceContextPropagator.TraceParentHeaderName);
                    headers.Should().NotContainKey(W3CTraceContextPropagator.TraceStateHeaderName);

                    // B3 trace context headers
                    headers.Should().NotContainKey(B3SingleHeaderContextPropagator.B3);
                    headers.Should().NotContainKey(B3MultipleHeaderContextPropagator.TraceId);
                    headers.Should().NotContainKey(B3MultipleHeaderContextPropagator.SpanId);
                    headers.Should().NotContainKey(B3MultipleHeaderContextPropagator.Sampled);

                    // Baggage header
                    headers.Should().NotContainKey(W3CBaggagePropagator.BaggageHeaderName);
                }

                using var scope = new AssertionScope();
                // ignore auto enabled for simplicity
                await telemetry.AssertIntegrationDisabledAsync(IntegrationId.HttpMessageHandler);
                await telemetry.AssertIntegrationAsync(IntegrationId.HttpSocketsHandler, enabled: false, autoEnabled: null);
                await telemetry.AssertIntegrationAsync(IntegrationId.WinHttpHandler, enabled: false, autoEnabled: null);
                await telemetry.AssertIntegrationAsync(IntegrationId.CurlHandler, enabled: false, autoEnabled: null);
                VerifyInstrumentation(processResult.Process);
            }
            catch (ExitCodeException)
            {
                if (EnvironmentHelper.IsCoreClr() && EnvironmentHelper.GetTargetFramework() == "netcoreapp2.1")
                {
                    throw new SkipException("Exit code exception. This test is flaky on netcoreapp 2.1 so skipping it.");
                }

                throw;
            }
        }

        private static int CalculateExpectedAsyncSpans(InstrumentationOptions instrumentation)
        {
            // net4x doesn't have patch
            var spansPerHttpClient = EnvironmentHelper.IsCoreClr() ? 35 : 31;

            var expectedSpanCount = spansPerHttpClient * 2; // default HttpClient and CustomHttpClientHandler

            if (IsUsingWinHttpHandler(instrumentation))
            {
                expectedSpanCount += spansPerHttpClient;
            }

            if (IsUsingSocketHandler(instrumentation))
            {
                expectedSpanCount += spansPerHttpClient;
            }

#if NETCOREAPP2_1 || NETCOREAPP3_0 || NETCOREAPP3_1
            if (instrumentation.InstrumentWinHttpOrCurlHandler == true)
            {
                // Add 1 span for internal WinHttpHandler and CurlHandler using the HttpMessageInvoker
                expectedSpanCount++;
            }
#endif

            return expectedSpanCount;
        }

        private static bool IsSocketsHandlerSupported() => EnvironmentHelper.IsCoreClr();

        private static bool IsWinHttpHandlerSupported()
            => RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

        private static bool IsCurlHandlerSupported()
        {
#if NETCOREAPP2_1 || NETCOREAPP3_0 || NETCOREAPP3_1
            return !RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#else
            return false;
#endif
        }

        private static bool IsUsingWinHttpHandler(InstrumentationOptions instrumentation)
        {
            // WinHttpHandler instrumentation is off by default, and only available on Windows
            return IsWinHttpHandlerSupported() && (instrumentation.InstrumentWinHttpOrCurlHandler == true);
        }

        private static bool IsUsingCurlHandler(InstrumentationOptions instrumentation)
        {
            return IsCurlHandlerSupported() && (instrumentation.InstrumentWinHttpOrCurlHandler == true);
        }

        private static bool IsUsingSocketHandler(InstrumentationOptions instrumentation)
        {
            // SocketsHttpHandler instrumentation is on by default
            return IsSocketsHandlerSupported() && (instrumentation.InstrumentSocketHandler ?? true);
        }

        private static int CalculateExpectedSyncSpans(InstrumentationOptions instrumentation)
        {
            // Sync requests are only enabled in .NET 5
            if (!EnvironmentHelper.IsNet5())
            {
                return 0;
            }

            var spansPerHttpClient = 21;

            var expectedSpanCount = spansPerHttpClient * 2; // default HttpClient and CustomHttpClientHandler

            // SocketsHttpHandler instrumentation is on by default
            if (instrumentation.InstrumentSocketHandler ?? true)
            {
                expectedSpanCount += spansPerHttpClient;
            }

            return expectedSpanCount;
        }

        private void ConfigureInstrumentation(InstrumentationOptions instrumentation, bool enableSocketsHandler)
        {
            // Should HttpClient try to use HttpSocketsHandler
            SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER", enableSocketsHandler ? "1" : "0");

            // Enable specific integrations, or use defaults
            if (instrumentation.InstrumentSocketHandler.HasValue)
            {
                SetEnvironmentVariable("DD_HttpSocketsHandler_ENABLED", instrumentation.InstrumentSocketHandler.Value ? "true" : "false");
            }

            if (instrumentation.InstrumentWinHttpOrCurlHandler.HasValue)
            {
                SetEnvironmentVariable("DD_WinHttpHandler_ENABLED", instrumentation.InstrumentWinHttpOrCurlHandler.Value ? "true" : "false");
                SetEnvironmentVariable("DD_CurlHandler_ENABLED", instrumentation.InstrumentWinHttpOrCurlHandler.Value ? "true" : "false");
            }
        }

        public class InstrumentationOptions : IXunitSerializable
        {
            // ReSharper disable once UnusedMember.Global
            public InstrumentationOptions()
            {
            }

            internal InstrumentationOptions(
                bool? instrumentSocketHandler,
                bool? instrumentWinHttpOrCurlHandler)
            {
                InstrumentSocketHandler = instrumentSocketHandler;
                InstrumentWinHttpOrCurlHandler = instrumentWinHttpOrCurlHandler;
            }

            internal bool? InstrumentSocketHandler { get; private set; }

            internal bool? InstrumentWinHttpOrCurlHandler { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                InstrumentSocketHandler = info.GetValue<bool?>(nameof(InstrumentSocketHandler));
                InstrumentWinHttpOrCurlHandler = info.GetValue<bool?>(nameof(InstrumentWinHttpOrCurlHandler));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(InstrumentSocketHandler), InstrumentSocketHandler);
                info.AddValue(nameof(InstrumentWinHttpOrCurlHandler), InstrumentWinHttpOrCurlHandler);
            }

            public override string ToString() =>
                $"InstrumentSocketHandler={InstrumentSocketHandler},InstrumentWinHttpOrCurlHandler={InstrumentWinHttpOrCurlHandler}";
        }

        public class StringSizeExpectation : IXunitSerializable
        {
            public StringSizeExpectation()
            {
            }

            public StringSizeExpectation(int? size, string expectation)
            {
                Size = size;
                Expectation = expectation;
            }

            public int? Size { get; private set; }

            public string Expectation { get; private set; }

            public void Deserialize(IXunitSerializationInfo info)
            {
                Size = info.GetValue<int?>(nameof(Size));
                Expectation = info.GetValue<string>(nameof(Expectation));
            }

            public void Serialize(IXunitSerializationInfo info)
            {
                info.AddValue(nameof(Size), Size);
                info.AddValue(nameof(Expectation), Expectation);
            }

            public override string ToString() => $"Size={Size},Expectation={Expectation}";
        }
    }
}
