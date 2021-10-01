// <copyright file="ContainerTaggingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.IntegrationTests
{
    public class ContainerTaggingTests
    {
        [Test]
        public async Task Http_Headers_Contain_ContainerId()
        {
            string expectedContainedId = ContainerMetadata.GetContainerId();
            string actualContainerId = null;
            var agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            {
                agent.RequestReceived += (sender, args) =>
                {
                    actualContainerId = args.Value.Request.Headers[AgentHttpHeaderNames.ContainerId];
                };

                var settings = new TracerSettings { AgentUri = new Uri($"http://localhost:{agent.Port}") };
                var tracer = new Tracer(settings);

                using (var scope = tracer.StartActive("operationName"))
                {
                    scope.Span.ResourceName = "resourceName";
                }

                await tracer.FlushAsync();

                var spans = agent.WaitForSpans(1);
                Assert.AreEqual(1, spans.Count);
                Assert.AreEqual(expectedContainedId, actualContainerId);

                if (EnvironmentTools.IsWindows())
                {
                    // we don't extract the containerId on Windows (yet?)
                    Assert.Null(actualContainerId);
                }
            }
        }
    }
}
