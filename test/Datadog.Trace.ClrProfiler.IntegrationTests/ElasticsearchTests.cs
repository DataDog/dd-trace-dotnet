using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ElasticsearchTests : TestHelper
    {
        private const int AgentPort = 9005;

        public ElasticsearchTests(ITestOutputHelper output)
            : base("Elasticsearch", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            using (var agent = new MockTracerAgent(AgentPort))
            using (var processResult = RunSampleAndWaitForExit(AgentPort))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var expected = new List<string>
                {
                     "Bulk",
                     "Create",
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
                     "SplitIndex",
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
                     "CatSnapshots",
                     "CatTasks",
                     "CatTemplates",
                     "CatThreadPool",

                     "PutJob",
                     "ValidateJob",
                     "GetInfluencers",
                     "GetJobs",
                     "GetJobStats",
                     "GetModelSnapshots",
                     "GetOverallBuckets",
                     "FlushJob",
                     "ForecastJob",
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
                };

                var spans = agent.WaitForSpans(expected.Count).
                    Where(s => s.Type == "elasticsearch").
                    OrderBy(s => s.Start).
                    ToList();

                foreach (var span in spans)
                {
                    Assert.Equal("elasticsearch.query", span.Name);
                    Assert.Equal("Samples.Elasticsearch-elasticsearch", span.Service);
                    Assert.Equal("elasticsearch", span.Type);
                }

                ValidateSpans(spans, (span) => span.Resource, expected);
            }
        }
    }
}
