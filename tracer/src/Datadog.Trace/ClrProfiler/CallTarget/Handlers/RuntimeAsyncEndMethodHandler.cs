// <copyright file="RuntimeAsyncEndMethodHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

// net6.0 is the only Datadog.Trace asset a .NET 10+ process can load
#if NET6_0_OR_GREATER // NET 10+ really

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

/// <summary>
/// End-method handler for a .NET 11 runtime-async method declaring a non-generic
/// <see cref="Task"/> or <c>ValueTask</c>.
/// </summary>
/// <remarks>
/// Such a method's body leaves nothing on the evaluation stack at <c>ret</c>, so the rewriter
/// treats its effective return type as void and there is no task to attach a continuation to.
/// There is no need for one either: the runtime drives suspension inside the body, so by the time
/// the epilog runs the method has genuinely completed, and an awaited failure has already
/// surfaced as a thrown exception rather than as a faulted task.
/// <para>
/// Integrations must not have to care whether their target happens to be runtime-async, so both
/// callbacks are bound against the type the method <em>declares</em>:
/// <c>OnAsyncMethodEnd</c> exactly as <see cref="TaskContinuationGenerator{TIntegration, TTarget, TReturn}"/>
/// binds it, and <c>OnMethodEnd</c> against <typeparamref name="TDeclaredReturn"/> with a
/// synthesised already-completed value. A completed task is a faithful stand-in here - the
/// operation really has finished by this point, unlike the in-flight task a state-machine target
/// would hand over.
/// </para>
/// <para>
/// An integration may declare both, and then both run, in the same order as
/// <see cref="EndMethodHandler{TIntegration, TTarget, TReturn}"/> runs its continuation generator
/// and its <c>OnMethodEnd</c>. <c>TraceAnnotationsIntegration</c> is the live example, and it
/// relies on that: its <c>OnMethodEnd</c> only disposes the scope when the value it is handed is
/// not task-like, so handing it a completed task correctly leaves disposal to
/// <c>OnAsyncMethodEnd</c>.
/// </para>
/// </remarks>
/// <typeparam name="TIntegration">Integration type</typeparam>
/// <typeparam name="TTarget">Target type</typeparam>
/// <typeparam name="TDeclaredReturn">The Task or ValueTask the method declares</typeparam>
internal static class RuntimeAsyncEndMethodHandler<TIntegration, TTarget, TDeclaredReturn>
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RuntimeAsyncEndMethodHandler<TIntegration, TTarget, TDeclaredReturn>));

    private static readonly ContinuationGenerator<TTarget, object>.ObjectContinuationMethodDelegate? OnAsyncMethodEnd;
    private static readonly EndMethodHandler<TIntegration, TTarget, TDeclaredReturn>.InvokeDelegate? OnMethodEnd;

    /// <summary>
    /// An already-completed value of the declared type, handed to OnMethodEnd in place of the task
    /// the body never materialises. Task.CompletedTask and default(ValueTask) are both free.
    /// </summary>
    private static readonly TDeclaredReturn? CompletedValue;

    static RuntimeAsyncEndMethodHandler()
    {
        try
        {
            var asyncResult = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(object));
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
                    var delegateType = typeof(ContinuationGenerator<TTarget, object>.ObjectContinuationMethodDelegate);
                    OnAsyncMethodEnd = (ContinuationGenerator<TTarget, object>.ObjectContinuationMethodDelegate)asyncMethod.CreateDelegate(delegateType);
                }
            }

            // Not an else: an integration that declares both gets both, as it would from
            // EndMethodHandler<TIntegration, TTarget, TDeclaredReturn>.
            if (IntegrationMapper.CreateEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TDeclaredReturn)) is { } endMethod)
            {
                var delegateType = typeof(EndMethodHandler<TIntegration, TTarget, TDeclaredReturn>.InvokeDelegate);
                OnMethodEnd = (EndMethodHandler<TIntegration, TTarget, TDeclaredReturn>.InvokeDelegate)endMethod.CreateDelegate(delegateType);
                CompletedValue = RuntimeAsyncHelper.CreateCompleted<TDeclaredReturn>();
            }
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            throw new CallTargetInvokerException(ex);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CallTargetReturn Invoke(TTarget? instance, Exception? exception, in CallTargetState state)
    {
        if (OnAsyncMethodEnd is not null)
        {
            OnAsyncMethodEnd(instance, null, exception, in state);
        }

        if (OnMethodEnd is not null)
        {
            // On the exception path we pass the default value rather than a faulted task, matching
            // what a state-machine target does when it throws before its first suspension: the
            // exception is carried by the exception argument, not by the return value.
            // Whatever OnMethodEnd returns is discarded - a runtime-async body has no task slot to
            // put a replacement into. That is inherent, not a choice.
            OnMethodEnd(instance, exception is null ? CompletedValue : default, exception, in state);
        }

        return CallTargetReturn.GetDefault();
    }
}
#endif
