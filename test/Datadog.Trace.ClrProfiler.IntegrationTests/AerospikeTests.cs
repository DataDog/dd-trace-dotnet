// <copyright file="AerospikeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.TestHelpers;
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
            SetCallTargetSettings(enableCallTarget: true);
        }

        [Theory]
        [MemberData(nameof(PackageVersions.Aerospike), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitTraces(string packageVersion)
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

                var expectedSpans = new List<string>
                {
                    // Synchronous
                    "Write",
                    "Write",
                    "Write",
                    "Write",
                    "Read",
                    "Exists",
                    "Delete",

                    // Asynchronous
                    "Write",
                    "Write",
                    "Write",
                    "Write",
                    "Read",
                    "Exists",
                    "Delete",
                };

                var spans = agent.WaitForSpans(expectedSpans.Count)
                                 .Where(s => s.Type == "aerospike")
                                 .OrderBy(s => s.Start)
                                 .ToList();

                foreach (var span in spans)
                {
                    Assert.Equal("aerospike.command", span.Name);
                    Assert.Equal("Samples.Aerospike-aerospike", span.Service);
                    Assert.Equal("aerospike", span.Type);
                    Assert.Equal(SpanKinds.Client, span.Tags[Tags.SpanKind]);
                    Assert.Equal("test:myset:mykey:56cfdb28a0a21ba119f76e0ea4e528a1f406dd94", span.Tags[Tags.AerospikeKey]);

                    Assert.True(expectedSpans.Remove(span.Resource), $"Unexpected span resource: {span.Resource}");
                }

                Assert.Empty(expectedSpans);
            }
        }
    }
}
