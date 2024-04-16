// <copyright file="BatchExecuteIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using System.Data.Common;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;

/// <summary>
/// CallTarget instrumentation for:
/// * [DbBatch].Execute*(*)
/// </summary>
[Browsable(browsable: false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class BatchExecuteIntegration
{
#if NET6_0_OR_GREATER
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        return new CallTargetState(DbScopeFactory.Cache<TTarget>.CreateDbBatchScope(Tracer.Instance, (DbBatch)(object)instance!));
    }

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="returnValue">Task of HttpResponse message instance</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A response value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return new CallTargetReturn<TReturn>(returnValue);
    }
#endif
}
