using System;
using System.Threading.Tasks;
using System.Web.UI;
using Nest;
using Page = System.Web.UI.Page;

namespace Samples.WebForms.Database
{
    public partial class Elasticsearch : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RegisterAsyncTask(new PageAsyncTask(CallElasticsearch));
        }

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("ELASTICSEARCH_HOST") ?? "localhost";
        }

        private async Task CallElasticsearch()
        {
            var host = new Uri("http://" + Host() + ":9200");
            var settings = new ConnectionSettings(host).DefaultIndex("elastic-net-example");
            var elastic = new ElasticClient(settings);

            await elastic.ClusterHealthAsync(new ClusterHealthRequest());
            await elastic.ClusterStateAsync(new ClusterStateRequest());
        }
    }
}
