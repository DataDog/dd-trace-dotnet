using System.Collections.Generic;
using System.Linq;
using Datadog.Core.Tools;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#if !NET452

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class Elasticsearch5Tests : TestHelper
    {
        public Elasticsearch5Tests(ITestOutputHelper output)
            : base("Elasticsearch.V5", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static System.Collections.Generic.IEnumerable<object[]> GetElasticsearch()
        {
            foreach (var item in PackageVersions.ElasticSearch5)
            {
                yield return item.Concat(false, false);
                yield return item.Concat(true, false);
                yield return item.Concat(true, true);
            }
        }

        [Theory]
        [MemberData(nameof(GetElasticsearch))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces(string packageVersion, bool enableCallTarget, bool enableInlining)
        {
            SetCallTargetSettings(enableCallTarget, enableInlining);

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode == 0, $"Process exited with code {processResult.ExitCode}");

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
                    Assert.Equal("elasticsearch.query", span.Name);
                    Assert.Equal("Samples.Elasticsearch.V5-elasticsearch", span.Service);
                    Assert.Equal("elasticsearch", span.Type);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }

                ValidateSpans(spans, (span) => span.Resource, expected);
            }
        }
    }
}

#endif
