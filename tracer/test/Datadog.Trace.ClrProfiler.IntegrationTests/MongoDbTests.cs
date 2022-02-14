// <copyright file="MongoDbTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class MongoDbTests : TestHelper
    {
        private static readonly Regex OsRegex = new(@"""os"" : \{.*?\} ");

        public MongoDbTests(ITestOutputHelper output)
            : base("MongoDB", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.MongoDB), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion)
        {
            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(3, 500);

                var version = string.IsNullOrEmpty(packageVersion) ? null : new Version(packageVersion);
                var snapshotSuffix = version switch
                    {
                        null or { Major: >= 2, Minor: >= 7 } => "2_7", // default is version 2.8.0
                        { Major: >= 2, Minor: >= 2 and < 7 } => "2_2",
                        _ => "PRE_2_2"
                    };

                var settings = VerifyHelper.GetSpanVerifierSettings();
                // mongo stamps the current framework version, and OS so normalise those
                settings.AddRegexScrubber(OsRegex, @"""os"" : {} ");

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseTextForParameters($"packageVersion={snapshotSuffix}")
                                  .DisableRequireUniquePrefix();

                telemetry.AssertIntegrationEnabled(IntegrationId.MongoDb);
            }
        }
    }
}
