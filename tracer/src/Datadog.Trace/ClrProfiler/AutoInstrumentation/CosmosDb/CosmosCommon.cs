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
            string host = null;
            string port = null;
            if (instance.TryDuckCast<ContainerNewStruct>(out var containerNew))
            {
                containerId = containerNew.Id;
                var database = containerNew.Database;
                databaseId = database.Id;
                host = database.Client.Endpoint?.Host;
                port = database.Client.Endpoint?.Port.ToString();
            }
            else if (instance.TryDuckCast<ContainerOldStruct>(out var containerOld))
            {
                containerId = containerOld.Id;
                var database = containerOld.Database;
                databaseId = database.Id;
                host = database.ClientContext.Client.Endpoint?.Host;
                port = database.ClientContext.Client.Endpoint?.Port.ToString();
            }

            return CreateCosmosDbCallState(instance, queryDefinition, containerId, databaseId, host, port);
        }

        public static CallTargetState CreateDatabaseCallStateExt<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition)
        {
            string databaseId = null;
            string host = null;
            string port = null;

            if (instance.TryDuckCast<DatabaseNewStruct>(out var databaseNew))
            {
                databaseId = databaseNew.Id;
                var client = databaseNew.Client;
                host = client.Endpoint?.Host;
                port = client.Endpoint?.Port.ToString();
            }
            else if (instance.TryDuckCast<DatabaseOldStruct>(out var databaseOld))
            {
                databaseId = databaseOld.Id;
                var client = databaseOld.ClientContext.Client;
                host = client.Endpoint?.Host;
                port = client.Endpoint?.Port.ToString();
            }

            return CreateCosmosDbCallState(instance, queryDefinition, null, databaseId, host, port);
        }

        public static CallTargetState CreateClientCallStateExt<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition)
        {
            string host = null;
            string port = null;

            if (instance.TryDuckCast<CosmosClientStruct>(out var c))
            {
                host = c.Endpoint?.Host;
                port = c.Endpoint?.Port.ToString();
            }

            return CreateCosmosDbCallState(instance, queryDefinition, null, null, host, port);
        }

        private static CallTargetState CreateCosmosDbCallState<TTarget, TQueryDefinition>(TTarget instance, TQueryDefinition queryDefinition, string containerId, string databaseId, string host, string port)
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

                string operationName = tracer.CurrentTraceSettings.Schema.Database.GetOperationName(DatabaseType);
                string serviceName = tracer.CurrentTraceSettings.Schema.Database.GetServiceName(DatabaseType);
                CosmosDbTags tags = tracer.CurrentTraceSettings.Schema.Database.CreateCosmosDbTags();
                tags.ContainerId = containerId;
                tags.DatabaseId = databaseId;
                tags.Host = host;
                tags.Port = port;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);

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
