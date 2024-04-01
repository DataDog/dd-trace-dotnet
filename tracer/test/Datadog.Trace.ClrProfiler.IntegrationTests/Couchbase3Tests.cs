// <copyright file="Couchbase3Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    [Trait("RequiresDockerDependency", "true")]
    public class Couchbase3Tests : TracingIntegrationTest
    {
        public Couchbase3Tests(ITestOutputHelper output)
            : base("Couchbase3", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.Couchbase3
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsCouchbase(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public async Task SubmitTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-couchbase" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(10, 500)
                                 .Where(s => s.Type == "db")
                                 .ToList();

                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                using var scope = new AssertionScope();

                var settings = VerifyHelper.GetSpanVerifierSettings();

                // this is a random id
                settings.AddRegexScrubber(new Regex(@"couchbase.operation.key: {.*},"), "couchbase.operation.key: obfuscated,");
                // normalise between running directly against localhost and against couchbase container
                settings.AddSimpleScrubber("db.couchbase.seed.nodes: localhost", "db.couchbase.seed.nodes: couchbase");
                settings.AddSimpleScrubber("out.host: localhost", "out.host: couchbase");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: couchbase");

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
                telemetry.AssertIntegrationEnabled(IntegrationId.Couchbase);
            }
        }

        private static string GetVersionSuffix(string packageVersion)
        {
            var version = new Version(string.IsNullOrEmpty(packageVersion) ? "3.4.1" : packageVersion); // default version in csproj
            if (version < new Version("3.1.2"))
            {
                return "_3_0";
            }

            if (version < new Version("3.2.0"))
            {
                return "_3_1";
            }

            if (version < new Version("3.2.6"))
            {
                return "_3_2";
            }

            if (version < new Version("3.3.0"))
            {
                return "_3_2_6";
            }

            if (version <= new Version("3.4.0"))
            {
                return "_3_4";
            }

            return string.Empty;
        }
    }
}
