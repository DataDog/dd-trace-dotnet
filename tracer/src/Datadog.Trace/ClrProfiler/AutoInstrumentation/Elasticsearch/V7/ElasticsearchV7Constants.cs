// <copyright file="ElasticsearchV7Constants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V7
{
    internal static class ElasticsearchV7Constants
    {
        internal const string Version7 = "7";
        internal const string ElasticsearchAssemblyName = "Elasticsearch.Net";
        internal const string TransportTypeName = "Elasticsearch.Net.Transport`1";

        internal const string IntegrationName = nameof(IntegrationIds.ElasticsearchNet);
        internal const IntegrationIds IntegrationId = IntegrationIds.ElasticsearchNet;
    }
}
