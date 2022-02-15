// <copyright file="MongoDbTests.cs" company="Datadog">
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
using FluentAssertions;
using FluentAssertions.Execution;
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
                        null or { Major: >= 3 } or { Major: 2, Minor: >= 7 } => "2_7", // default is version 2.8.0
                        { Major: 2, Minor: >= 2 } => "2_2",
                        _ => "PRE_2_2"
                    };

                var settings = VerifyHelper.GetSpanVerifierSettings();
                // mongo stamps the current framework version, and OS so normalise those
                settings.AddRegexScrubber(OsRegex, @"""os"" : {} ");
                // normalise between running directly against localhost and against mongo container
                settings.AddSimpleScrubber("out.host: localhost", "out.host: mongo");
                settings.AddSimpleScrubber("out.host: mongo_arm64", "out.host: mongo");
                // In some package versions, aggregate queries have an ID, others don't
                settings.AddSimpleScrubber("\"$group\" : { \"_id\" : null, \"n\"", "\"$group\" : { \"_id\" : 1, \"n\"");

                // The mongodb driver sends periodic monitors
                var adminSpans = spans
                                .Where(x => x.Resource is "buildInfo admin" or "getLastError admin")
                                .ToList();
                var nonAdminSpans = spans
                                   .Where(x => !adminSpans.Contains(x))
                                   .ToList();

                await VerifyHelper.VerifySpans(nonAdminSpans, settings)
                                  .UseTextForParameters($"packageVersion={snapshotSuffix}")
                                  .DisableRequireUniquePrefix();

                telemetry.AssertIntegrationEnabled(IntegrationId.MongoDb);

                // do some basic verification on the "admin" spans
                using var scope = new AssertionScope();
                adminSpans.Should().AllBeEquivalentTo(new { Service = "Samples.MongoDB-mongodb", Type = "mongodb", });
                foreach (var adminSpan in adminSpans)
                {
                    adminSpan.Tags.Should().IntersectWith(new Dictionary<string, string>
                    {
                        { "component", "MongoDb" },
                        { "db.name", "admin" },
                        { "env", "integration_tests" },
                        { "mongodb.collection", "1" },
                        { "span.kind", "client" },
                    });

                    if (adminSpan.Resource == "buildInfo admin")
                    {
                        adminSpan.Tags.Should().Contain("mongodb.query", "{ \"buildInfo\" : 1 }");
                    }
                    else
                    {
                        adminSpan.Tags.Should().Contain("mongodb.query", "{ \"getLastError\" : 1 }");
                    }
                }
            }
        }
    }
}
