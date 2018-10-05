using System;
using System.Collections.Generic;
using System.Linq;
using Nest;


namespace Samples.Elasticsearch
{
    public class Program
    {
        static void Main(string[] args)
        {
            var localhost = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(localhost).DefaultIndex("elastic-net-example");
            var elastic = new ElasticClient(settings);
            // elastic.Analyze(new AnalyzeRequest("test_index"));
            // elastic.CancelTasks
            // elastic.ClearCache(new ClearCacheRequest());
            // elastic.ClearCachedRealms(new ClearCachedRealmsRequest("test_realm"));
            // elastic.ClearCachedRoles(new ClearCachedRolesRequest("test_role"));
            // elastic.ClearScroll(new ClearScrollRequest("scroll_id"));
            // elastic.CreateRepository(new CreateRepositoryRequest("test_create_repository"));
            // elastic.DeleteDatafeed
            // elastic.DeleteExpiredData
            // elastic.DeleteLicense(new DeleteLicenseRequest());
            // elastic.DeleteModelSnapshot
            // elastic.DeletePipeline
            // elastic.DeleteRepository(new DeleteRepositoryRequest("test_delete_repository"));
            // elastic.DeleteScript(new DeleteScriptRequest("test_script"));
            // elastic.DeleteSnapshot(new DeleteSnapshotRequest("test_repository", "test_snapshot"));
            // elastic.DeprecationInfo(new DeprecationInfoRequest());
            // elastic.FieldCapabilities(new FieldCapabilitiesRequest());
            // elastic.GetDatafeeds(new GetDatafeedsRequest());
            // elastic.GetDatafeedStats(new GetDatafeedStatsRequest("test_datafeed"));
            // elastic.GetFieldMapping(new GetFieldMappingRequest("test_index", "Post", "Title"));
            // elastic.GetLicense(new GetLicenseRequest());
            // elastic.GetMapping(new GetMappingRequest());
            // elastic.GetPipeline(new GetPipelineRequest());
            // elastic.GetRepository(new GetRepositoryRequest());
            // elastic.GetScript(new GetScriptRequest("test_script"));
            // elastic.GetSnapshot(new GetSnapshotRequest("test_repository", "test_snapshot"));
            // elastic.GetTask(new GetTaskRequest("test_task"));
            // elastic.GetTrialLicenseStatus(new GetTrialLicenseStatusRequest());
            // elastic.GraphExplore(new GraphExploreRequest("test_index"));
            // elastic.GrokProcessorPatterns(new GrokProcessorPatternsRequest());
            // // elastic.InvalidateUserAccessToken
            // elastic.ListTasks(new ListTasksRequest());
            // elastic.Map(new PutMappingRequest("test_index", "Post"));
            // elastic.MigrationAssistance(new MigrationAssistanceRequest());
            // elastic.MigrationUpgrade(new MigrationUpgradeRequest("test_index"));

            var commands = new List<Func<object>>().
                Concat(IndexCommands(elastic)).
                Concat(CatCommands(elastic)).
                Concat(JobCommands(elastic)).
                Concat(ClusterCommands(elastic)).
                Concat(UserCommands(elastic)).
                Concat(WatchCommands(elastic)).
                Concat(DocumentCommands(elastic));
            foreach (var action in commands)
            {
                try
                {
                    Console.WriteLine($"{action()}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                }
            }
            // elastic.MultiTermVectors
            // elastic.NodesHotThreads
            // elastic.NodesInfo
            // elastic.NodesStats
            // elastic.NodesUsage
            // elastic.Ping
            // elastic.PostLicense
            // elastic.PreviewDatafeed
            // elastic.PutDatafeed
            // elastic.PutPipeline
            // elastic.PutScript
            // elastic.RecoveryStatus
            // elastic.Refresh
            // elastic.Reindex
            // elastic.ReindexOnServer
            // elastic.RemoteInfo
            // elastic.Restore
            // elastic.RestoreObservable
            // elastic.Rethrottle
            // elastic.RevertModelSnapshot
            // elastic.RolloverIndex
            // elastic.RootNodeInfo
            // elastic.Scroll
            // elastic.ScrollAll
            // elastic.Segments
            // elastic.ShrinkIndex
            // elastic.SimulatePipeline
            // elastic.Snapshot
            // elastic.SnapshotObservable
            // elastic.SnapshotStatus
            // elastic.StartDatafeed
            // elastic.StartTrialLicense
            // elastic.StopDatafeed
            // elastic.Suffix
            // elastic.SyncedFlush
            // elastic.TermVectors
            // elastic.UpdateDatafeed
            // elastic.UpdateModelSnapshot
            // elastic.Upgrade
            // elastic.UpgradeStatus
            // elastic.ValidateDetector
            // elastic.ValidateQuery
            // elastic.VerifyRepository
            // elastic.XPackInfo
            // elastic.XPackUsage
        }

        private static List<Func<object>> DocumentCommands(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.Bulk(new BulkRequest("test_index")
                {
                    Operations = new List<IBulkOperation>
                    {
                        new BulkCreateOperation<Post>(new Post
                        {
                            Id = 1,
                            Title = "BulkCreateOperation",
                        })
                    }
                }),
                () => elastic.Create<Post>(new CreateRequest<Post>(new Post
                {
                    Id = 2,
                    Title = "CreateRequest",
                }, "test_index")),
                () => elastic.CreateDocument<Post>(new Post
                {
                    Id = 3,
                    Title = "CreateDocument",
                }),
                () => elastic.DeleteByQuery(new DeleteByQueryRequest("test_index")
                {
                    Size = 0,
                }),
                // () => elastic.Count<Post>(new CountRequest()),
                // () => elastic.Delete(new DeleteRequest("test_index", "Post", 2)),
                // () => elastic.MultiGet
                // () => elastic.MultiSearch
                // () => elastic.MultiSearchTemplate
                // () => elastic.Update
                // () => elastic.UpdateByQuery
                // () => elastic.TypeExists

                // () => elastic.Source
                // () => elastic.SourceExists
                // () => elastic.SourceMany
                // () => elastic.Search
                // () => elastic.SearchShards
                // () => elastic.SearchTemplate
                // () => elastic.RenderSearchTemplate
                // () => elastic.Index<Post>(new IndexRequest<Post>(new Post
                // {
                //     Id = 4,
                //     Title = "Index",
                // })),
                // () => elastic.IndexDocument<Post>(new Post
                // {
                //     Id = 5,
                //     Title = "IndexDocument",
                // }),
                // () => elastic.IndexMany()
                // () => elastic.GetMany
                // () => elastic.Flush(new FlushRequest()),
                // () => elastic.ForceMerge(new ForceMergeRequest("test_force_merge")),
                // () => elastic.Get<Post>(new GetRequest("test_index", "Post", 1)),
                // () => elastic.DocumentExists(new DocumentExistsRequest("test_index", "Post", 2)),
            // () => elastic.Explain<Post>(new ExplainRequest<Post>("test_index", "Post", 1)),
            };
        }

        private static List<Func<object>> IndexCommands(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.CreateIndex("test_index_1"),
                () => elastic.IndexExists("test_index_1"),
                () => elastic.UpdateIndexSettings(new UpdateIndexSettingsRequest("test_index_1")
                {
                    IndexSettings = new IndexSettings()
                    {
                        Sorting = new SortingSettings
                        {
                            Fields = new Field("Title"),
                        },
                    },
                }),
                () => elastic.Alias(new BulkAliasRequest
                {
                    Actions = new List<IAliasAction>
                    {
                        new AliasAddAction
                        {
                            Add = new AliasAddOperation
                            {
                                Index = "test_index_1",
                                Alias = "test_index_2",
                            },
                        },
                    },
                }),
                () => elastic.GetAliasesPointingToIndex("test_index_1"),
                () => elastic.PutAlias("test_index_1", "test_index_3"),
                () => elastic.AliasExists(new AliasExistsRequest("test_index_1")),
                () => elastic.DeleteAlias(new DeleteAliasRequest("test_index_1", "test_index_3")),
                () => elastic.DeleteAlias(new DeleteAliasRequest("test_index_1", "test_index_2")),
                () => elastic.CreateIndex("test_index_4"),
                () => elastic.SplitIndex("test_index_1", "test_index_4"),
                () => elastic.DeleteIndex("test_index_4"),
                () => elastic.CloseIndex("test_index_1"),
                () => elastic.OpenIndex("test_index_1"),
                () => elastic.PutIndexTemplate(new PutIndexTemplateRequest("test_template_1")),
                () => elastic.IndexTemplateExists("test_template_1"),
                () => elastic.DeleteIndexTemplate("test_template_1"),
                () => elastic.IndicesShardStores(),
                () => elastic.IndicesStats("test_index_1"),
                () => elastic.DeleteIndex("test_index_1"),
                () => elastic.GetAlias(new GetAliasRequest()),
            // elastic.GetIndex(new GetIndexRequest("test_index"));
            // elastic.GetIndexSettings(new GetIndexSettingsRequest());
            // elastic.GetIndexTemplate(new GetIndexTemplateRequest());
            // elastic.GetIndicesPointingToAlias("test_alias");
            };
        }

