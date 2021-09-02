// <copyright file="IWireProtocol_Execute_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.ClrProfiler.Integrations;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    /// <summary>
    /// MongoDB.Driver.Core.WireProtocol.IWireProtocol instrumentation
    /// </summary>
    [MongoDbExecute(
        typeName: "MongoDB.Driver.Core.WireProtocol.KillCursorsWireProtocol",
        isGeneric: false)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class IWireProtocol_Execute_Integration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="connection">The MongoDB connection</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, object connection, CancellationToken cancellationToken)
        {
            var scope = MongoDbIntegration.CreateScope(instance, connection);

            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            var scope = state.Scope;

            scope.DisposeWithException(exception);

            return CallTargetReturn.GetDefault();
        }
    }
}
