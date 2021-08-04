// <copyright file="ServiceMappingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(WebRequestTests))]
    public class ServiceMappingTests : TestHelper
    {
        public ServiceMappingTests(ITestOutputHelper output)
            : base("WebRequest", output)
        {
            SetEnvironmentVariable("DD_TRACE_SERVICE_MAPPING", "some-trace:not-used,http-client:my-custom-client");
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [InlineData(false)]
        [InlineData(true)]
        public void RenamesService(bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            var (ignoreAsync, expectedSpanCount) = (EnvironmentHelper.IsCoreClr(), enableCallTarget) switch
            {
                (false, false) => (true, 28), // .NET Framework CallSite instrumentation doesn't cover Async / TaskAsync operations
                _ => (false, 73)
            };

            const string expectedOperationName = "http.request";
            const string expectedServiceName = "my-custom-client";

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();
            var extraArgs = ignoreAsync ? "IgnoreAsync " : string.Empty;

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"{extraArgs}Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);

                foreach (var span in spans)
                {
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(expectedServiceName, span.Service);
                    Assert.Equal(SpanTypes.Http, span.Type);
                    Assert.Matches("WebRequest|HttpMessageHandler", span.Tags[Tags.InstrumentationName]);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }
    }
}
