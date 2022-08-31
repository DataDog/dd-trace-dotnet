// <copyright file="ProcessStartTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ProcessStartTests : TestHelper
    {
        public ProcessStartTests(ITestOutputHelper output)
            : base("ProcessStart", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.MongoDB), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces(string packageVersion)
        {
            const int expectedSpanCount = 5;
            const string expectedOperationName = "command_execution";
            const string expectedServiceName = "Samples.ProcessStart-command";

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);

            Assert.Equal(expectedSpanCount, spans.Count);

            foreach (var span in spans)
            {
                var result = span.IsProcessStart();
                Assert.True(result.Success, result.ToString());
                Assert.Equal(SpanTypes.System, span.Type);
                Assert.Equal(span.Name, expectedOperationName);
                Assert.Contains(".exe", span.Resource.ToLower());
                Assert.Equal(expectedServiceName, span.Service);
                Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.ProcessStart);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "ArmUnsupported")]
        public void IntegrationDisabled()
        {
            const int totalSpanCount = 21;
            const string expectedOperationName = "command_execution";

            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.ProcessStart)}_ENABLED", "false");

            using var telemetry = this.ConfigureTelemetry();
            string packageVersion = PackageVersions.MicrosoftDataSqlite.First()[0] as string;
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(totalSpanCount, returnAllOperations: true);

            Assert.Empty(spans.Where(s => s.Name.Equals(expectedOperationName)));
            telemetry.AssertIntegrationDisabled(IntegrationId.ProcessStart);
        }
    }
}
