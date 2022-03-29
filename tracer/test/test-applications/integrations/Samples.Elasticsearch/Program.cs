using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Nest;


namespace Samples.Elasticsearch
{
    public class Program
    {
        static void Main(string[] args)
        {
            var host = new Uri("http://" + Host());
            var settings = new ConnectionSettings(host).DefaultIndex("elastic-net-example");
            var elastic = new ElasticClient(settings);

            var commands = new List<Func<object>>().
                Concat(IndexCommands(elastic)).
                Concat(IndexCommandsAsync(elastic)).
                Concat(DocumentCommands(elastic)).
                Concat(DocumentCommandsAsync(elastic)).
                Concat(CatCommands(elastic)).
                Concat(CatCommandsAsync(elastic)).
                Concat(JobCommands(elastic)).
                Concat(JobCommandsAsync(elastic)).
                Concat(ClusterCommands(elastic)).
                Concat(ClusterCommandsAsync(elastic)).
                Concat(UserCommands(elastic)).
                Concat(UserCommandsAsync(elastic)).
                Concat(WatchCommands(elastic));

            var exceptions = new List<Exception>();

            foreach (var action in commands)
            {
                try
                {
                    var result = action();
                    if (result is Task task)
                    {
                        result = TaskResult(task);
                    }

                    Console.WriteLine($"{result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions).Flatten();
            }
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("ELASTICSEARCH6_HOST") ?? "localhost:9200";
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
                () => elastic.Count<Post>(),
                () => elastic.Search<Post>(s => s.MatchAll()),
                () => elastic.DeleteByQuery(new DeleteByQueryRequest("test_index")
                {
                    Size = 0,
                }),
            };
        }

        private static List<Func<object>> DocumentCommandsAsync(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.BulkAsync(new BulkRequest("test_index")
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
                () => elastic.CreateAsync<Post>(new CreateRequest<Post>(new Post
                {
                    Id = 2,
                    Title = "CreateRequest",
                }, "test_index")),
                () => elastic.CreateDocumentAsync<Post>(new Post
                {
                    Id = 3,
                    Title = "CreateDocument",
                }),
                () => elastic.CountAsync<Post>(),
                () => elastic.SearchAsync<Post>(s => s.MatchAll()),
                () => elastic.DeleteByQueryAsync(new DeleteByQueryRequest("test_index")
                {
                    Size = 0,
                }),
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
#if (ELASTICSEARCH_6_1 && !DEFAULT_SAMPLES)
                () => elastic.SplitIndex("test_index_1", "test_index_4"),
#endif
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
            };
        }

        private static List<Func<object>> IndexCommandsAsync(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.CreateIndexAsync("test_index_1"),
                () => elastic.IndexExistsAsync("test_index_1"),
                () => elastic.UpdateIndexSettingsAsync(new UpdateIndexSettingsRequest("test_index_1")
                {
                    IndexSettings = new IndexSettings()
                    {
                        Sorting = new SortingSettings
                        {
                            Fields = new Field("Title"),
                        },
                    },
                }),
                () => elastic.AliasAsync(new BulkAliasRequest
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
                () => elastic.GetAliasesPointingToIndexAsync("test_index_1"),
                () => elastic.PutAliasAsync("test_index_1", "test_index_3"),
                () => elastic.AliasExistsAsync(new AliasExistsRequest("test_index_1")),
                () => elastic.DeleteAliasAsync(new DeleteAliasRequest("test_index_1", "test_index_3")),
                () => elastic.DeleteAliasAsync(new DeleteAliasRequest("test_index_1", "test_index_2")),
                () => elastic.CreateIndexAsync("test_index_4"),
#if (ELASTICSEARCH_6_1 && !DEFAULT_SAMPLES)
                () => elastic.SplitIndexAsync("test_index_1", "test_index_4"),
#endif
                () => elastic.DeleteIndexAsync("test_index_4"),
                () => elastic.CloseIndexAsync("test_index_1"),
                () => elastic.OpenIndexAsync("test_index_1"),
                () => elastic.PutIndexTemplateAsync(new PutIndexTemplateRequest("test_template_1")),
                () => elastic.IndexTemplateExistsAsync("test_template_1"),
                () => elastic.DeleteIndexTemplateAsync("test_template_1"),
                () => elastic.IndicesShardStoresAsync(),
                () => elastic.IndicesStatsAsync("test_index_1"),
                () => elastic.DeleteIndexAsync("test_index_1"),
                () => elastic.GetAliasAsync(new GetAliasRequest()),
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
                // CatSnapshots is failing with a JSON deserialization error.
                // It might be a bug in the client or an incompatibility between client
                // and server versions.
                // () => elastic.CatSnapshots(new CatSnapshotsRequest()),
                () => elastic.CatTasks(new CatTasksRequest()),
                () => elastic.CatTemplates(new CatTemplatesRequest()),
                () => elastic.CatThreadPool(new CatThreadPoolRequest()),
            };
        }

        private static List<Func<object>> CatCommandsAsync(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.CatAliasesAsync(new CatAliasesRequest()),
                () => elastic.CatAllocationAsync(new CatAllocationRequest()),
                () => elastic.CatCountAsync(new CatCountRequest()),
                () => elastic.CatFielddataAsync(new CatFielddataRequest()),
                () => elastic.CatHealthAsync(new CatHealthRequest()),
                () => elastic.CatHelpAsync(new CatHelpRequest()),
                () => elastic.CatIndicesAsync(new CatIndicesRequest()),
                () => elastic.CatMasterAsync(new CatMasterRequest()),
                () => elastic.CatNodeAttributesAsync(new CatNodeAttributesRequest()),
                () => elastic.CatNodesAsync(new CatNodesRequest()),
                () => elastic.CatPendingTasksAsync(new CatPendingTasksRequest()),
                () => elastic.CatPluginsAsync(new CatPluginsRequest()),
                () => elastic.CatRecoveryAsync(new CatRecoveryRequest()),
                () => elastic.CatRepositoriesAsync(new CatRepositoriesRequest()),
                () => elastic.CatSegmentsAsync(new CatSegmentsRequest()),
                () => elastic.CatShardsAsync(new CatShardsRequest()),
                // CatSnapshots is failing with a JSON deserialization error.
                // It might be a bug in the client or an incompatibility between client
                // and server versions.
                // () => elastic.CatSnapshotsAsync(new CatSnapshotsRequest()),
                () => elastic.CatTasksAsync(new CatTasksRequest()),
                () => elastic.CatTemplatesAsync(new CatTemplatesRequest()),
                () => elastic.CatThreadPoolAsync(new CatThreadPoolRequest()),
            };
        }

