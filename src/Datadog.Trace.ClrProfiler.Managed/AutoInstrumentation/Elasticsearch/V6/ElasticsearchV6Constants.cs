using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V6
{
    internal static class ElasticsearchV6Constants
    {
        internal const string Version6 = "6";
        internal const string ElasticsearchAssemblyName = "Elasticsearch.Net";
        internal const string RequestPipelineTypeName = "Elasticsearch.Net.RequestPipeline";

        internal const string IntegrationName = nameof(IntegrationIds.ElasticsearchNet);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
    }
}
