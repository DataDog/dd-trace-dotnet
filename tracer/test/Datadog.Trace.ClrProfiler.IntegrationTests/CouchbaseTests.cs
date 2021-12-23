// <copyright file="CouchbaseTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class CouchbaseTests : TestHelper
    {
        public CouchbaseTests(ITestOutputHelper output)
            : base("Couchbase", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static System.Collections.Generic.IEnumerable<object[]> GetCouchbase()
        {
            foreach (var item in PackageVersions.Couchbase)
            {
                yield return item.ToArray();
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetCouchbase))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitTraces(string packageVersion)
        {
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(13, 500);
                Assert.True(spans.Count >= 13, $"Expecting at least 13 spans, only received {spans.Count}");

                foreach (var span in spans)
                {
                    Assert.Equal("couchbase.query", span.Name);
                    Assert.Equal("Samples.Couchbase-couchbase", span.Service);
                    Assert.Equal("db", span.Type);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }

                var expected = new List<string>
                {
                    "GetClusterConfig", "Get", "Set", "Get", "Add", "Replace", "Delete",
                    "Get", "Set", "Get", "Add", "Replace", "Delete"
                };

                ValidateSpans(spans, (span) => span.Resource, expected);
            }
        }
    }
}
