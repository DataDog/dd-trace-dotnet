// <copyright file="PeerServiceMappingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(WebRequestTests))]
    public class PeerServiceMappingTests : TestHelper
    {
        public PeerServiceMappingTests(ITestOutputHelper output)
            : base("WebRequest", output)
        {
            SetEnvironmentVariable("DD_TRACE_PEER_SERVICE_DEFAULTS_ENABLED", "true");
            SetEnvironmentVariable("DD_TRACE_PEER_SERVICE_MAPPING", "127.0.0.1:my-custom-host,localhost:my-custom-host");
            SetServiceVersion("1.0.0");
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("SupportsInstrumentationVerification", "True")]
        public async Task RenamesService()
        {
            var expectedSpanCount = 87;

            SetInstrumentationVerification();
            const string expectedOperationName = "http.request";
            const string expectedPeerServiceName = "my-custom-host";
            const string expectedPeerServiceFromName = "localhost";

            int httpPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (var processResult = await RunSampleAndWaitForExit(agent, arguments: $"Port={httpPort}"))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);

                foreach (var span in spans)
                {
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(expectedPeerServiceName, span.GetTag(Tags.PeerService));
                    Assert.Equal(expectedPeerServiceFromName, span.GetTag(Tags.PeerServiceRemappedFrom));
                    Assert.Equal(SpanTypes.Http, span.Type);
                }

                VerifyInstrumentation(processResult.Process);
            }
        }
    }
}
