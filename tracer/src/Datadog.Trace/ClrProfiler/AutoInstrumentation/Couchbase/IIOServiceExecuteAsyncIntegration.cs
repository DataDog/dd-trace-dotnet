// <copyright file="IIOServiceExecuteAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase
{
    /// <summary>
    /// Couchbase.IO.IIOService.Execute calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
       AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
       TypeName = "Couchbase.IO.Services.PooledIOService",
       MethodName = "ExecuteAsync",
       ReturnTypeName = ClrNames.Task,
       ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseOperationTypeName },
       MinimumVersion = CouchbaseCommon.MinVersion2,
       MaximumVersion = CouchbaseCommon.MaxVersion2,
       IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
        TypeName = "Couchbase.IO.Services.SharedPooledIOService",
        MethodName = "ExecuteAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseOperationTypeName },
        MinimumVersion = CouchbaseCommon.MinVersion2,
        MaximumVersion = CouchbaseCommon.MaxVersion2,
        IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
       AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
       TypeName = "Couchbase.IO.Services.MultiplexingIOService",
       MethodName = "ExecuteAsync",
       ReturnTypeName = ClrNames.Task,
       ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseOperationTypeName },
       MinimumVersion = CouchbaseCommon.MinVersion2,
       MaximumVersion = CouchbaseCommon.MaxVersion2,
       IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
        TypeName = "Couchbase.IO.Services.PooledIOService",
        MethodName = "ExecuteAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseGenericOperationTypeName },
        MinimumVersion = CouchbaseCommon.MinVersion2,
        MaximumVersion = CouchbaseCommon.MaxVersion2,
        IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
        TypeName = "Couchbase.IO.Services.SharedPooledIOService",
        MethodName = "ExecuteAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseGenericOperationTypeName },
        MinimumVersion = CouchbaseCommon.MinVersion2,
        MaximumVersion = CouchbaseCommon.MaxVersion2,
        IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
        TypeName = "Couchbase.IO.Services.MultiplexingIOService",
        MethodName = "ExecuteAsync",
        ReturnTypeName = ClrNames.Task,
        ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseGenericOperationTypeName },
        MinimumVersion = CouchbaseCommon.MinVersion2,
        MaximumVersion = CouchbaseCommon.MaxVersion2,
        IntegrationName = CouchbaseCommon.IntegrationName)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class IIOServiceExecuteAsyncIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="operation">The requested couchbase operation</param>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TOperation">Type of the operation</typeparam>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TOperation>(TTarget instance, TOperation operation)
            where TTarget : IIOService
        {
            var normalizedSeedNodes = CouchbaseCommon.GetNormalizedSeedNodesFromClientConfiguration(instance.ConnectionPool.Configuration.ClientConfiguration);
            return CouchbaseCommon.CommonOnMethodBegin(operation, normalizedSeedNodes);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the execution result value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            return CouchbaseCommon.CommonOnMethodEnd(returnValue, exception, in state);
        }
    }
}
