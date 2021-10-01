// <copyright file="WebRequest20Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET452
using System;
using System.Globalization;
using System.Linq;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class WebRequest20Tests : TestHelper
    {
        public WebRequest20Tests()
            : base("WebRequest.NetFramework20")
        {
            SetServiceVersion("1.0.0");
        }

        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [TestCase(false)]
        [TestCase(true)]
        public void SubmitsTraces(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            int expectedSpanCount = enableCallTarget ? 45 : 25; // CallSite insturmentation doesn't instrument async requests
            const string expectedOperationName = "http.request";
            const string expectedServiceName = "Samples.WebRequest.NetFramework20-http-client";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            Console.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Console.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"Port={httpPort}"))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.AreEqual(expectedSpanCount, spans.Count);

                foreach (var span in spans)
                {
                    Assert.AreEqual(expectedOperationName, span.Name);
                    Assert.AreEqual(expectedServiceName, span.Service);
                    Assert.AreEqual(SpanTypes.Http, span.Type);
                    Assert.AreEqual("WebRequest", span.Tags[Tags.InstrumentationName]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }

                var firstSpan = spans.First();
                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);

                Assert.AreEqual(firstSpan.TraceId.ToString(CultureInfo.InvariantCulture), traceId);
                Assert.AreEqual(firstSpan.SpanId.ToString(CultureInfo.InvariantCulture), parentSpanId);
            }
        }

        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [TestCase(false)]
        [TestCase(true)]
        public void TracingDisabled_DoesNotSubmitsTraces(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            const string expectedOperationName = "http.request";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"TracingDisabled Port={httpPort}"))
            {
                var spans = agent.WaitForSpans(1, 3000, operationName: expectedOperationName);
                Assert.AreEqual(0, spans.Count);

                var traceId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TraceId);
                var parentSpanId = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.ParentId);
                var tracingEnabled = StringUtil.GetHeader(processResult.StandardOutput, HttpHeaderNames.TracingEnabled);

                Assert.Null(traceId);
                Assert.Null(parentSpanId);
                Assert.AreEqual("false", tracingEnabled);
            }
        }
    }
}
#endif
