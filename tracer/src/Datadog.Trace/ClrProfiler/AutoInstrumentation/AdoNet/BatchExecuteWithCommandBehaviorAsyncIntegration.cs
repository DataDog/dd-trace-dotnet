// <copyright file="BatchExecuteWithCommandBehaviorAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using System.Data.Common;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;

/// <summary>
/// CallTarget instrumentation for:
/// Task[*] [DbBatch].Execute*Async(CommandBehavior, CancellationToken)
/// </summary>
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class BatchExecuteWithCommandBehaviorAsyncIntegration
{
#if NET6_0_OR_GREATER
    internal static CallTargetState OnMethodBegin<TTarget, TBehavior>(TTarget instance, TBehavior commandBehavior, CancellationToken cancellationToken)
    {
        return new CallTargetState(DbScopeFactory.Cache<TTarget>.CreateDbBatchScope(Tracer.Instance, (DbBatch)(object)instance!));
    }

    internal static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return returnValue;
    }
#endif
}
