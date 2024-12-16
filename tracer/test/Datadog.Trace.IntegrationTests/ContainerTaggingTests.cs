// <copyright file="ContainerTaggingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
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
                var settings = TracerSettings.Create(new() { { ConfigurationKeys.AgentUri, $"http://localhost:{agent.Port}" } });
                var tracer = new Tracer(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);

                using (var scope = tracer.StartActive("operationName"))
                {
                    scope.Span.ResourceName = "resourceName";
                }

                await tracer.FlushAsync();

                var spans = agent.WaitForSpans(count: 1);
                Assert.Equal(expected: 1, spans.Count);

                var headers = agent.TraceRequestHeaders.Should().ContainSingle().Subject;
                var headerMap = headers.AllKeys.ToDictionary(x => x.ToLower(), x => headers[x]);

                var expectedContainedId = ContainerMetadata.GetContainerId();
                var expectedEntitydId = ContainerMetadata.GetEntityId();
                if (expectedContainedId is not null)
                {
                    expectedContainedId.Should().NotBeNullOrEmpty();
                    headerMap.Should().ContainKey(AgentHttpHeaderNames.ContainerId.ToLower());
                    var actualContainerId = headerMap[AgentHttpHeaderNames.ContainerId.ToLower()];
                    actualContainerId.Should().Be(expectedContainedId);
                }
                else if (expectedEntitydId is not null)
                {
                    expectedEntitydId.Should().NotBeNullOrEmpty();
                    headerMap.Should().ContainKey(AgentHttpHeaderNames.EntityId.ToLower());
                    var actualEntityId = headerMap[AgentHttpHeaderNames.EntityId.ToLower()];
                    actualEntityId.Should().Be(expectedEntitydId);
                }
                else
                {
                    // we don't extract the containerId in some cases, such as when running on Windows.
                    // In these cases, it should not appear in the headers at all.
                    headerMap.Should().NotContainKey(AgentHttpHeaderNames.ContainerId.ToLower());
                    headerMap.Should().NotContainKey(AgentHttpHeaderNames.EntityId.ToLower());
                }
            }
        }
    }
}
