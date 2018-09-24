using System;
using Nest;


namespace Samples.Elasticsearch
{
    class Program
    {
        static void Main(string[] args)
        {
            var localhost = new Uri("http://localhost:9200");
            var settings = new ConnectionSettings(localhost);
            var elastic = new ElasticClient(settings);

            var res = elastic.ClusterHealth();
            Console.WriteLine(res.DebugInformation);
        }
    }
}
