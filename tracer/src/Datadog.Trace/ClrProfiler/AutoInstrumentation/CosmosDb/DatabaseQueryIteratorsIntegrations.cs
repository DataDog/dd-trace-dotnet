// <copyright file="DatabaseQueryIteratorsIntegrations.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.Container.QueryIteratorsIntegrations calltarget instrumentation
    /// </summary>
    /// <remarks>
    /// https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/Resource/Database/DatabaseCore.cs
    /// </remarks>
    // Database level instrumentations for querying users
    // https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/Resource/Database/DatabaseCore.cs#L609
    [InstrumentMethod(
        AssemblyName = CosmosCommon.MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetUserQueryIterator",
        ReturnTypeName = CosmosCommon.MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { CosmosCommon.MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, CosmosCommon.MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = CosmosCommon.Major3Minor6,
        MaximumVersion = CosmosCommon.Major3MinorX,
        IntegrationName = CosmosCommon.IntegrationName)]
    // https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/Resource/Database/DatabaseCore.cs#L646
    [InstrumentMethod(
        AssemblyName = CosmosCommon.MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetUserQueryIterator",
        ReturnTypeName = CosmosCommon.MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, CosmosCommon.MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = CosmosCommon.Major3Minor6,
        MaximumVersion = CosmosCommon.Major3MinorX,
        IntegrationName = CosmosCommon.IntegrationName)]

    // Database level instrumentations for querying containers
    // https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/Resource/Database/DatabaseCore.cs#L572
    [InstrumentMethod(
        AssemblyName = CosmosCommon.MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetContainerQueryStreamIterator",
        ReturnTypeName = CosmosCommon.MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { CosmosCommon.MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, CosmosCommon.MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = CosmosCommon.Major3Minor6,
        MaximumVersion = CosmosCommon.Major3MinorX,
        IntegrationName = CosmosCommon.IntegrationName)]
    // https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/Resource/Database/DatabaseCore.cs#L538
    [InstrumentMethod(
        AssemblyName = CosmosCommon.MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetContainerQueryStreamIterator",
        ReturnTypeName = CosmosCommon.MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, CosmosCommon.MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = CosmosCommon.Major3Minor6,
        MaximumVersion = CosmosCommon.Major3MinorX,
        IntegrationName = CosmosCommon.IntegrationName)]
    // https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/Resource/Database/DatabaseCore.cs#L588
    [InstrumentMethod(
        AssemblyName = CosmosCommon.MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetContainerQueryIterator",
        ReturnTypeName = CosmosCommon.MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { CosmosCommon.MicrosoftAzureCosmosQueryDefinitionTypeName, ClrNames.String, CosmosCommon.MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = CosmosCommon.Major3Minor6,
        MaximumVersion = CosmosCommon.Major3MinorX,
        IntegrationName = CosmosCommon.IntegrationName)]
    // https://github.com/Azure/azure-cosmos-dotnet-v3/blob/a25730a77ab43a8e460ddc292f1a6d8eb193395a/Microsoft.Azure.Cosmos/src/Resource/Database/DatabaseCore.cs#L555C57-L555C57
    [InstrumentMethod(
        AssemblyName = CosmosCommon.MicrosoftAzureCosmosClientAssemblyName,
        TypeName = "Microsoft.Azure.Cosmos.DatabaseCore",
        MethodName = "GetContainerQueryIterator",
        ReturnTypeName = CosmosCommon.MicrosoftAzureCosmosFeedIteratorTypeName,
        ParameterTypeNames = new[] { ClrNames.String, ClrNames.String, CosmosCommon.MicrosoftAzureCosmosQueryRequestOptionsTypeName, },
        MinimumVersion = CosmosCommon.Major3Minor6,
        MaximumVersion = CosmosCommon.Major3MinorX,
        IntegrationName = CosmosCommon.IntegrationName)]

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    // ReSharper disable once InconsistentNaming
    public class DatabaseQueryIteratorsIntegrations
    {
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
        internal static CallTargetState OnMethodBegin<TTarget, TQueryDefinition, TQueryRequestOptions>(TTarget instance, TQueryDefinition queryDefinition, string cancellationToken, TQueryRequestOptions queryRequestOptions)
        {
            return CosmosCommon.CreateDatabaseCallStateExt(instance, queryDefinition);
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
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
