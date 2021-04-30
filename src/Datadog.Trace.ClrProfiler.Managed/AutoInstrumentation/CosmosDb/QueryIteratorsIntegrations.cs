using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Web;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.ClrProfiler.Integrations.AdoNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.Container.QueryIteratorsIntegrations calltarget instrumentation
    /// </summary>
    // Container level instrumentations
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.ContainerCore",
        MethodName = "GetItemQueryIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.ContainerCore",
        MethodName = "GetItemQueryIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.ContainerCore",
        MethodName = "GetItemQueryStreamIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.ContainerCore",
        MethodName = "GetItemQueryStreamIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]

    // Database level instrumentations for quering containers
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetContainerQueryStreamIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetContainerQueryStreamIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetContainerQueryIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetContainerQueryIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]

    // Database level instrumentations for quering users
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetUserQueryIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetUserQueryIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]

    // Database level instrumentations for quering containers
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.CosmosClient",
        MethodName = "GetDatabaseQueryIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.CosmosClient",
        MethodName = "GetDatabaseQueryIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.CosmosClient",
        MethodName = "GetDatabaseQueryStreamIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]
    [InstrumentMethod(
        AssemblyName = MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.CosmosClient",
        MethodName = "GetDatabaseQueryStreamIterator",
        ReturnTypeName = MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = Major3Minor0,
        MaximumVersion = Major3MinorX,
        IntegrationName = IntegrationName)]

    // ReSharper disable once InconsistentNaming
    public class QueryIteratorsIntegrations
    {
        private const string MicrosoftAzureCosmosClientAssemblyName = "Microsoft.Azure.Cosmos.Client";
        private const string MicrosoftAzureCosmosFeedIteratorTypeName = "Microsoft.Azure.Cosmos.FeedIterator`1<T>";
        private const string MicrosoftAzureCosmosQueryDefinitionTypeName = "Microsoft.Azure.Cosmos.QueryDefinition";
        private const string MicrosoftAzureCosmosQueryRequestOptionsTypeName = "Microsoft.Azure.Cosmos.QueryRequestOptions";
        private const string Major3Minor0 = "3.0.0";
        private const string Major3MinorX = "3";

        private const string OperationName = "cosmosdb.query";
        private const string ServiceName = "cosmosdb";

        private const string IntegrationName = nameof(IntegrationIds.CosmosDb);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(QueryIteratorsIntegrations));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of target</typeparam>
        /// <typeparam name="TQueryDefinition">Type of the query definition</typeparam>
        /// <typeparam name="TQueryRequestOptions">Type of the query request options</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="queryDefinition">Query definition instance</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="queryRequestOptions">Query request options</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TQueryDefinition, TQueryRequestOptions>(TTarget instance, TQueryDefinition queryDefinition, string cancellationToken, TQueryRequestOptions queryRequestOptions)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
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
                    var queryDefinitionObj = DuckType.Create<IQueryDefinition>(queryDefinition);
                    query = queryDefinitionObj.QueryText;
                }

                var tracer = Tracer.Instance;

                var parent = tracer.ActiveScope?.Span;

                if (parent != null &&
                    parent.Type == SpanTypes.CosmosDb &&
                    parent.ResourceName == query)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    return new CallTargetState(null);
                }

                string containerId = null;
                string databaseId = null;
                string endpoint = null;

                object databaseObj = null;
                object clientObj = null;

                if (DuckType.CanCreate(typeof(IContainerOld), instance))
                {
                    var container = DuckType.Create<IContainerOld>(instance);
                    containerId = container.Id;
                    databaseObj = container.Database;
                }
                else if (DuckType.CanCreate(typeof(IContainerNew), instance))
                {
                    var container = DuckType.Create<IContainerNew>(instance);
                    containerId = container.Id;
                    databaseObj = container.Database;
                }
                else
                {
                    databaseObj = instance;
                }

                if (DuckType.CanCreate(typeof(IDatabaseOld), databaseObj))
                {
                    var database = DuckType.Create<IDatabaseOld>(databaseObj);
                    databaseId = database.Id;
                    clientObj = database.ClientContext.Client;
                }
                else if (DuckType.CanCreate(typeof(IDatabaseNew), databaseObj))
                {
                    var database = DuckType.Create<IDatabaseNew>(databaseObj);
                    databaseId = database.Id;
                    endpoint = database.Client.Endpoint.ToString();
                    clientObj = database.Client;
                }
                else
                {
                    clientObj = instance;
                }

                if (DuckType.CanCreate(typeof(ICosmosClient), clientObj))
                {
                    var database = DuckType.Create<ICosmosClient>(clientObj);
                    endpoint = database.Endpoint.ToString();
                }

                var tags = new CosmosDbTags
                {
                    ContainerId = containerId,
                    DatabaseId = databaseId,
                    Host = endpoint
                };

                var serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                var scope = tracer.StartActiveWithTags(OperationName, tags: tags, serviceName: serviceName);

                var span = scope.Span;

                span.ResourceName = query;
                span.Type = SpanTypes.CosmosDb;

                return new CallTargetState(scope);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return new CallTargetState(null);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value</returns>
        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
