// <copyright file="AerospikeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AerospikeTests : TestHelper
    {
        public AerospikeTests(ITestOutputHelper output)
            : base("Aerospike", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Aerospike), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitTraces(string packageVersion)
        {
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                var expectedSpans = new List<string>
                {
                    // Synchronous
                    "Write",
                    "Write",
                    "Write",
                    "Write",
                    "Read",
                    "Exists",
                    "BatchGetArray",
                    "BatchExistsArray",
                    "QueryRecord",
                    "Delete",

                    // Asynchronous
                    "Write",
                    "Write",
                    "Write",
                    "Write",
                    "Read",
                    "Exists",
                    "BatchGetArray",
                    "BatchExistsArray",
                    "Delete",
                };

                var spans = agent.WaitForSpans(expectedSpans.Count)
                                 .Where(s => s.Type == "aerospike")
                                 .OrderBy(s => s.Start)
                                 .ToList();

                spans.Should()
                     .OnlyContain(span => span.Name == "aerospike.command")
                     .And.OnlyContain(span => span.Service == "Samples.Aerospike-aerospike")
                     .And.OnlyContain(span => span.Tags[Tags.SpanKind] == SpanKinds.Client)
                     .And.OnlyContain(span => ValidateSpanKey(span));

                spans.Select(span => span.Resource).Should().ContainInOrder(expectedSpans);
            }
        }

        private static bool ValidateSpanKey(MockTracerAgent.Span span)
        {
            if (span.Resource.Contains("Batch"))
            {
                return span.Tags[Tags.AerospikeKey] == "test:myset1:mykey1;test:myset2:mykey2;test:myset3:mykey3";
            }
            else if (span.Resource.Contains("Record"))
            {
                return span.Tags[Tags.AerospikeKey] == "test:myset1"
                    && span.Tags[Tags.AerospikeNamespace] == "test"
                    && span.Tags[Tags.AerospikeSetName] == "myset1";
            }
            else
            {
                return span.Tags[Tags.AerospikeKey] == "test:myset1:mykey1"
                    && span.Tags[Tags.AerospikeNamespace] == "test"
                    && span.Tags[Tags.AerospikeSetName] == "myset1"
                    && span.Tags[Tags.AerospikeUserKey] == "mykey1";
            }
        }
    }
}
