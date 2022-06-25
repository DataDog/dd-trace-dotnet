// <copyright file="MicrosoftDataSqliteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet
{
    public class MicrosoftDataSqliteTests : TestHelper
    {
        public MicrosoftDataSqliteTests(ITestOutputHelper output)
            : base("Microsoft.Data.Sqlite", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.MicrosoftDataSqlite), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces(string packageVersion)
        {
#if NETCOREAPP3_0
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine")) // set in dockerfile
             && !string.IsNullOrEmpty(packageVersion)
             && new Version(packageVersion) >= new Version("6.0.0"))
            {
                Output.WriteLine("Skipping as Microsoft.Data.Sqlite hanqs on Alpine .NET Core 3.0 with 6.0.0 package");
                return;
            }
#endif
            const int expectedSpanCount = 91;
            const string dbType = "sqlite";
            const string expectedOperationName = dbType + ".query";
            const string expectedServiceName = "Samples.Microsoft.Data.Sqlite-" + dbType;

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);

            Assert.Equal(expectedSpanCount, spans.Count);

            foreach (var span in spans)
            {
                var result = span.IsSqlite();
                Assert.True(result.Success, result.ToString());

                Assert.Equal(expectedServiceName, span.Service);
                Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.Sqlite);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public void IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "sqlite.query";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.Sqlite)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            string packageVersion = PackageVersions.MicrosoftDataSqlite.First()[0] as string;
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.NotEmpty(spans);
            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.Sqlite);
        }
    }
}