        private static List<Func<object>> CatCommands(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.CatAliases(new CatAliasesRequest()),
                () => elastic.CatAllocation(new CatAllocationRequest()),
                () => elastic.CatCount(new CatCountRequest()),
                () => elastic.CatFielddata(new CatFielddataRequest()),
                () => elastic.CatHealth(new CatHealthRequest()),
                () => elastic.CatHelp(new CatHelpRequest()),
                () => elastic.CatIndices(new CatIndicesRequest()),
                () => elastic.CatMaster(new CatMasterRequest()),
                () => elastic.CatNodeAttributes(new CatNodeAttributesRequest()),
                () => elastic.CatNodes(new CatNodesRequest()),
                () => elastic.CatPendingTasks(new CatPendingTasksRequest()),
                () => elastic.CatPlugins(new CatPluginsRequest()),
                () => elastic.CatRecovery(new CatRecoveryRequest()),
                () => elastic.CatRepositories(new CatRepositoriesRequest()),
                () => elastic.CatSegments(new CatSegmentsRequest()),
                () => elastic.CatShards(new CatShardsRequest()),
                () => elastic.CatSnapshots(new CatSnapshotsRequest()),
                () => elastic.CatTasks(new CatTasksRequest()),
                () => elastic.CatTemplates(new CatTemplatesRequest()),
                () => elastic.CatThreadPool(new CatThreadPoolRequest()),
            };
        }

