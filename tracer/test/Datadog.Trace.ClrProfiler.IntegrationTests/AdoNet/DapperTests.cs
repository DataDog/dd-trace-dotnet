// <copyright file="DapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    [Trait("RequiresDockerDependency", "true")]
    public class DapperTests : TracingIntegrationTest
    {
        private const string ServiceName = "Samples.Dapper";

        public DapperTests(ITestOutputHelper output)
            : base("Dapper", output)
        {
            SetServiceVersion("1.0.0");
        }

        // Assert Npgsql because the Dapper application uses Postgres for the actual client
        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsNpgsql();

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesV0() => RunTest("v0");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesV1() => RunTest("v1");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesWithNetStandardV0() => RunTest("v0");

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTracesWithNetStandardV1() => RunTest("v1");

        private void RunTest(string metadataSchemaVersion)
        {
            const int expectedSpanCount = 17;
            const string dbType = "postgres";
            const string expectedOperationName = dbType + ".query";

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{ServiceName}-{dbType}" : ServiceName;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                Assert.Equal(expectedSpanCount, spans.Count);
                ValidateIntegrationSpans(spans, expectedServiceName: clientSpanServiceName, isExternalSpan);
            }
        }
    }
}
