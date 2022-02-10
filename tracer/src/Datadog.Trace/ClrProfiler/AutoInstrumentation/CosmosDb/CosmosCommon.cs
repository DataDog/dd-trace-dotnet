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

        private const string OperationName = "cosmosdb.query";
        private const string ServiceName = "cosmosdb";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CosmosCommon));

        public static CallTargetState CreateContainerCallStateExt<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition)
        {
            string containerId = null;
            string databaseId = null;
            string endpoint = null;
            if (instance.TryDuckCast<ContainerNewStruct>(out var containerNew))
            {
                containerId = containerNew.Id;
                var database = containerNew.Database;
                databaseId = database.Id;
                endpoint = database.Client.Endpoint?.ToString();
            }
            else if (instance.TryDuckCast<ContainerOldStruct>(out var containerOld))
            {
                containerId = containerOld.Id;
                var database = containerOld.Database;
                databaseId = database.Id;
                endpoint = database.ClientContext.Client.Endpoint?.ToString();
            }

            return CreateCosmosDbCallState(instance, queryDefinition, containerId, databaseId, endpoint);
        }

        public static CallTargetState CreateDatabaseCallStateExt<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition)
        {
            string databaseId = null;
            string endpoint = null;

            if (instance.TryDuckCast<DatabaseNewStruct>(out var databaseNew))
            {
                databaseId = databaseNew.Id;
                var client = databaseNew.Client;
                endpoint = client.Endpoint?.ToString();
            }
            else if (instance.TryDuckCast<DatabaseOldStruct>(out var databaseOld))
            {
                databaseId = databaseOld.Id;
                var client = databaseOld.ClientContext.Client;
                endpoint = client.Endpoint?.ToString();
            }

            return CreateCosmosDbCallState(instance, queryDefinition, null, databaseId, endpoint);
        }

        public static CallTargetState CreateClientCallStateExt<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition)
        {
            string endpoint = null;

            if (instance.TryDuckCast<CosmosClientStruct>(out var c))
            {
                endpoint = c.Endpoint?.ToString();
            }

            return CreateCosmosDbCallState(instance, queryDefinition, null, null, endpoint);
        }

        private static CallTargetState CreateCosmosDbCallState<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition, string containerId, string databaseId, string endpoint)
        {
            if (Tracer.Instance is null || Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId) is false)
            {
                // integration disabled or Tracer.Instance is null, don't create a scope, skip this trace
                Log.Debug(Tracer.Instance is null ? "Tracer.Instance is null." : "CosmosDb Integration is disabled.");
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

                var tags = new CosmosDbTags
                {
                    ContainerId = containerId,
                    DatabaseId = databaseId,
                    Host = endpoint,
                    DbType = "cosmosdb",
                };

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);

                var serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                var scope = tracer.StartActiveInternal(OperationName, tags: tags, serviceName: serviceName);

                var span = scope.Span;

                span.ResourceName = query;
                span.Type = SpanTypes.Sql;

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
