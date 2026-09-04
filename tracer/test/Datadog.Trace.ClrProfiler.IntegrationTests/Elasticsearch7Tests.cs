// <copyright file="Elasticsearch7Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    [Trait("DockerGroup", "2")]
    [Collection(Elasticsearch7Collection.Name)]
    [UsesVerify]
    public class Elasticsearch7Tests : TracingIntegrationTest
    {
        private const string ServiceName = "Samples.Elasticsearch";
        private readonly Elasticsearch7Fixture _elasticsearchFixture;

        public Elasticsearch7Tests(ITestOutputHelper output, Elasticsearch7Fixture elasticsearchFixture)
            : base("Elasticsearch.V7", output)
        {
            _elasticsearchFixture = elasticsearchFixture;
            SetServiceName(ServiceName);
            SetServiceVersion("1.0.0");
            ConfigureContainers(elasticsearchFixture);
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsElasticsearchNet(metadataSchemaVersion);

        [SkippableTheory]
        [CombinatorialOrPairwiseData]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(
            [PackageVersionData(nameof(PackageVersions.ElasticSearch7))] string packageVersion,
            [MetadataSchemaVersionData] string metadataSchemaVersion)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{ServiceName}-elasticsearch" : ServiceName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var expected = new List<string>();

                // commands with sync and async
                for (var i = 0; i < 2; i++)
                {
                    expected.AddRange(new List<string>
                    {
                        "Bulk",
                        "Create",
                        "Create",
                        "Count",
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

                    if (string.IsNullOrEmpty(packageVersion) || string.Compare(packageVersion, "6.1.0", StringComparison.Ordinal) < 0)
                    {
                        expected.Remove("SplitIndex");
                        expected.Remove("GetOverallBuckets");
                        expected.Remove("ForecastJob");
                    }
                }

                var spans = (await agent.WaitForSpansAsync(expected.Count))
                                 .Where(s => s.Type == "elasticsearch")
                                 .OrderBy(s => s.Start)
                                 .ToList();

                var settings = VerifyHelper.GetSpanVerifierSettings();
                // Normalise the dynamically-mapped Testcontainers endpoint.
                settings.AddSimpleScrubber("out.host: localhost", "out.host: elasticsearch");
                settings.AddSimpleScrubber($"out.host: {_elasticsearchFixture.Host}", "out.host: elasticsearch");
                settings.AddSimpleScrubber($"out.port: {_elasticsearchFixture.Port}", "out.port: 9200");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: elasticsearch");
                settings.AddSimpleScrubber($"peer.service: {_elasticsearchFixture.Host}", "peer.service: elasticsearch");
                settings.AddSimpleScrubber(_elasticsearchFixture.HostAndPort, "localhost:00000");

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseTextForParameters($"Schema{metadataSchemaVersion.ToUpper()}")
                                  .DisableRequireUniquePrefix();

                ValidateIntegrationSpans(spans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);
                ValidateSpans(spans, (span) => span.Resource, expected);
                await telemetry.AssertIntegrationEnabledAsync(IntegrationId.ElasticsearchNet);
            }
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public async Task IntegrationDisabled()
        {
            using var telemetry = this.ConfigureTelemetry();
            string packageVersion = PackageVersions.ElasticSearch7.First()[0] as string;
            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.ElasticsearchNet)}_ENABLED", "false");
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
            var spans = (await agent.WaitForSpansAsync(1)).Where(s => s.Type == "elasticsearch").ToList();

            Assert.Empty(spans);
            await telemetry.AssertIntegrationDisabledAsync(IntegrationId.ElasticsearchNet);
        }
    }
}
