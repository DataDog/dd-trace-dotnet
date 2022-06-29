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

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public void RenamesService()
        {
            var expectedSpanCount = 76;

            SetInstrumentationVerification();
            const string expectedOperationName = "http.request";
            const string expectedServiceName = "my-custom-client";

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
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

                VerifyInstrumentation(processResult.Process);
            }
        }
    }
}
