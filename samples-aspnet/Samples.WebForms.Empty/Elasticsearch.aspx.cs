using System;
using Nest;

namespace Samples.WebForms.Empty
{
    public partial class Elasticsearch : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var host = Environment.GetEnvironmentVariable("ELASTICSEARCH_HOST") ?? "localhost";
            var uri = new Uri($"http://{host}:9200");
            var settings = new ConnectionSettings(uri).DefaultIndex("elastic-net-example");
            var elastic = new ElasticClient(settings);
            elastic.ClusterHealth(new ClusterHealthRequest());
        }
    }
}
