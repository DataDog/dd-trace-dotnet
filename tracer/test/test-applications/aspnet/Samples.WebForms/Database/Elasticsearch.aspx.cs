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

        private static Uri Endpoint()
        {
            var endpoint = Environment.GetEnvironmentVariable("ELASTICSEARCH6_HOST");
            return endpoint is null
                       ? new Uri("http://" + (Environment.GetEnvironmentVariable("ELASTICSEARCH_HOST") ?? "localhost") + ":9200")
                       : new Uri("http://" + endpoint);
        }

        private async Task CallElasticsearch()
        {
            var settings = new ConnectionSettings(Endpoint()).DefaultIndex("elastic-net-example");
            var elastic = new ElasticClient(settings);

            await elastic.ClusterHealthAsync(new ClusterHealthRequest());
            await elastic.ClusterStateAsync(new ClusterStateRequest());
        }
    }
}
