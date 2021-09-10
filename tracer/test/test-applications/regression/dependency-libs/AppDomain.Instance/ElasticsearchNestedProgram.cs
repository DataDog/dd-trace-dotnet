using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;

namespace AppDomain.Instance
{
    public class ElasticsearchNestedProgram : NestedProgram
    {
        public override void Run()
        {
            var host = new Uri("http://" + Host());
            var settings = new ConnectionSettings(host).DefaultIndex("elastic-net-example");
            var elastic = new ElasticClient(settings);

            var commands = new List<Func<object>>().
                Concat(DocumentCommands(elastic)).
                Concat(DocumentCommandsAsync(elastic));

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

        private static object TaskResult(Task task)
        {
            task.Wait();
            var taskType = task.GetType();

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
