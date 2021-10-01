// <copyright file="ServiceMappingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [NonParallelizable]
    public class ServiceMappingTests : TestHelper
    {
        public ServiceMappingTests()
            : base("WebRequest")
        {
            SetEnvironmentVariable("DD_TRACE_SERVICE_MAPPING", "some-trace:not-used,http-client:my-custom-client");
            SetServiceVersion("1.0.0");
        }

        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [TestCase(false)]
        [TestCase(true)]
        public void RenamesService(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            var (ignoreAsync, expectedSpanCount) = (EnvironmentHelper.IsCoreClr(), enableCallTarget) switch
            {
                (false, false) => (true, 28), // .NET Framework CallSite instrumentation doesn't cover Async / TaskAsync operations
                _ => (false, 74)
            };

            const string expectedOperationName = "http.request";
            const string expectedServiceName = "my-custom-client";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();
            var extraArgs = ignoreAsync ? "IgnoreAsync " : string.Empty;

            Console.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Console.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, arguments: $"{extraArgs}Port={httpPort}"))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.AreEqual(expectedSpanCount, spans.Count);

                foreach (var span in spans)
                {
                    Assert.AreEqual(expectedOperationName, span.Name);
                    Assert.AreEqual(expectedServiceName, span.Service);
                    Assert.AreEqual(SpanTypes.Http, span.Type);
                    Assert.That(span.Tags[Tags.InstrumentationName], Is.EqualTo("WebRequest").Or.EqualTo("HttpMessageHandler"));
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }
    }
}
