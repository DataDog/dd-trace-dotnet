// <copyright file="MongoDbTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class MongoDbTests : TestHelper
    {
        public MongoDbTests()
            : base("MongoDB")
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

        [TestCaseSource(nameof(GetMongoDb))]
        [Property("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion, bool enableCallTarget)
        {
            SetCallTargetSettings(enableCallTarget);

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(3, 500);
                Assert.True(spans.Count >= 3, $"Expecting at least 3 spans, only received {spans.Count}");

                var rootSpan = spans.Single(s => s.ParentId == null);

                // Check for manual trace
                Assert.AreEqual("Main()", rootSpan.Name);
                Assert.AreEqual("Samples.MongoDB", rootSpan.Service);
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
                        Assert.AreEqual("mongodb.query", span.Name);
                        Assert.AreEqual(SpanTypes.MongoDb, span.Type);
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
                        Assert.AreEqual("Samples.MongoDB", span.Service);
                        Assert.True("1.0.0" == span.Tags?.GetValueOrDefault(Tags.Version), span.ToString());
                    }
                }

                Assert.False(spansWithResourceName == 0, "Extraction of the command failed on all spans");
            }
        }
    }
}
