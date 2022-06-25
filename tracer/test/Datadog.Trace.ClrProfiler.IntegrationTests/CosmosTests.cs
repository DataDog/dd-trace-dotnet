// <copyright file="CosmosTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
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
                using var agent = EnvironmentHelper.GetMockAgent();
                using var processResult = RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}");
            }
        }

        [SkippableTheory(Skip = "Cosmos emulator is too flaky at the moment")]
        [MemberData(nameof(PackageVersions.CosmosDb), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("Category", "LinuxUnsupported")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces(string packageVersion)
        {
            var expectedSpanCount = 14;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: ExpectedOperationName);
                spans.Count.Should().BeGreaterOrEqualTo(expectedSpanCount, $"Expecting at least {expectedSpanCount} spans, only received {spans.Count}");

                Output.WriteLine($"spans.Count: {spans.Count}");

                foreach (var span in spans)
                {
                    Output.WriteLine(span.ToString());
                }

                var dbTags = 0;
                var containerTags = 0;

                foreach (var span in spans)
                {
                    var result = span.IsCosmosDb();
                    Assert.True(result.Success, result.ToString());

                    span.Service.Should().Be(ExpectedServiceName);
                    span.Resource.Should().StartWith("SELECT * FROM");
                    span.Tags.Should().NotContain(Tags.Version, "External service span should not have service version tag.");

                    if (span.Tags.ContainsKey(Tags.CosmosDbContainer))
                    {
                        span.Tags.Should().Contain(new KeyValuePair<string, string>(Tags.CosmosDbContainer, "items"));
                        containerTags++;
                    }

                    if (span.Tags.ContainsKey(Tags.DbName))
                    {
                        span.Tags.Should().Contain(new KeyValuePair<string, string>(Tags.DbName, "db"));
                        dbTags++;
                    }
                }

                dbTags.Should().Be(10);
                containerTags.Should().Be(4);
                telemetry.AssertIntegrationEnabled(IntegrationId.CosmosDb);
            }
        }
    }
}
