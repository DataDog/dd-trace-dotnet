// <copyright file="ClusterNodeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase
{
    /// <summary>
    /// Couchbase clusterNode 3.2 calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
       AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
       TypeName = "Couchbase.Core.ClusterNode",
       MethodName = "ExecuteOp",
       ReturnTypeName = ClrNames.Task,
       ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseOperationV3TypeName, "Couchbase.Core.IO.Operations.CancellationTokenPair" },
       MinimumVersion = "3.1.3",
       MaximumVersion = CouchbaseCommon.MaxVersion3,
       IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
       AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
       TypeName = "Couchbase.Core.ClusterNode",
       MethodName = "SendAsync",
       ReturnTypeName = ClrNames.Task,
       ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseOperationV3TypeName, "Couchbase.Core.IO.Operations.CancellationTokenPair" },
       MinimumVersion = "3.1.3",
       MaximumVersion = CouchbaseCommon.MaxVersion3,
       IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
       AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
       TypeName = "Couchbase.Core.ClusterNode",
       MethodName = "ExecuteOp",
       ReturnTypeName = ClrNames.Task,
       ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseOperationV3TypeName, ClrNames.CancellationToken },
       MinimumVersion = "3.0.7",
       MaximumVersion = "3.1.2",
       IntegrationName = CouchbaseCommon.IntegrationName)]
    [InstrumentMethod(
       AssemblyName = CouchbaseCommon.CouchbaseClientAssemblyName,
       TypeName = "Couchbase.Core.ClusterNode",
       MethodName = "SendAsync",
       ReturnTypeName = ClrNames.Task,
       ParameterTypeNames = new[] { CouchbaseCommon.CouchbaseOperationV3TypeName, ClrNames.CancellationToken },
       MinimumVersion = "3.0.7",
       MaximumVersion = "3.1.2",
       IntegrationName = CouchbaseCommon.IntegrationName)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ClusterNodeIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="operation">The requested couchbase operation</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TOperation">Type of the operation</typeparam>
        /// <typeparam name="TCancellationToken">Type of the cancellationToken</typeparam>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TOperation, TCancellationToken>(TTarget instance, TOperation operation, TCancellationToken cancellationToken)
        {
            return CouchbaseCommon.CommonOnMethodBeginV3(operation);
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
        internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref CallTargetState state)
        {
            return CouchbaseCommon.CommonOnMethodEnd(returnValue, exception, ref state);
        }
    }
}
