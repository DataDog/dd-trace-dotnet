// <copyright file="Elasticsearch6Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    [UsesVerify]
    public class Elasticsearch6Tests : TracingIntegrationTest
    {
        private const string ServiceName = "Samples.Elasticsearch";

        public Elasticsearch6Tests(ITestOutputHelper output)
            : base("Elasticsearch", output)
        {
            SetServiceName(ServiceName);
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.ElasticSearch6
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsElasticsearchNet(metadataSchemaVersion);

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{ServiceName}-elasticsearch" : ServiceName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var expected = new List<string>();
                var version = string.IsNullOrEmpty(packageVersion) ? null : new Version(packageVersion);

                // commands with sync and async
                for (var i = 0; i < 2; i++)
                {
                    expected.AddRange(new List<string>
                    {
                        "Bulk",
                        "Create",
                        "Search",
                        "DeleteByQuery",

                        "CreateIndex",
                        "IndexExists",
                        "UpdateIndexSettings",
                        "BulkAlias",
                        "GetAlias",
                        "PutAlias",
                        "AliasExists",
                        "DeleteAlias",
                        "DeleteAlias",
                        "CreateIndex",
                        "SplitIndex", // Only present on 6.1+
                        "DeleteIndex",
                        "CloseIndex",
                        "OpenIndex",
                        "PutIndexTemplate",
                        "IndexTemplateExists",
                        "DeleteIndexTemplate",
                        "IndicesShardStores",
                        "IndicesStats",
                        "DeleteIndex",
                        "GetAlias",

                        "CatAliases",
                        "CatAllocation",
                        "CatCount",
                        "CatFielddata",
                        "CatHealth",
                        "CatHelp",
                        "CatIndices",
                        "CatMaster",
                        "CatNodeAttributes",
                        "CatNodes",
                        "CatPendingTasks",
                        "CatPlugins",
                        "CatRecovery",
                        "CatRepositories",
                        "CatSegments",
                        "CatShards",
                        // "CatSnapshots",
                        "CatTasks",
                        "CatTemplates",
                        "CatThreadPool",

                        "PutJob",
                        "ValidateJob",
                        "GetInfluencers",
                        "GetJobs",
                        "GetJobStats",
                        "GetModelSnapshots",
                        "FlushJob",
                        "GetOverallBuckets", // Only present on 6.1+
                        "ForecastJob", // Only present on 6.1+
                        "GetAnomalyRecords",
                        "GetBuckets",
                        "GetCategories",
                        "CloseJob",
                        "OpenJob",
                        "DeleteJob",

                        "ClusterAllocationExplain",
                        "ClusterGetSettings",
                        "ClusterHealth",
                        "ClusterPendingTasks",
                        "ClusterPutSettings",
                        "ClusterReroute",
                        "ClusterState",
                        "ClusterStats",

                        "PutRole",
                        "PutRoleMapping",
                        "GetRole",
                        "GetRoleMapping",
                        "DeleteRoleMapping",
                        "DeleteRole",
                        "PutUser",
                        "ChangePassword",
                        "GetUser",
                        "DisableUser",
                        "DeleteUser",
                    });

                    // Remove spans that are only generated on 6.1+
                    // The default version is 6.1.0, so we do not remove spans when the version number is null
                    if (version is not null && version < new Version(6, 1, 0))
                    {
                        expected.Remove("SplitIndex");
                        expected.Remove("GetOverallBuckets");
                        expected.Remove("ForecastJob");
                    }
                }

                var spans = agent.WaitForSpans(expected.Count)
                                 .Where(s => s.Type == "elasticsearch")
                                 .OrderBy(s => s.Start)
                                 .ToList();

                var snapshotSuffix = version switch
                {
                    null => "6_1", // default is version 6.1.0
                    { Major: 6, Minor: >= 1 } => "6_1",
                    _ => "6_0"
                };

                var host = Environment.GetEnvironmentVariable("ELASTICSEARCH6_HOST");

                var settings = VerifyHelper.GetSpanVerifierSettings();
                // normalise between running directly against localhost and against elasticsearch containers
                settings.AddSimpleScrubber("out.host: localhost", "out.host: elasticsearch");
                settings.AddSimpleScrubber("out.host: elasticsearch6", "out.host: elasticsearch");
                settings.AddSimpleScrubber("out.host: elasticsearch7_arm64", "out.host: elasticsearch");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: elasticsearch");
                settings.AddSimpleScrubber("peer.service: elasticsearch6", "peer.service: elasticsearch");
                settings.AddSimpleScrubber("peer.service: elasticsearch7_arm64", "peer.service: elasticsearch");
                if (!string.IsNullOrWhiteSpace(host))
                {
                    settings.AddSimpleScrubber(host, "localhost:00000");
                }

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseTextForParameters($"packageVersion={snapshotSuffix}.Schema{metadataSchemaVersion.ToUpper()}")
                                  .DisableRequireUniquePrefix();

                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);
                ValidateSpans(spans, (span) => span.Resource, expected);
                telemetry.AssertIntegrationEnabled(IntegrationId.ElasticsearchNet);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public void IntegrationDisabled()
        {
            using var telemetry = this.ConfigureTelemetry();
            string packageVersion = PackageVersions.ElasticSearch6.First()[0] as string;
            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.ElasticsearchNet)}_ENABLED", "false");
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(1).Where(s => s.Type == "elasticsearch").ToList();

            Assert.Empty(spans);
            telemetry.AssertIntegrationDisabled(IntegrationId.ElasticsearchNet);
        }
    }
}
