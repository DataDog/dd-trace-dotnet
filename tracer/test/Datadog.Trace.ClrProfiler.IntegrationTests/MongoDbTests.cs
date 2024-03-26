// <copyright file="MongoDbTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class MongoDbTests : TracingIntegrationTest
    {
        private static readonly Regex OsRegex = new(@"""os"" : \{.*?\} ");
        private static readonly Regex ObjectIdRegex = new(@"ObjectId\("".*?""\)");

        public MongoDbTests(ITestOutputHelper output)
            : base("MongoDB", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.MongoDB
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsMongoDb(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-mongodb" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(3, 500);

                var version = string.IsNullOrEmpty(packageVersion) ? null : new Version(packageVersion);
                var snapshotSuffix = version switch
                {
                    null => "2_7", // default is version 2.8.0
                    { Major: >= 3 } or { Major: 2, Minor: >= 15 } => "2_15", // A bunch of stuff was removed in 2.15.0
                    { Major: 2, Minor: >= 7 } => "2_7", // default is version 2.8.0
                    { Major: 2, Minor: >= 5 } => "2_5", // version 2.5 + 2.6 include additional info on queries compared to 2.2
                    { Major: 2, Minor: >= 2 } => "2_2",
                    _ => "PRE_2_2"
                };

                var settings = VerifyHelper.GetSpanVerifierSettings();
                // mongo stamps the current framework version, and OS so normalise those
                settings.AddRegexScrubber(OsRegex, @"""os"" : {} ");
                // v2.5.x records additional info in the insert query which is execution-specific
                settings.AddRegexScrubber(ObjectIdRegex, @"ObjectId(""ABC123"")");
                // normalise between running directly against localhost and against mongo container
                settings.AddSimpleScrubber("out.host: localhost", "out.host: mongo");
                settings.AddSimpleScrubber("out.host: mongo_arm64", "out.host: mongo");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: mongo");
                settings.AddSimpleScrubber("peer.service: mongo_arm64", "peer.service: mongo");
                // In some package versions, aggregate queries have an ID, others don't
                settings.AddSimpleScrubber("\"$group\" : { \"_id\" : null, \"n\"", "\"$group\" : { \"_id\" : 1, \"n\"");
                // In 2.19, The explain query includes { "$expr" : true }, whereas in earlier versions it doesn't
                settings.AddSimpleScrubber("{ \"$expr\" : true }", "{ }");

                // The mongodb driver sends periodic monitors
                var adminSpans = spans
                                .Where(x => x.Resource is "buildInfo admin" or "getLastError admin")
                                .ToList();
                var nonAdminSpans = spans
                                   .Where(x => !adminSpans.Contains(x))
                                   .ToList();
                var allMongoSpans = spans
                                    .Where(x => x.GetTag(Tags.InstrumentationName) == "MongoDb")
                                    .ToList();

                await VerifyHelper.VerifySpans(nonAdminSpans, settings)
                                  .UseTextForParameters($"packageVersion={snapshotSuffix}.Schema{metadataSchemaVersion.ToUpper()}")
                                  .DisableRequireUniquePrefix();

                ValidateIntegrationSpans(allMongoSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);

                telemetry.AssertIntegrationEnabled(IntegrationId.MongoDb);

                // do some basic verification on the "admin" spans
                using var scope = new AssertionScope();
                adminSpans.Should().AllBeEquivalentTo(new { Service = clientSpanServiceName, Type = "mongodb", });
                foreach (var adminSpan in adminSpans)
                {
                    adminSpan.Tags.Should().IntersectWith(new Dictionary<string, string>
                    {
                        { "db.name", "admin" },
                        { "env", "integration_tests" },
                        { "mongodb.collection", "1" },
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
