// <copyright file="IIOServiceExecuteIntegrationBis.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase
{
    /// <summary>
    /// Couchbase.IO.IIOService.Execute calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
        TypeName = "Couchbase.IO.Services.PooledIOService",
        MethodName = "Execute",
        ReturnTypeName = CouchbaseCommon.CouchbaseOperationResultTypeName,
        ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseGenericOperationTypeName, CouchbaseCommon.CouchbaseConnectionTypeName },
        MinimumVersion = CouchbaseCommon.MinVersion2,
        MaximumVersion = CouchbaseCommon.MaxVersion2,
        IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
        TypeName = "Couchbase.IO.Services.SharedPooledIOService",
        MethodName = "Execute",
        ReturnTypeName = CouchbaseCommon.CouchbaseOperationResultTypeName,
        ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseGenericOperationTypeName, CouchbaseCommon.CouchbaseConnectionTypeName },
        MinimumVersion = CouchbaseCommon.MinVersion2,
        MaximumVersion = CouchbaseCommon.MaxVersion2,
        IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
        TypeName = "Couchbase.IO.Services.MultiplexingIOService",
        MethodName = "Execute",
        ReturnTypeName = CouchbaseCommon.CouchbaseOperationResultTypeName,
        ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseGenericOperationTypeName, CouchbaseCommon.CouchbaseConnectionTypeName },
        MinimumVersion = CouchbaseCommon.MinVersion2,
        MaximumVersion = CouchbaseCommon.MaxVersion2,
        IntegrationName = CouchbaseCommon.IntegrationName)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class IIOServiceExecuteIntegrationBis
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="operation">The requested couchbase operation</param>
        /// <param name="connection">A provided connection</param>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TOperation">Type of the operation</typeparam>
        /// <typeparam name="TConnection">Type of the connection</typeparam>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TOperation, TConnection>(TTarget instance, TOperation operation, TConnection connection)
        {
            if (connection.TryDuckCast<ConnectionStruct>(out var connectionStruct))
            {
                var normalizedSeedNodes = CouchbaseCommon.GetNormalizedSeedNodesFromClientConfiguration(connectionStruct.Configuration.ClientConfiguration);
                return CouchbaseCommon.CommonOnMethodBegin(operation, normalizedSeedNodes);
            }

            return CouchbaseCommon.CommonOnMethodBegin(operation, null);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TOperationResult">Type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="result">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TOperationResult> OnMethodEnd<TTarget, TOperationResult>(TTarget instance, TOperationResult result, Exception exception, in CallTargetState state)
        {
            return CouchbaseCommon.CommonOnMethodEndSync(result, exception, in state);
        }
}
}
