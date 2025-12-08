// <copyright file="IWireProtocol_3_5_Execute_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;

/// <summary>
/// MongoDB.Driver.Core.WireProtocol.IWireProtocol&lt;TResult&gt; instrumentation
/// </summary>
#pragma warning disable SA1118 // parameter shouldn't span multiple lines
[InstrumentMethod(
    AssemblyNames = [MongoDbIntegration.MongoDbClientV3Assembly],
    IntegrationName = MongoDbIntegration.IntegrationName,
    MinimumVersion = MongoDbIntegration.Major3Minor5,
    MaximumVersion = MongoDbIntegration.Major3,
    MethodName = "Execute",
    ParameterTypeNames = ["MongoDB.Driver.OperationContext", "MongoDB.Driver.Core.Connections.IConnection"],
    ReturnTypeName = "!0",
    TypeNames = new[]
    {
        "MongoDB.Driver.Core.WireProtocol.CommandUsingQueryMessageWireProtocol`1",
        "MongoDB.Driver.Core.WireProtocol.CommandUsingCommandMessageWireProtocol`1",
        "MongoDB.Driver.Core.WireProtocol.CommandWireProtocol`1",
    })]
#pragma warning restore SA1118
// ReSharper disable once InconsistentNaming
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class IWireProtocol_3_5_Execute_Integration
{
    internal static CallTargetState OnMethodBegin<TTarget, TOperationContext, TConnection>(TTarget instance, TOperationContext? operationContext, TConnection connection)
        where TConnection : IConnection
    {
        var scope = MongoDbIntegration.CreateScope(instance, connection);

        return new CallTargetState(scope);
    }

    /// <summary>
    /// OnAsyncMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Return value</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        var scope = state.Scope;

        scope.DisposeWithException(exception);

        return new CallTargetReturn<TReturn>(returnValue);
    }
}
