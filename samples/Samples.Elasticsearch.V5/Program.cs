using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Nest;

namespace Samples.Elasticsearch.V5
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"ProfilerAttached: {Instrumentation.ProfilerAttached}");

            var host = new Uri("http://" + Host());

            var settings = new ConnectionSettings(host)
                          .DefaultIndex("elastic-net-example")
                          .BasicAuthentication("elastic", "changeme");

            var elastic = new ElasticClient(settings);

            var commands = new List<Func<object>>().
                Concat(IndexCommands(elastic)).
                Concat(IndexCommandsAsync(elastic)).
                Concat(DocumentCommands(elastic)).
                Concat(DocumentCommandsAsync(elastic)).
                Concat(CatCommands(elastic)).
                Concat(CatCommandsAsync(elastic)).
                Concat(ClusterCommands(elastic)).
                Concat(ClusterCommandsAsync(elastic)).
                Concat(UserCommands(elastic)).
                Concat(UserCommandsAsync(elastic));

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
            return Environment.GetEnvironmentVariable("ELASTICSEARCH5_HOST") ?? "localhost:9205";
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
                 () => elastic.Create<Post>(new Post
                 {
                     Id = 2,
                     Title = "Create",
                 }),
                // () => elastic.CreateDocument<Post>(new Post
                // {
                //     Id = 3,
                //     Title = "CreateDocument",
                // }), // V6 Feature
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
                () => elastic.CreateAsync<Post>(new Post
                {
                    Id = 2,
                    Title = "Create",
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
                        // V6 feature
                        // Sorting = new SortingSettings
                        // {
                        //     Fields = new Field("Title"),
                        // },
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
                // () => elastic.AliasExists(new AliasExistsRequest("test_index_1")), // TODO: enable
                () => elastic.DeleteAlias(new DeleteAliasRequest("test_index_1", "test_index_3")),
                () => elastic.DeleteAlias(new DeleteAliasRequest("test_index_1", "test_index_2")),
                () => elastic.CreateIndex("test_index_4"),
                // () => elastic.SplitIndex("test_index_1", "test_index_4"), // V6 Feature
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
                        // V6 Feature
                        // Sorting = new SortingSettings
                        // {
                        //     Fields = new Field("Title"),
                        // },
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
                () => elastic.DeleteAliasAsync(new DeleteAliasRequest("test_index_1", "test_index_3")),
                () => elastic.DeleteAliasAsync(new DeleteAliasRequest("test_index_1", "test_index_2")),
                () => elastic.CreateIndexAsync("test_index_4"),
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
                // () => elastic.PutRoleMapping("test_role_1"),
                () => elastic.GetRole(new GetRoleRequest("test_role_1")),
                // () => elastic.GetRoleMapping(new GetRoleMappingRequest("test_role_1")),
                // () => elastic.DeleteRoleMapping("test_role_1"),
                () => elastic.DeleteRole("test_role_1"),
                () => elastic.PutUser("test_user_1"),
                () => elastic.ChangePassword(new ChangePasswordRequest("test_user_1")
                {
                    Password = "supersecret",
                }),
                () => elastic.GetUser(new GetUserRequest("test_user_1")),
                //() => elastic.DisableUser("test_user_1"),
                () => elastic.DeleteUser("test_user_1"),
            };
        }

        private static List<Func<object>> UserCommandsAsync(ElasticClient elastic)
        {
            return new List<Func<object>>
            {
                () => elastic.PutRoleAsync("test_role_1"),
                // () => elastic.PutRoleMappingAsync("test_role_1"),
                () => elastic.GetRoleAsync(new GetRoleRequest("test_role_1")),
                // () => elastic.GetRoleMappingAsync(new GetRoleMappingRequest("test_role_1")),
                // () => elastic.DeleteRoleMappingAsync("test_role_1"),
                () => elastic.DeleteRoleAsync("test_role_1"),
                () => elastic.PutUserAsync("test_user_1"),
                () => elastic.ChangePasswordAsync(new ChangePasswordRequest("test_user_1")
                {
                    Password = "supersecret",
                }),
                () => elastic.GetUserAsync(new GetUserRequest("test_user_1")),
                //() => elastic.DisableUserAsync("test_user_1"),
                () => elastic.DeleteUserAsync("test_user_1"),
            };
        }

        private static object TaskResult(Task task)
        {
            task.GetAwaiter().GetResult();
            var taskType = task.GetType();

            bool isTaskOfT = taskType.IsGenericType &&
                             taskType.GetGenericTypeDefinition() == typeof(Task<>);

            return isTaskOfT
                       ? taskType.GetProperty("Result")?.GetValue(task)
                       : null;
        }

        public class Post
        {
            public int Id { get; set; }
            public string Title { get; set; }
        }
    }
}
