// <copyright file="ContainerTaggingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.IntegrationTests
{
    public class ContainerTaggingTests
    {
        private readonly ITestOutputHelper _output;

        public ContainerTaggingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableFact]
        public async Task Http_Headers_Contain_ContainerId()
        {
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = MockTracerAgent.Create(_output, agentPort))
            {
                var settings = new TracerSettings { Exporter = new ExporterSettings() { AgentUri = new Uri($"http://localhost:{agent.Port}") } };
                var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);

                using (var scope = tracer.StartActive("operationName"))
                {
                    scope.Span.ResourceName = "resourceName";
                }

                await tracer.FlushAsync();

                var spans = agent.WaitForSpans(count: 1);
                Assert.Equal(expected: 1, spans.Count);

                // we don't extract the containerId on Windows (yet?)
                Assert.Equal(expected: 1, agent.TraceRequestHeaders.Count);
                var expectedContainedId = EnvironmentTools.IsWindows() ? null : ContainerMetadata.GetContainerId();
                Assert.All(agent.TraceRequestHeaders, headers => Assert.Equal(expectedContainedId, headers[AgentHttpHeaderNames.ContainerId]));
            }
        }
    }
}
