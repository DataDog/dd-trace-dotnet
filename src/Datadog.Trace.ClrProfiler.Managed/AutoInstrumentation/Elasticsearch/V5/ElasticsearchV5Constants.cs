using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V5
{
    internal static class ElasticsearchV5Constants
    {
        internal const string Version5 = "5";
        internal const string ElasticsearchAssemblyName = "Elasticsearch.Net";
        internal const string RequestPipelineTypeName = "Elasticsearch.Net.RequestPipeline";

        internal const string IntegrationName = nameof(IntegrationIds.ElasticsearchNet5);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
    }
}
