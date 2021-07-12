// <copyright file="MongoDbTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class MongoDbTests : TestHelper
    {
        public MongoDbTests(ITestOutputHelper output)
            : base("MongoDB", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static System.Collections.Generic.IEnumerable<object[]> GetMongoDb()
        {
            foreach (var item in PackageVersions.MongoDB)
            {
                yield return item.Concat(false);
                yield return item.Concat(true);
            }
        }

        [Theory]
        [MemberData(nameof(GetMongoDb))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion, bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(3, 500);
                Assert.True(spans.Count >= 3, $"Expecting at least 3 spans, only received {spans.Count}");

                var rootSpan = spans.Single(s => s.ParentId == null);

                // Check for manual trace
                Assert.Equal("Main()", rootSpan.Name);
                Assert.Equal("Samples.MongoDB", rootSpan.Service);
                Assert.Null(rootSpan.Type);

                int spansWithResourceName = 0;

                foreach (var span in spans)
                {
                    if (span == rootSpan)
                    {
                        continue;
                    }

                    if (span.Service == "Samples.MongoDB-mongodb")
                    {
                        Assert.Equal("mongodb.query", span.Name);
                        Assert.Equal(SpanTypes.MongoDb, span.Type);
                        Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");

                        if (span.Resource != null && span.Resource != "mongodb.query")
                        {
                            spansWithResourceName++;
                            Assert.True(span.Tags?.ContainsKey(Tags.MongoDbQuery), $"No query found on span {span}");
                        }
                    }
                    else
                    {
                        // These are manual traces
                        Assert.Equal("Samples.MongoDB", span.Service);
                        Assert.True("1.0.0" == span.Tags?.GetValueOrDefault(Tags.Version), span.ToString());
                    }
                }

                Assert.False(spansWithResourceName == 0, "Extraction of the command failed on all spans");
            }
        }
    }
}
