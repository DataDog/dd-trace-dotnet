using System;
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

            var createRes = elastic.CreateDocumentAsync(new Post
            {
                Id = 1,
                Title = "Hello World",
                Contents = "Hello World"
            }).Result;
            Console.WriteLine(createRes.DebugInformation);

            var getRes = elastic.GetAsync<Post>(1).Result;
            Console.WriteLine(getRes.DebugInformation);

            var res = elastic.ClusterHealth();
            Console.WriteLine(res.DebugInformation);
        }

        public class Post
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Contents { get; set; }
        }
    }
}
