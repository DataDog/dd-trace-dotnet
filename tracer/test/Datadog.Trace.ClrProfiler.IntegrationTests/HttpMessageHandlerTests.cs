// <copyright file="HttpMessageHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
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
        private const string ServiceName = "Samples.HttpMessageHandler";

        public HttpMessageHandlerTests(ITestOutputHelper output)
            : base("HttpMessageHandler", output)
        {
            SetEnvironmentVariable("DD_HTTP_CLIENT_ERROR_STATUSES", "400-499, 502,-343,11-53, 500-500-200");
            SetServiceVersion("1.0.0");
        }

        internal static IEnumerable<InstrumentationOptions> InstrumentationOptionsValues =>
            new List<InstrumentationOptions>
            {
                new InstrumentationOptions(instrumentSocketHandler: false, instrumentWinHttpOrCurlHandler: false),
                new InstrumentationOptions(instrumentSocketHandler: false, instrumentWinHttpOrCurlHandler: true),
                new InstrumentationOptions(instrumentSocketHandler: true, instrumentWinHttpOrCurlHandler: false),
                new InstrumentationOptions(instrumentSocketHandler: true, instrumentWinHttpOrCurlHandler: true),
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
            select new object[] { instrumentationOptions, socketHandlerEnabled, queryStringEnabled, queryStringSizeAndExpectation.Key,  queryStringSizeAndExpectation.Value, metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsHttpMessageHandler();

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        [MemberData(nameof(IntegrationConfigWithObfuscation))]
        public void HttpClient_SubmitsTraces(InstrumentationOptions instrumentation, bool enableSocketsHandler, bool queryStringCaptureEnabled, int? queryStringSize, string expectedQueryString, string metadataSchemaVersion)
        {
            SetInstrumentationVerification();
            ConfigureInstrumentation(instrumentation, enableSocketsHandler);
            SetEnvironmentVariable("DD_HTTP_SERVER_TAG_QUERY_STRING", queryStringCaptureEnabled ? "true" : "false");

            if (queryStringSize.HasValue)
            {
                SetEnvironmentVariable("DD_HTTP_SERVER_TAG_QUERY_STRING_SIZE", queryStringSize.ToString());
            }

            var expectedAsyncCount = CalculateExpectedAsyncSpans(instrumentation);
            var expectedSyncCount = CalculateExpectedSyncSpans(instrumentation);

            var expectedSpanCount = expectedAsyncCount + expectedSyncCount;

            const string expectedOperationName = "http.request";

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{ServiceName}-http-client" : ServiceName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);
                ValidateIntegrationSpans(spans, expectedServiceName: clientSpanServiceName, isExternalSpan);

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

                var firstSpan = spans.First();
                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);

                Assert.Equal(firstSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                Assert.Equal(firstSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);

                using var scope = new AssertionScope();
                telemetry.AssertIntegrationEnabled(IntegrationId.HttpMessageHandler);
                // ignore for now auto enabled for simplicity
                telemetry.AssertIntegration(IntegrationId.HttpSocketsHandler, enabled: IsUsingSocketHandler(instrumentation), autoEnabled: null);
                telemetry.AssertIntegration(IntegrationId.WinHttpHandler, enabled: IsUsingWinHttpHandler(instrumentation), autoEnabled: null);
                telemetry.AssertIntegration(IntegrationId.CurlHandler, enabled: IsUsingCurlHandler(instrumentation), autoEnabled: null);
                VerifyInstrumentation(processResult.Process);
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        [MemberData(nameof(IntegrationConfig))]
        public void TracingDisabled_DoesNotSubmitsTraces(InstrumentationOptions instrumentation, bool enableSocketsHandler)
        {
            SetInstrumentationVerification();
            ConfigureInstrumentation(instrumentation, enableSocketsHandler);

            const string expectedOperationName = "http.request";

            using var telemetry = this.ConfigureTelemetry();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent, arguments: $"TracingDisabled Port={httpPort}"))
            {
                var spans = agent.WaitForSpans(1, 2000, operationName: expectedOperationName);
                Assert.Equal(0, spans.Count);

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
