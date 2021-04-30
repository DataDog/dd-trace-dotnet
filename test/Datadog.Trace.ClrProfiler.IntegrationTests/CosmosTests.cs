#if !NET452
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Core.Tools;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class CosmosTests : TestHelper
    {
        private const string ExpectedOperationName = "cosmosdb.query";
        private const string ExpectedServiceName = "Samples.CosmosDb-cosmosdb";

        public CosmosTests(ITestOutputHelper output)
            : base("CosmosDb", output)
        {
            SetServiceVersion("1.0.0");

            // for some reason, the elumator needs a warm up run when piloted by the x86 client
            if (!Environment.Is64BitProcess)
            {
                int agentPort = TcpPortProvider.GetOpenPort();
                using var agent = new MockTracerAgent(agentPort);
                using var processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"{TestPrefix}");
            }
        }

        public static IEnumerable<object[]> GetCosmosVersions()
        {
            foreach (object[] item in PackageVersions.CosmosDb)
            {
                yield return item.ToArray();
            }
        }

        [Theory]
        [MemberData(nameof(GetCosmosVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces(string packageVersion)
        {
            SetCallTargetSettings(true);

            var expectedSpanCount = 14;

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
                processResult.ExitCode.Should().Be(0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(expectedSpanCount, operationName: ExpectedOperationName);
                spans.Count.Should().BeGreaterOrEqualTo(expectedSpanCount, $"Expecting at least {expectedSpanCount} spans, only received {spans.Count}");

                var dbTags = 0;
                var containerTags = 0;

                foreach (var span in spans)
                {
                    span.Name.Should().Be(ExpectedOperationName);
                    span.Service.Should().Be(ExpectedServiceName);
                    span.Type.Should().Be(SpanTypes.CosmosDb);
                    span.Resource.Should().StartWith("SELECT * FROM");
                    span.Tags.Should().NotContain(Tags.Version, "External service span should not have service version tag.");
                    span.Tags.Should().Contain(new KeyValuePair<string, string>(Tags.OutHost, "https://localhost:8081/"));

                    if (span.Tags.ContainsKey(Tags.CosmosDbContainer))
                    {
                        span.Tags.Should().Contain(new KeyValuePair<string, string>(Tags.CosmosDbContainer, "items"));
                        containerTags++;
                    }

                    if (span.Tags.ContainsKey(Tags.CosmosDbDatabase))
                    {
                        span.Tags.Should().Contain(new KeyValuePair<string, string>(Tags.CosmosDbDatabase, "db"));
                        dbTags++;
                    }
                }

                dbTags.Should().Be(10);
                containerTags.Should().Be(4);
            }
        }
    }
}
#endif
