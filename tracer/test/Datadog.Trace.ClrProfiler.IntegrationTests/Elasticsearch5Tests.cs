// <copyright file="Elasticsearch5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    public class Elasticsearch5Tests : TestHelper
    {
        public Elasticsearch5Tests(ITestOutputHelper output)
            : base("Elasticsearch.V5", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.ElasticSearch5), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces(string packageVersion)
        {
            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var expected = new List<string>();

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
                        // "AliasExists",
                        "DeleteAlias",
                        "DeleteAlias",
                        "CreateIndex",
                        // "SplitIndex",
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

                        // "PutJob",
                        // "ValidateJob",
                        // "GetInfluencers",
                        // "GetJobs",
                        // "GetJobStats",
                        // "GetModelSnapshots",
                        // "GetOverallBuckets",
                        // "FlushJob",
                        // "ForecastJob",
                        // "GetAnomalyRecords",
                        // "GetBuckets",
                        // "GetCategories",
                        // "CloseJob",
                        // "OpenJob",
                        // "DeleteJob",

                        "ClusterAllocationExplain",
                        "ClusterGetSettings",
                        "ClusterHealth",
                        "ClusterPendingTasks",
                        "ClusterPutSettings",
                        "ClusterReroute",
                        "ClusterState",
                        "ClusterStats",

                        "PutRole",
                        // "PutRoleMapping",
                        "GetRole",
                        // "GetRoleMapping",
                        // "DeleteRoleMapping",
                        "DeleteRole",
                        "PutUser",
                        "ChangePassword",
                        "GetUser",
                        // "DisableUser",
                        "DeleteUser",
                    });
                }

                var spans = agent.WaitForSpans(expected.Count)
                                 .Where(s => s.Type == "elasticsearch")
                                 .OrderBy(s => s.Start)
                                 .ToList();

                foreach (var span in spans)
                {
                    var result = span.IsElasticsearchNet();
                    Assert.True(result.Success, result.ToString());

                    Assert.Equal("Samples.Elasticsearch.V5-elasticsearch", span.Service);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }

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
            string packageVersion = PackageVersions.ElasticSearch5.First()[0] as string;
            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.ElasticsearchNet)}_ENABLED", "false");

            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = agent.WaitForSpans(1).Where(s => s.Type == "elasticsearch").ToList();

            Assert.Empty(spans);
            telemetry.AssertIntegrationDisabled(IntegrationId.ElasticsearchNet);
        }
    }
}
