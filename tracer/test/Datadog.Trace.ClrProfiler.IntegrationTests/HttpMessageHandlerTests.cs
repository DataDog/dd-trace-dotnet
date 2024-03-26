// <copyright file="HttpMessageHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
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

        internal static IEnumerable<InstrumentationOptions> InstrumentationOptionsValues =>
            new List<InstrumentationOptions>
            {
                new(instrumentSocketHandler: false, instrumentWinHttpOrCurlHandler: false),
                new(instrumentSocketHandler: false, instrumentWinHttpOrCurlHandler: true),
                new(instrumentSocketHandler: true, instrumentWinHttpOrCurlHandler: false),
                new(instrumentSocketHandler: true, instrumentWinHttpOrCurlHandler: true),
            };

        public static IEnumerable<object[]> IntegrationConfig() =>
            from instrumentationOptions in InstrumentationOptionsValues
            from socketHandlerEnabled in new[] { true, false }
            select new object[] { instrumentationOptions, socketHandlerEnabled };

        public static IEnumerable<object[]> IntegrationConfigWithObfuscation() =>
            from instrumentationOptions in InstrumentationOptionsValues
            from socketHandlerEnabled in new[] { true, false }
            from queryStringEnabled in new[] { true, false }
            from queryStringSizeAndExpectation in new[] { new KeyValuePair<int?, string>(null, "?key1=value1&<redacted>"), new KeyValuePair<int?, string>(200, "?key1=value1&<redacted>"), new KeyValuePair<int?, string>(2, "?k") }
            from metadataSchemaVersion in new[] { "v0", "v1" }
            from traceId128Enabled in new[] { true, false }
            select new object[]
                   {
                       instrumentationOptions,
                       socketHandlerEnabled,
                       queryStringEnabled,
                       queryStringSizeAndExpectation.Key,
                       queryStringSizeAndExpectation.Value,
                       metadataSchemaVersion,
                       traceId128Enabled
                   };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsHttpMessageHandler(metadataSchemaVersion);

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        [MemberData(nameof(IntegrationConfigWithObfuscation))]
        public async Task HttpClient_SubmitsTraces(
            InstrumentationOptions instrumentation,
            bool socketsHandlerEnabled,
            bool queryStringCaptureEnabled,
            int? queryStringSize,
            string expectedQueryString,
            string metadataSchemaVersion,
            bool traceId128Enabled)
        {
            try
            {
                SetInstrumentationVerification();
                ConfigureInstrumentation(instrumentation, socketsHandlerEnabled);
                SetEnvironmentVariable("DD_HTTP_SERVER_TAG_QUERY_STRING", queryStringCaptureEnabled ? "true" : "false");
                SetEnvironmentVariable("DD_TRACE_128_BIT_TRACEID_GENERATION_ENABLED", traceId128Enabled ? "true" : "false");
                SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

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
                using (var agent = EnvironmentHelper.GetMockAgent())
                using (ProcessResult processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
                {
                    agent.SpanFilters.Add(s => s.Type == SpanTypes.Http);
                    var spans = agent.WaitForSpans(expectedSpanCount);
                    Assert.Equal(expectedSpanCount, spans.Count);
                    ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                    foreach (var span in spans)
                    {
                        if (span.Tags[Tags.HttpStatusCode] == "502")
                        {
                            Assert.Equal(1, span.Error);
                        }

                        if (span.Tags.TryGetValue(Tags.HttpUrl, out var url))
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
                    var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                    var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);
                    var propagatedTags = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.PropagatedTags);

                    var firstSpan = spans.First();
                    Assert.Equal(firstSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                    Assert.Equal(firstSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);

                    var traceTags = TagPropagation.ParseHeader(propagatedTags);
                    var traceIdUpperTagFromHeader = traceTags.GetTag(Tags.Propagated.TraceIdUpper);
                    var traceIdUpperTagFromSpan = firstSpan.GetTag(Tags.Propagated.TraceIdUpper);

                    if (traceId128Enabled)
                    {
                        // assert that "_dd.p.tid" was added to the "x-datadog-tags" header (horizontal propagation)
                        // note this assumes Datadog propagation headers are enabled (which is the default).
                        Assert.NotNull(traceIdUpperTagFromHeader);

                        // not all spans will have this tag, but if it is present,
                        // it should match the value in the "x-datadog-tags" header
                        if (traceIdUpperTagFromSpan != null)
                        {
                            Assert.Equal(traceIdUpperTagFromHeader, traceIdUpperTagFromSpan);
                        }
                    }
                    else
                    {
                        // assert that "_dd.p.tid" was NOT added
                        Assert.Null(traceIdUpperTagFromHeader);
                        Assert.Null(traceIdUpperTagFromSpan);
                    }

                    using var scope = new AssertionScope();
                    telemetry.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
                    // ignore for now auto enabled for simplicity
                    telemetry.AssertIntegration(IntegrationId.HttpSocketsHandler, enabled: IsUsingSocketHandler(instrumentation), autoEnabled: null);
                    telemetry.AssertIntegration(IntegrationId.WinHttpHandler, enabled: IsUsingWinHttpHandler(instrumentation), autoEnabled: null);
                    telemetry.AssertIntegration(IntegrationId.CurlHandler, enabled: IsUsingCurlHandler(instrumentation), autoEnabled: null);
                    VerifyInstrumentation(processResult.Process);
                }
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
        [MemberData(nameof(IntegrationConfig))]
        public async Task TracingDisabled_DoesNotSubmitsTraces(InstrumentationOptions instrumentation, bool enableSocketsHandler)
        {
            try
            {
                SetInstrumentationVerification();
                ConfigureInstrumentation(instrumentation, enableSocketsHandler);

                using var telemetry = this.ConfigureTelemetry();
                int httpPort = TcpPortProvider.GetOpenPort();

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

                    using var scope = new AssertionScope();
                    // ignore auto enabled for simplicity
                    telemetry.AssertIntegrationDisabled(IntegrationId.HttpMessageHandler);
                    telemetry.AssertIntegration(IntegrationId.HttpSocketsHandler, enabled: false, autoEnabled: null);
                    telemetry.AssertIntegration(IntegrationId.WinHttpHandler, enabled: false, autoEnabled: null);
                    telemetry.AssertIntegration(IntegrationId.CurlHandler, enabled: false, autoEnabled: null);
                    VerifyInstrumentation(processResult.Process);
                }
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
    }
}
