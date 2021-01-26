using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Core.Tools;
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

        internal static IEnumerable<InliningOptions> InliningOptionsValues =>
            new List<InliningOptions>
            {
                new InliningOptions(enableCallTarget: false, enableInlining: false),
                new InliningOptions(enableCallTarget: true, enableInlining: false),
                new InliningOptions(enableCallTarget: true, enableInlining: true),
            };

        internal static IEnumerable<InstrumentationOptions> InstrumentationOptionsValues =>
            new List<InstrumentationOptions>
            {
                new InstrumentationOptions(instrumentSocketHandler: false, instrumentWinHttpHandler: false),
                new InstrumentationOptions(instrumentSocketHandler: false, instrumentWinHttpHandler: true),
                new InstrumentationOptions(instrumentSocketHandler: true, instrumentWinHttpHandler: false),
                new InstrumentationOptions(instrumentSocketHandler: true, instrumentWinHttpHandler: true),
            };

        public static IEnumerable<object[]> IntegrationConfig() =>
            from inliningOptions in InliningOptionsValues
            from instrumentationOptions in InstrumentationOptionsValues
            from socketHandlerEnabled in new[] { true, false }
            select new object[] { inliningOptions, instrumentationOptions, socketHandlerEnabled };

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(IntegrationConfig))]
        public void HttpClient_SubmitsTraces(InliningOptions inlining, InstrumentationOptions instrumentation, bool enableSocketsHandler)
        {
            ConfigureInstrumentation(inlining, instrumentation, enableSocketsHandler);

            var expectedSpanCount = CalculateExpectedAsyncSpans(instrumentation, inlining.EnableCallTarget);

            const string expectedOperationName = "http.request";
            const string expectedServiceName = "Samples.HttpMessageHandler-http-client";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

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

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(IntegrationConfig))]
        public void TracingDisabled_DoesNotSubmitsTraces(InliningOptions inlining, InstrumentationOptions instrumentation, bool enableSocketsHandler)
        {
            ConfigureInstrumentation(inlining, instrumentation, enableSocketsHandler);

            const string expectedOperationName = "http.request";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"TracingDisabled Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

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
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            // net4x doesn't have patch
            var spansPerHttpClient = EnvironmentHelper.IsCoreClr() ? 35 : 31;

            var expectedSpanCount = spansPerHttpClient * 2; // default HttpClient and CustomHttpClientHandler

#if !NET452
            // WinHttpHandler instrumentation is off by default, and only available on Windows
            if (enableCallTarget && isWindows && (instrumentation.InstrumentWinHttpHandler ?? false))
            {
                expectedSpanCount += spansPerHttpClient;
            }
#endif

            // SocketsHttpHandler instrumentation is on by default
            if (EnvironmentHelper.IsCoreClr() && (instrumentation.InstrumentSocketHandler ?? true))
            {
                expectedSpanCount += spansPerHttpClient;
            }

            return expectedSpanCount;
        }

        private void ConfigureInstrumentation(InliningOptions inlining, InstrumentationOptions instrumentation, bool enableSocketsHandler)
        {
            SetCallTargetSettings(inlining.EnableCallTarget, inlining.EnableInlining);

            // Should HttpClient try to use HttpSocketsHandler
            SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER", enableSocketsHandler ? "1" : "0");

            // Enable specific integrations, or use defaults
            if (instrumentation.InstrumentSocketHandler.HasValue)
            {
                SetEnvironmentVariable("DD_HttpSocketsHandler_ENABLED", instrumentation.InstrumentSocketHandler.Value ? "true" : "false");
            }

            if (instrumentation.InstrumentWinHttpHandler.HasValue)
            {
                SetEnvironmentVariable("DD_WinHttpHandler_ENABLED", instrumentation.InstrumentWinHttpHandler.Value ? "true" : "false");
            }
        }

        public class InliningOptions
        {
            internal InliningOptions(
                bool enableCallTarget,
                bool enableInlining)
            {
                EnableCallTarget = enableCallTarget;
                EnableInlining = enableInlining;
            }

            internal bool EnableCallTarget { get; }

            internal bool EnableInlining { get; }

            public override string ToString() =>
                $"EnableCallTarget={EnableCallTarget},EnableInlining={EnableInlining}";
        }

        public class InstrumentationOptions
        {
            internal InstrumentationOptions(
                bool? instrumentSocketHandler,
                bool? instrumentWinHttpHandler)
            {
                InstrumentSocketHandler = instrumentSocketHandler;
                InstrumentWinHttpHandler = instrumentWinHttpHandler;
            }

            internal bool? InstrumentSocketHandler { get; }

            internal bool? InstrumentWinHttpHandler { get; }

            public override string ToString() =>
                $"InstrumentSocketHandler={InstrumentSocketHandler},InstrumentWinHttpHandler={InstrumentWinHttpHandler}";
        }
    }
}