        private static List<Func<object>> JobCommands(ElasticClient elastic)
        {
            // elastic.UpdateJob
            // elastic.PostJobData
            return new List<Func<object>>
            {
                () => elastic.PutJob(new PutJobRequest("test_job")),
                () => elastic.ValidateJob(new ValidateJobRequest()),
                () => elastic.GetInfluencers(new GetInfluencersRequest("test_job")),
                () => elastic.GetJobs(new GetJobsRequest("test_job")),
                () => elastic.GetJobStats(new GetJobStatsRequest()),
                () => elastic.GetModelSnapshots(new GetModelSnapshotsRequest("test_job")),
                () => elastic.GetOverallBuckets(new GetOverallBucketsRequest("test_job")),
                () => elastic.FlushJob(new FlushJobRequest("test_job")),
                () => elastic.ForecastJob(new ForecastJobRequest("test_job")),
                () => elastic.GetAnomalyRecords(new GetAnomalyRecordsRequest("test_job")),
                () => elastic.GetBuckets(new GetBucketsRequest("test_job")),
                () => elastic.GetCategories(new GetCategoriesRequest("test_job")),
                () => elastic.CloseJob(new CloseJobRequest("test_job")),
                () => elastic.OpenJob(new OpenJobRequest("test_job")),
                () => elastic.DeleteJob(new DeleteJobRequest("test_job")),
            };
        }

        private static List<Func<object>> ClusterCommands(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.ClusterAllocationExplain(new ClusterAllocationExplainRequest()),
                () => elastic.ClusterGetSettings(new ClusterGetSettingsRequest()),
                () => elastic.ClusterHealth(new ClusterHealthRequest()),
                () => elastic.ClusterPendingTasks(new ClusterPendingTasksRequest()),
                () => elastic.ClusterPutSettings(new ClusterPutSettingsRequest()),
                () => elastic.ClusterReroute(new ClusterRerouteRequest()),
                () => elastic.ClusterState(new ClusterStateRequest()),
                () => elastic.ClusterStats(new ClusterStatsRequest()),
            };
        }

        private static List<Func<object>> UserCommands(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.PutRole("test_role_1"),
                () => elastic.PutRoleMapping("test_role_1"),
                () => elastic.GetRole(new GetRoleRequest("test_role_1")),
                () => elastic.GetRoleMapping(new GetRoleMappingRequest("test_role_1")),
                () => elastic.DeleteRoleMapping("test_role_1"),
                () => elastic.DeleteRole("test_role_1"),
                () => elastic.PutUser("test_user_1"),
                () => elastic.ChangePassword(new ChangePasswordRequest("test_user_1")
                {
                    Password = "supersecret",
                }),
                () => elastic.GetUser(new GetUserRequest("test_user_1")),
                () => elastic.DisableUser("test_user_1"),
                () => elastic.DeleteUser("test_user_1"),
                // elastic.Authenticate
                // elastic.GetUserAccessToken(new GetUserAccessTokenRequest())
                // elastic.DeleteRole(new DeleteRoleRequest("test_role"));
                // elastic.DeleteRoleMapping(new DeleteRoleMappingRequest("test_role_mapping"));
            // elastic.EnableUser(new EnableUserRequest("test_user"));
            };
        }

        public static List<Func<object>> WatchCommands(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                // elastic.AcknowledgeWatch()
                // elastic.ActivateWatch()
                // elastic.PutWatch
                // // elastic.DeactivateWatch
                // elastic.DeleteWatch(new DeleteWatchRequest("test_watch"));
                // elastic.ExecuteWatch(new ExecuteWatchRequest());
                // elastic.GetWatch(new GetWatchRequest("test_watch"));
                // elastic.RestartWatcher
                // elastic.WatcherStats
                // elastic.StopWatcher
                // elastic.StartWatcher
            };
        }

        public class Post
        {
            public int Id { get; set; }
            public string Title { get; set; }
        }
    }
}
