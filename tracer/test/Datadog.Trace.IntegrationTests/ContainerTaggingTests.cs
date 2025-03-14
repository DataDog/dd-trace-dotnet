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

                var expectedContainedId = ContainerMetadata.GetContainerId();
                if (expectedContainedId == null)
                {
                    // we don't extract the containerId in some cases, such as when running on Windows.
                    // In these cases, it should not appear in the headers at all.
                    headers.AllKeys.Select(x => x?.ToLower()).Should().NotContain(AgentHttpHeaderNames.ContainerId.ToLower());
                }
                else
                {
                    headers.AllKeys.ToDictionary(x => x?.ToLower(), x => headers[x]).Should().Contain(AgentHttpHeaderNames.ContainerId.ToLower(), expectedContainedId);
                }

                var expectedEntityId = ContainerMetadata.GetEntityId();
                if (expectedEntityId == null)
                {
                    // we don't extract the entityId in some cases, such as when running on Windows.
                    // In these cases, it should not appear in the headers at all.
                    headers.AllKeys.Select(x => x?.ToLower()).Should().NotContain(AgentHttpHeaderNames.EntityId.ToLower());
                }
                else
                {
                    headers.AllKeys.ToDictionary(x => x?.ToLower(), x => headers[x]).Should().Contain(AgentHttpHeaderNames.EntityId.ToLower(), expectedEntityId);
                }

                if (expectedContainedId is not null && expectedEntityId is not null)
                {
                    expectedEntityId.Should().Be($"cid-{expectedContainedId}");
                }
                else if (expectedEntityId is not null)
                {
                    // Note: This line hasn't been executed by CI yet
                    expectedEntityId.StartsWith("in-").Should().BeTrue();
                }
            }
        }
    }
}
