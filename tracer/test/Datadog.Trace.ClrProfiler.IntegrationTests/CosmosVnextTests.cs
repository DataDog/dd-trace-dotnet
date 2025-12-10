// <copyright file="CosmosVnextTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [UsesVerify]
    public class CosmosVnextTests : TracingIntegrationTest, IAsyncLifetime
    {
        private const string ExpectedOperationName = "cosmosdb.query";

        public CosmosVnextTests(ITestOutputHelper output)
            : base("CosmosDb.Vnext", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.CosmosDbVnext
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsCosmosDb(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        // vnext emulator only supports queries on items
        public async Task SubmitTracesQuery(string packageVersion, string metadataSchemaVersion)
        {
            var expectedSpanCount = 4;

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("TEST_MODE", "Query");
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-cosmosdb" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
                var spans = await agent.WaitForSpansAsync(expectedSpanCount, operationName: ExpectedOperationName);
                spans.Count.Should().BeGreaterOrEqualTo(expectedSpanCount, $"Expecting at least {expectedSpanCount} spans, only received {spans.Count}");

                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                var settings = VerifyHelper.GetSpanVerifierSettings();

                // Normalize cosmosdb host between localhost, x64, and ARM64
                settings.AddSimpleScrubber("out.host: https://localhost:00000/", "out.host: https://cosmosdb-emulator:8081/");
                settings.AddSimpleScrubber("out.host: https://cosmosdb-emulator_arm64:8081/", "out.host: https://cosmosdb-emulator:8081/");
                settings.AddSimpleScrubber("out.host: localhost", "out.host: cosmosdb-emulator");
                settings.AddSimpleScrubber("out.host: cosmosdb-emulator_arm64", "out.host: cosmosdb-emulator");

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseTextForParameters($"Schema{metadataSchemaVersion.ToUpper()}")
                                  .DisableRequireUniquePrefix();

                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.CosmosDb);
            }
        }

        public async Task InitializeAsync()
        {
            // For some reason, the emulator needs a warm up run when piloted by the x86 client
            if (!EnvironmentTools.IsTestTarget64BitProcess())
            {
                using var agent = EnvironmentHelper.GetMockAgent();
                using var processResult = await RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}");
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
