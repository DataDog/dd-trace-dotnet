// <copyright file="MongoDbTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
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

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.MongoDB), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion)
        {
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(3, 500);
                spans.Count.Should().BeGreaterOrEqualTo(3);

                var rootSpan = spans.Single(s => s.ParentId == null);

                // Check for manual trace
                rootSpan.Name.Should().Be("Main()");
                rootSpan.Service.Should().Be("Samples.MongoDB");
                rootSpan.Type.Should().BeNull();

                int spansWithResourceName = 0;

                foreach (var span in spans)
                {
                    if (span == rootSpan)
                    {
                        continue;
                    }

                    if (span.Service == "Samples.MongoDB-mongodb")
                    {
                        span.Name.Should().Be("mongodb.query");
                        span.Type.Should().Be(SpanTypes.MongoDb);
                        span.Tags.Should().NotContainKey(Tags.Version, "external service span should not have service version tag.");

                        if (span.Resource != null && span.Resource != "mongodb.query")
                        {
                            spansWithResourceName++;

                            span.Tags.Should().ContainKey(Tags.MongoDbQuery);
                            span.Resource.Should().NotStartWith("isMaster").And.NotStartWith("hello");
                        }
                    }
                    else
                    {
                        // These are manual traces
                        span.Service.Should().Be("Samples.MongoDB");
                        span.Tags[Tags.Version].Should().Be("1.0.0");
                    }
                }

                spansWithResourceName.Should().BeGreaterThan(0, "extraction of the command failed on all spans");
            }
        }
    }
}
