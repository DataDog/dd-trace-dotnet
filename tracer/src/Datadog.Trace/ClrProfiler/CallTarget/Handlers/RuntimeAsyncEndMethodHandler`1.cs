// <copyright file="RuntimeAsyncEndMethodHandler`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using Datadog.Trace.Logging;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

/// <summary>
/// End-method handler for a .NET 11 runtime-async method declaring <see cref="Task{TResult}"/> or
/// <c>ValueTask&lt;TResult&gt;</c>, where <typeparamref name="TReturn"/> is the unwrapped
/// <c>TResult</c> the body actually leaves on the evaluation stack at <c>ret</c>.
/// </summary>
/// <remarks>
/// No continuation is needed or possible: the runtime drives suspension inside the body, so the
/// epilog already runs at true completion with the real result in hand, and an awaited failure
/// has surfaced as a thrown exception rather than as a faulted task. That makes this strictly
/// cheaper than the state-machine path - no continuation task is allocated, and the exception
/// arrives without <c>GetBaseException</c> unwrapping.
/// <para>
/// Integrations must not have to care whether their target happens to be runtime-async, so both
/// callbacks are bound exactly as they would be for a state-machine target.
/// <c>OnAsyncMethodEnd</c> goes through the same
/// <see cref="IntegrationMapper.CreateAsyncEndMethodDelegate"/> the continuation generators use,
/// with the unwrapped type - which is what they pass too - so duck typing, proxy creation and
/// generic constraints behave identically. <c>OnMethodEnd</c> is bound against
/// <typeparamref name="TDeclaredReturn"/>, the declared <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c>,
/// and handed a completed instance carrying the real result.
/// </para>
/// </remarks>
/// <typeparam name="TIntegration">Integration type</typeparam>
/// <typeparam name="TTarget">Target type</typeparam>
/// <typeparam name="TReturn">The unwrapped return type</typeparam>
/// <typeparam name="TDeclaredReturn">The Task&lt;T&gt; or ValueTask&lt;T&gt; the method declares</typeparam>
internal static class RuntimeAsyncEndMethodHandler<TIntegration, TTarget, TReturn, TDeclaredReturn>
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RuntimeAsyncEndMethodHandler<TIntegration, TTarget, TReturn, TDeclaredReturn>));

    private static readonly ContinuationGenerator<TTarget, TReturn, TReturn>.ContinuationMethodDelegate? OnAsyncMethodEnd;
    private static readonly EndMethodHandler<TIntegration, TTarget, TDeclaredReturn>.InvokeDelegate? OnMethodEnd;

    static RuntimeAsyncEndMethodHandler()
    {
        try
        {
            var asyncResult = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TReturn));
            if (asyncResult.Method is { } asyncMethod)
            {
                if (RuntimeAsyncHelper.IsTaskReturning(asyncMethod.ReturnType))
                {
                    // We cannot await here. The epilog runs inside the finally of the rewritten
                    // method, and the runtime-async spec forbids suspension points in handler
                    // blocks; blocking instead would risk deadlock on a thread with a sync context.
                    Log.Error(
                        "Integration '{IntegrationType}' has an async 'OnAsyncMethodEnd' returning {ReturnType}, which is not supported on .NET 11 runtime-async target '{TargetType}'. The callback will not run.",
                        typeof(TIntegration).FullName,
                        asyncMethod.ReturnType.FullName,
                        typeof(TTarget).FullName);
                }
                else
                {
                    var delegateType = typeof(ContinuationGenerator<TTarget, TReturn, TReturn>.ContinuationMethodDelegate);
                    OnAsyncMethodEnd = (ContinuationGenerator<TTarget, TReturn, TReturn>.ContinuationMethodDelegate)asyncMethod.CreateDelegate(delegateType);
                }
            }
            else if (IntegrationMapper.CreateEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TDeclaredReturn)) is { } endMethod)
            {
                var delegateType = typeof(EndMethodHandler<TIntegration, TTarget, TDeclaredReturn>.InvokeDelegate);
                OnMethodEnd = (EndMethodHandler<TIntegration, TTarget, TDeclaredReturn>.InvokeDelegate)endMethod.CreateDelegate(delegateType);
            }
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            throw new CallTargetInvokerException(ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CallTargetReturn<TReturn> Invoke(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (OnAsyncMethodEnd is not null)
        {
            return new CallTargetReturn<TReturn>(OnAsyncMethodEnd(instance, returnValue, exception, in state));
        }

        if (OnMethodEnd is not null)
        {
            // On the exception path we pass the default value rather than a faulted task, matching
            // what a state-machine target does when it throws before its first suspension: the
            // exception is carried by the exception argument, not by the return value.
            // Whatever OnMethodEnd returns is discarded - it is a task, and a runtime-async body
            // has no task slot to put a replacement into. That is inherent, not a choice; an
            // integration that needs to substitute a result should use OnAsyncMethodEnd, which can.
            var declared = exception is null
                               ? RuntimeAsyncHelper.CreateCompletedFromResult<TReturn, TDeclaredReturn>(returnValue)
                               : default;
            OnMethodEnd(instance, declared, exception, in state);
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }
}