        private static List<Func<object>> JobCommands(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.PutJob(new PutJobRequest("test_job")),
                () => elastic.ValidateJob(new ValidateJobRequest()),
                () => elastic.GetInfluencers(new GetInfluencersRequest("test_job")),
                () => elastic.GetJobs(new GetJobsRequest("test_job")),
                () => elastic.GetJobStats(new GetJobStatsRequest()),
                () => elastic.GetModelSnapshots(new GetModelSnapshotsRequest("test_job")),
                () => elastic.FlushJob(new FlushJobRequest("test_job")),
#if (ELASTICSEARCH_6_1 && !DEFAULT_SAMPLES)
                () => elastic.GetOverallBuckets(new GetOverallBucketsRequest("test_job")),
                () => elastic.ForecastJob(new ForecastJobRequest("test_job")),
#endif
                () => elastic.GetAnomalyRecords(new GetAnomalyRecordsRequest("test_job")),
                () => elastic.GetBuckets(new GetBucketsRequest("test_job")),
                () => elastic.GetCategories(new GetCategoriesRequest("test_job")),
                () => elastic.CloseJob(new CloseJobRequest("test_job")),
                () => elastic.OpenJob(new OpenJobRequest("test_job")),
                () => elastic.DeleteJob(new DeleteJobRequest("test_job")),
            };
        }

