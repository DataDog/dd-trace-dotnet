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
using Datadog.Trace.TestHelpers.TestTracer;
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
                var settings = TracerSettings.Create(new()
                {
                    { ConfigurationKeys.AgentUri, $"http://localhost:{agent.Port}" },
                    { ConfigurationKeys.TraceDataPipelineEnabled, "false" }
                });
                await using var tracer = TracerHelper.Create(settings, agentWriter: null, sampler: null, scopeManager: null, statsd: null);

                using (var scope = tracer.StartActive("operationName"))
                {
                    scope.Span.ResourceName = "resourceName";
                }

                await tracer.FlushAsync();

                var spans = await agent.WaitForSpansAsync(count: 1);
                spans.Count.Should().Be(1);

                var headers = agent.TraceRequestHeaders.Should().ContainSingle().Subject;

                var expectedContainedId = ContainerMetadata.Instance.ContainerId;
                if (expectedContainedId == null)
                {
                    // we don't extract the containerId in some cases, such as when running on Windows.
                    // In these cases, it should not appear in the headers at all.
                    headers.AllKeys.Should().NotContain(AgentHttpHeaderNames.ContainerId);
                }
                else
                {
                    headers.AllKeys.ToDictionary(x => x, x => headers[x]).Should().Contain(AgentHttpHeaderNames.ContainerId, expectedContainedId);
                }

                var expectedEntitydId = ContainerMetadata.Instance.EntityId;
                if (expectedEntitydId == null)
                {
                    // we don't extract the entityId in some cases, such as when running on Windows.
                    // In these cases, it should not appear in the headers at all.
                    headers.AllKeys.Should().NotContain(AgentHttpHeaderNames.EntityId);
                }
                else
                {
                    headers.AllKeys.ToDictionary(x => x, x => headers[x]).Should().Contain(AgentHttpHeaderNames.EntityId, expectedEntitydId);
                }

                if (expectedContainedId is not null && expectedEntitydId is not null)
                {
                    expectedEntitydId.Should().Be($"ci-{expectedContainedId}");
                }
                else if (expectedEntitydId is not null)
                {
                    // Note: This line hasn't been executed by CI yet
                    expectedEntitydId.StartsWith("in-").Should().BeTrue();
                }
            }
        }
    }
}
