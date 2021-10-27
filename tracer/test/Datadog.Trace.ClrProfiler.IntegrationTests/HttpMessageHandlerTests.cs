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
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition(nameof(HttpMessageHandlerTests), DisableParallelization = true)]
    public class HttpMessageHandlerTests : TestHelper
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
                new InstrumentationOptions(instrumentSocketHandler: false, instrumentWinHttpOrCurlHandler: false),
                new InstrumentationOptions(instrumentSocketHandler: false, instrumentWinHttpOrCurlHandler: true),
                new InstrumentationOptions(instrumentSocketHandler: true, instrumentWinHttpOrCurlHandler: false),
                new InstrumentationOptions(instrumentSocketHandler: true, instrumentWinHttpOrCurlHandler: true),
            };

        public static IEnumerable<object[]> IntegrationConfig() =>
            from instrumentationOptions in InstrumentationOptionsValues
            from socketHandlerEnabled in new[] { true, false }
            select new object[] { true, instrumentationOptions, socketHandlerEnabled };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(IntegrationConfig))]
        public void HttpClient_SubmitsTraces(bool enableCallTarget, InstrumentationOptions instrumentation, bool enableSocketsHandler)
        {
            ConfigureInstrumentation(enableCallTarget, instrumentation, enableSocketsHandler);

            var expectedAsyncCount = CalculateExpectedAsyncSpans(instrumentation, enableCallTarget);
            var expectedSyncCount = CalculateExpectedSyncSpans(instrumentation);

            var expectedSpanCount = expectedAsyncCount + expectedSyncCount;

            const string expectedOperationName = "http.request";
            const string expectedServiceName = "Samples.HttpMessageHandler-http-client";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"Port={httpPort}"))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);

                foreach (var span in spans)
                {
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(expectedServiceName, span.Service);
                    Assert.Equal(SpanTypes.Http, span.Type);
                    Assert.Equal("HttpMessageHandler", span.Tags[Tags.InstrumentationName]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");

                    if (span.Tags[Tags.HttpStatusCode] == "502")
                    {
                        Assert.Equal(1, span.Error);
                    }
                }

                var firstSpan = spans.First();
                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);

                Assert.Equal(firstSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                Assert.Equal(firstSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(IntegrationConfig))]
        public void TracingDisabled_DoesNotSubmitsTraces(bool enableCallTarget, InstrumentationOptions instrumentation, bool enableSocketsHandler)
        {
            ConfigureInstrumentation(enableCallTarget, instrumentation, enableSocketsHandler);

            const string expectedOperationName = "http.request";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"TracingDisabled Port={httpPort}"))
            {
                var spans = agent.WaitForSpans(1, 2000, operationName: expectedOperationName);
                Assert.Equal(0, spans.Count);

                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);
                var tracingEnabled = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TracingEnabled);

                Assert.Null(traceId);
                Assert.Null(parentSpanId);
                Assert.Equal("false", tracingEnabled);
            }
        }

        private static int CalculateExpectedAsyncSpans(InstrumentationOptions instrumentation, bool enableCallTarget)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

            // net4x doesn't have patch
            var spansPerHttpClient = EnvironmentHelper.IsCoreClr() ? 35 : 31;

            var expectedSpanCount = spansPerHttpClient * 2; // default HttpClient and CustomHttpClientHandler

#if !NET452
            // WinHttpHandler instrumentation is off by default, and only available on Windows
            if (enableCallTarget && isWindows && (instrumentation.InstrumentWinHttpOrCurlHandler ?? false))
            {
                expectedSpanCount += spansPerHttpClient;
            }
#endif

            // SocketsHttpHandler instrumentation is on by default
            if (EnvironmentHelper.IsCoreClr() && (instrumentation.InstrumentSocketHandler ?? true))
            {
                expectedSpanCount += spansPerHttpClient;
            }

#if NETCOREAPP2_1 || NETCOREAPP3_0 || NETCOREAPP3_1
            if (enableCallTarget && instrumentation.InstrumentWinHttpOrCurlHandler == true)
            {
                // Add 1 span for internal WinHttpHandler and CurlHandler using the HttpMessageInvoker
                expectedSpanCount++;
            }
#endif

            return expectedSpanCount;
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

        private void ConfigureInstrumentation(bool enableCallTarget, InstrumentationOptions instrumentation, bool enableSocketsHandler)
        {
            SetCallTargetSettings(enableCallTarget);

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