        private static List<Func<object>> JobCommandsAsync(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.PutJobAsync(new PutJobRequest("test_job")),
                () => elastic.ValidateJobAsync(new ValidateJobRequest()),
                () => elastic.GetInfluencersAsync(new GetInfluencersRequest("test_job")),
                () => elastic.GetJobsAsync(new GetJobsRequest("test_job")),
                () => elastic.GetJobStatsAsync(new GetJobStatsRequest()),
                () => elastic.GetModelSnapshotsAsync(new GetModelSnapshotsRequest("test_job")),
                () => elastic.FlushJobAsync(new FlushJobRequest("test_job")),
#if (ELASTICSEARCH_6_1 && !DEFAULT_SAMPLES)
                () => elastic.GetOverallBucketsAsync(new GetOverallBucketsRequest("test_job")),
                () => elastic.ForecastJobAsync(new ForecastJobRequest("test_job")),
#endif
                () => elastic.GetAnomalyRecordsAsync(new GetAnomalyRecordsRequest("test_job")),
                () => elastic.GetBucketsAsync(new GetBucketsRequest("test_job")),
                () => elastic.GetCategoriesAsync(new GetCategoriesRequest("test_job")),
                () => elastic.CloseJobAsync(new CloseJobRequest("test_job")),
                () => elastic.OpenJobAsync(new OpenJobRequest("test_job")),
                () => elastic.DeleteJobAsync(new DeleteJobRequest("test_job")),
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

        private static List<Func<object>> ClusterCommandsAsync(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.ClusterAllocationExplainAsync(new ClusterAllocationExplainRequest()),
                () => elastic.ClusterGetSettingsAsync(new ClusterGetSettingsRequest()),
                () => elastic.ClusterHealthAsync(new ClusterHealthRequest()),
                () => elastic.ClusterPendingTasksAsync(new ClusterPendingTasksRequest()),
                () => elastic.ClusterPutSettingsAsync(new ClusterPutSettingsRequest()),
                () => elastic.ClusterRerouteAsync(new ClusterRerouteRequest()),
                () => elastic.ClusterStateAsync(new ClusterStateRequest()),
                () => elastic.ClusterStatsAsync(new ClusterStatsRequest()),
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
            };
        }

        private static List<Func<object>> UserCommandsAsync(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.PutRoleAsync("test_role_1"),
                () => elastic.PutRoleMappingAsync("test_role_1"),
                () => elastic.GetRoleAsync(new GetRoleRequest("test_role_1")),
                () => elastic.GetRoleMappingAsync(new GetRoleMappingRequest("test_role_1")),
                () => elastic.DeleteRoleMappingAsync("test_role_1"),
                () => elastic.DeleteRoleAsync("test_role_1"),
                () => elastic.PutUserAsync("test_user_1"),
                () => elastic.ChangePasswordAsync(new ChangePasswordRequest("test_user_1")
                {
                    Password = "supersecret",
                }),
                () => elastic.GetUserAsync(new GetUserRequest("test_user_1")),
                () => elastic.DisableUserAsync("test_user_1"),
                () => elastic.DeleteUserAsync("test_user_1"),
            };
        }

        public static List<Func<object>> WatchCommands(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                // elastic.AcknowledgeWatch()
                // elastic.ActivateWatch()
                // elastic.PutWatch
                // elastic.DeactivateWatch
                // elastic.DeleteWatch(new DeleteWatchRequest("test_watch"));
                // elastic.ExecuteWatch(new ExecuteWatchRequest());
                // elastic.GetWatch(new GetWatchRequest("test_watch"));
                // elastic.RestartWatcher
                // elastic.WatcherStats
                // elastic.StopWatcher
                // elastic.StartWatcher
            };
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
        // elastic.InvalidateUserAccessToken
        // elastic.ListTasks(new ListTasksRequest());
        // elastic.Map(new PutMappingRequest("test_index", "Post"));
        // elastic.MigrationAssistance(new MigrationAssistanceRequest());
        // elastic.MigrationUpgrade(new MigrationUpgradeRequest("test_index"));
        // elastic.GetIndex(new GetIndexRequest("test_index"));
        // elastic.GetIndexSettings(new GetIndexSettingsRequest());
        // elastic.GetIndexTemplate(new GetIndexTemplateRequest());
        // elastic.GetIndicesPointingToAlias("test_alias");
        // elastic.Authenticate
        // elastic.GetUserAccessToken(new GetUserAccessTokenRequest())
        // elastic.DeleteRole(new DeleteRoleRequest("test_role"));
        // elastic.DeleteRoleMapping(new DeleteRoleMappingRequest("test_role_mapping"));
        // elastic.EnableUser(new EnableUserRequest("test_user"));
        // elastic.UpdateJob
        // elastic.PostJobData
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

        private static object TaskResult(Task task)
        {
            task.Wait();
            var taskType = task.GetType();

            while (!taskType.Name.StartsWith("Task"))
            {
                taskType = taskType.BaseType;
            }

            bool isTaskOfT =
                taskType.IsGenericType
                && taskType.GetGenericTypeDefinition() == typeof(Task<>);


            return isTaskOfT ? taskType.GetProperty("Result")?.GetValue(task) : null;
        }

        public class Post
        {
            public int Id { get; set; }
            public string Title { get; set; }
        }
    }
}
