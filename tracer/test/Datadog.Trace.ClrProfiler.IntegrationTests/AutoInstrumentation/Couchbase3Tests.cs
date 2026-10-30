// <copyright file="Couchbase3Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "1")]
    [Collection(CouchbaseCollection.Name)]
    public class Couchbase3Tests : TracingIntegrationTest
    {
        private readonly CouchbaseFixture _couchbaseFixture;

        public Couchbase3Tests(ITestOutputHelper output, CouchbaseFixture couchbaseFixture)
            : base("Couchbase3", output)
        {
            _couchbaseFixture = couchbaseFixture;
            SetServiceVersion("1.0.0");
            ConfigureContainers(couchbaseFixture);
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsCouchbase(metadataSchemaVersion);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public async Task SubmitTraces(
            [PackageVersionData(nameof(PackageVersions.Couchbase3))] string packageVersion,
            [MetadataSchemaVersionData] string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);

            if (GetPackageVersion(packageVersion) < new Version(3, 2, 0))
            {
                // Early versions of tracing just called new Activity(), which results in activities with no source, so
                // our IgnoreActivityHandler fails to detect it: https://github.com/couchbase/couchbase-net-client/blob/3.1.0/src/Couchbase/Core/Diagnostics/Tracing/ActivityRequestTracer.cs#L24
                // for simplicity, just disable OTEL integration for these cases
                SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "false");
            }

            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-couchbase" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion, retryOnRuntime127957Race: false))
            {
                var spans = (await agent.WaitForSpansAsync(10, 500))
                           .Where(s => s.Type == "db")
                           .ToList();

                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                using var scope = new AssertionScope();

                var settings = VerifyHelper.GetSpanVerifierSettings();

                // this is a random id
                settings.AddRegexScrubber(new Regex(@"couchbase.operation.key: {.*},"), "couchbase.operation.key: obfuscated,");
                // Normalize the fixture's hostname for local and containerized runs.
                settings.AddRegexScrubber(new Regex($@"db.couchbase.seed.nodes: {Regex.Escape(_couchbaseFixture.Host)}(?::[0-9]+)?"), "db.couchbase.seed.nodes: couchbase");
                settings.AddSimpleScrubber($"out.host: {_couchbaseFixture.Host}", "out.host: couchbase");
                settings.AddRegexScrubber(new Regex($@"peer.service: {Regex.Escape(_couchbaseFixture.Host)}(?::[0-9]+)?"), "peer.service: couchbase");
                // .NET Framework exposes the async state machine instead of the original method signature.
                settings.AddRegexScrubber(
                    new Regex(@"at Couchbase\.Core\.ClusterNode\.(?:ExecuteOp\([^\r\n)]*\)|<ExecuteOp>d__\d+\.MoveNext\(\))"),
                    "at Couchbase.Core.ClusterNode.ExecuteOp(...)");

                // theres' a fair amount less in 3.0.7 - fewer spans, different terminology etc

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(Couchbase3Tests) + GetVersionSuffix(packageVersion) + $".Schema{metadataSchemaVersion.ToUpper()}")
                                  .DisableRequireUniquePrefix(); // testing multiple package versions may converge on one snapshot

                var expected = new List<string>
                {
                    "Hello", "Hello", "GetErrorMap", "GetErrorMap", "SelectBucket", "SelectBucket",
                    "Set", "Get", "Delete"
                };

                if (packageVersion == "3.0.7")
                {
                    expected.Remove("Get");
                    expected.Add("MultiLookup");
                }

                ValidateSpans(spans, (span) => span.Resource, expected);
                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.Couchbase);
            }
        }

        private static string GetVersionSuffix(string packageVersion)
        {
            var version = GetPackageVersion(packageVersion);
            return version switch
            {
                _ when version < new Version("3.1.0") => "_3_0",
                _ when version < new Version("3.1.3") => "_3_1",
                _ when version < new Version("3.1.5") => "_3_1_3",
                _ when version < new Version("3.2.0") => "_3_1_5",
                _ when version < new Version("3.2.6") => "_3_2",
                _ when version < new Version("3.2.9") => "_3_2_6",
                _ when version < new Version("3.4.1") => "_3_2_9",
                _ when version < new Version("3.8.0") => "_3_4_1",
                _ when version < new Version("3.9.1") => "_3_8",
                _ when version < new Version("3.9.4") => "_3_9_1",
                _ => string.Empty
            };
        }

        private static Version GetPackageVersion(string packageVersion)
            => new(string.IsNullOrEmpty(packageVersion) ? "3.4.1" : packageVersion); // default version in csproj
    }
}
