// <copyright file="CosmosCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    internal static class CosmosCommon
    {
        public const string MicrosoftAzureCosmosClientAssemblyName = "Microsoft.Azure.Cosmos.Client";
        public const string MicrosoftAzureCosmosFeedIteratorTypeName = "Microsoft.Azure.Cosmos.FeedIterator`1<T>";
        public const string MicrosoftAzureCosmosQueryDefinitionTypeName = "Microsoft.Azure.Cosmos.QueryDefinition";
        public const string MicrosoftAzureCosmosQueryRequestOptionsTypeName = "Microsoft.Azure.Cosmos.QueryRequestOptions";
        public const string Major3Minor6 = "3.6.0";
        public const string Major3MinorX = "3";

        public const string IntegrationName = nameof(Configuration.IntegrationId.CosmosDb);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.CosmosDb;

        private const string DatabaseType = "cosmosdb";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CosmosCommon));

        public static CallTargetState CreateContainerCallStateExt<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition)
        {
            string containerId = null;
            string databaseId = null;
            Uri endpoint = null;

            if (instance.TryDuckCast<ContainerStruct>(out var container))
            {
                containerId = container.Id;
                if (container.Database.TryDuckCast<DatabaseNewStruct>(out var databaseNew))
                {
                    databaseId = databaseNew.Id;
                    endpoint = databaseNew.Client.Endpoint;
                }
                else if (container.Database.TryDuckCast<DatabaseOldStruct>(out var databaseOld))
                {
                    databaseId = databaseOld.Id;
                    endpoint = databaseOld.ClientContext.Client.Endpoint;
                }
            }

            return CreateCosmosDbCallState(instance, queryDefinition, containerId, databaseId, endpoint);
        }

        public static CallTargetState CreateDatabaseCallStateExt<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition)
        {
            string databaseId = null;
            Uri endpoint = null;

            if (instance.TryDuckCast<DatabaseNewStruct>(out var databaseNew))
            {
                databaseId = databaseNew.Id;
                var client = databaseNew.Client;
                endpoint = client.Endpoint;
            }
            else if (instance.TryDuckCast<DatabaseOldStruct>(out var databaseOld))
            {
                databaseId = databaseOld.Id;
                var client = databaseOld.ClientContext.Client;
                endpoint = client.Endpoint;
            }

            return CreateCosmosDbCallState(instance, queryDefinition, null, databaseId, endpoint);
        }

        public static CallTargetState CreateClientCallStateExt<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition)
        {
            Uri endpoint = null;
            if (instance.TryDuckCast<CosmosClientStruct>(out var c))
            {
                endpoint = c.Endpoint;
            }

            return CreateCosmosDbCallState(instance, queryDefinition, null, null, endpoint);
        }

        private static CallTargetState CreateCosmosDbCallState<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition, string containerId, string databaseId, Uri endpoint)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return CallTargetState.GetDefault();
            }

            try
            {
                string query;
                if (queryDefinition is string queryDefinitionString)
                {
                    query = queryDefinitionString;
                }
                else
                {
                    var success = queryDefinition.TryDuckCast<QueryDefinitionStruct>(out var queryDefinitionObj);
                    query = queryDefinitionObj.QueryText;
                }

                var tracer = Tracer.Instance;

                var parent = tracer.ActiveScope?.Span;

                if (parent != null &&
                    parent.Type == SpanTypes.Sql &&
                    parent.GetTag(Tags.DbType) == "cosmosdb" &&
                    parent.ResourceName == query)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    return new CallTargetState(null);
                }

                var operationName = tracer.CurrentTraceSettings.Schema.Database.GetOperationName(DatabaseType);
                var serviceName = tracer.CurrentTraceSettings.Schema.Database.GetServiceName(DatabaseType);
                var tags = tracer.CurrentTraceSettings.Schema.Database.CreateCosmosDbTags();
                tags.ContainerId = containerId;
                tags.DatabaseId = databaseId;
                tags.SetEndpoint(endpoint);

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

                var scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName);

                var span = scope.Span;

                span.ResourceName = query;
                span.Type = SpanTypes.Sql;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);

                return new CallTargetState(scope);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return new CallTargetState(null);
        }
    }
}
