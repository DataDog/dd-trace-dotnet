// <copyright file="RuntimeAsyncEndMethodHandler`1.cs" company="Datadog">
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
/// <para>
/// An integration may declare both, and then both run, in the same order as
/// <see cref="EndMethodHandler{TIntegration, TTarget, TReturn}"/> runs its continuation generator
/// and its <c>OnMethodEnd</c> - including feeding the value the first substituted into the second.
/// <c>TraceAnnotationsIntegration</c> is the live example, and it relies on that: its
/// <c>OnMethodEnd</c> only disposes the scope when the value it is handed is not task-like, so
/// handing it a completed task correctly leaves disposal to <c>OnAsyncMethodEnd</c>.
/// </para>
/// </remarks>
/// <typeparam name="TIntegration">Integration type</typeparam>
/// <typeparam name="TTarget">Target type</typeparam>
/// <typeparam name="TReturn">The unwrapped return type</typeparam>
/// <typeparam name="TDeclaredReturn">The Task&lt;T&gt; or ValueTask&lt;T&gt; the method declares</typeparam>
internal static class RuntimeAsyncEndMethodHandler<TIntegration, TTarget, TReturn, TDeclaredReturn>
{
    private static readonly ContinuationGenerator<TTarget, TReturn, TReturn>.ContinuationMethodDelegate? OnAsyncMethodEnd;
    private static readonly EndMethodHandler<TIntegration, TTarget, TDeclaredReturn>.InvokeDelegate? OnMethodEnd;

    /// <summary>
    /// Non-null when the integration's OnAsyncMethodEnd is itself async, which cannot be honoured
    /// on a runtime-async target. Recorded here rather than thrown from the static constructor,
    /// because the CLR would wrap that in a TypeInitializationException and
    /// <see cref="IntegrationOptions{TIntegration, TTarget}.LogException"/> only recognises a
    /// <see cref="CallTargetInvokerException"/> as a reason to disable the integration.
    /// </summary>
    private static readonly Exception? UnsupportedAsyncCallback;

    private static bool _reportedUnsupportedSubstitution;

    static RuntimeAsyncEndMethodHandler()
    {
        try
        {
            var asyncResult = IntegrationMapper.CreateAsyncEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TReturn));
            if (asyncResult.Method is { } asyncMethod)
            {
                if (asyncResult.IsTaskReturn)
                {
                    // We cannot await here. The epilog runs inside the finally of the rewritten
                    // method, and the runtime-async spec forbids suspension points in handler
                    // blocks; blocking instead would risk deadlock on a thread with a sync context.
                    UnsupportedAsyncCallback = new NotSupportedException(
                        $"Integration '{typeof(TIntegration).FullName}' has an async 'OnAsyncMethodEnd' returning {asyncMethod.ReturnType.FullName}, which cannot be invoked on the .NET 11 runtime-async target '{typeof(TTarget).FullName}' because the CallTarget epilog runs inside a finally block. The integration will be disabled for this target.");
                }
                else
                {
                    var delegateType = typeof(ContinuationGenerator<TTarget, TReturn, TReturn>.ContinuationMethodDelegate);
                    OnAsyncMethodEnd = (ContinuationGenerator<TTarget, TReturn, TReturn>.ContinuationMethodDelegate)asyncMethod.CreateDelegate(delegateType);
                }
            }

            // Not an else: an integration that declares both gets both, as it would from
            // EndMethodHandler<TIntegration, TTarget, TDeclaredReturn>.
            if (IntegrationMapper.CreateEndMethodDelegate(typeof(TIntegration), typeof(TTarget), typeof(TDeclaredReturn)) is { } endMethod)
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
        // There is deliberately no IntegrationOptions.RestoreScopeFromAsyncExecution call here,
        // unlike EndMethodHandler<TIntegration, TTarget, TReturn>. That one exists because
        // CallTarget instruments a state-machine async method via its *stub*: OnMethodBegin runs
        // before builder.Start(ref stateMachine), outside the ExecutionContext save/restore that
        // AsyncMethodBuilderCore.Start performs, so its AsyncLocal write escapes to the caller and
        // has to be undone by hand. A runtime-async method has no stub - the prologue runs inside
        // the method, within the region the runtime already unwinds - so there is nothing to undo,
        // and restoring here would clobber any distributed context the body legitimately set.
        // RuntimeAsyncScopeRestoreTests pins both halves of this.
        if (UnsupportedAsyncCallback is { } unsupported)
        {
            // Turn the integration off for this target rather than leave it half-live. OnMethodBegin
            // has already run and created state that only the callback we cannot invoke would clean
            // up, so every later call would leak the same way. LogException records telemetry and
            // sets the flag that every BeginMethod and EndMethod overload on CallTargetInvoker gates
            // on, so this runs exactly once: from the next call the target is cleanly uninstrumented.
            IntegrationOptions<TIntegration, TTarget>.LogException(new CallTargetInvokerException(unsupported));
            return new CallTargetReturn<TReturn>(returnValue);
        }

        if (OnAsyncMethodEnd is not null)
        {
            // Feeding the substituted value forward mirrors EndMethodHandler, where OnMethodEnd
            // receives whatever the continuation generator produced rather than the original.
            returnValue = OnAsyncMethodEnd(instance, returnValue, exception, in state);
        }

        if (OnMethodEnd is not null)
        {
            // On the exception path we pass the default value rather than a faulted task, matching
            // what a state-machine target does when it throws before its first suspension: the
            // exception is carried by the exception argument, not by the return value.
            var declared = exception is null
                               ? RuntimeAsyncHelper.CreateCompletedFromResult<TReturn, TDeclaredReturn>(returnValue)
                               : default;
            var returned = OnMethodEnd(instance, declared, exception, in state).GetReturnValue();

            // When an exception is propagating the rewritten method rethrows it, so the return value
            // never reaches the caller and there is nothing to honour.
            if (exception is null)
            {
                // A runtime-async body returns the unwrapped T and the runtime builds the task from
                // it, so a replacement task can only be honoured by taking its result back out -
                // which needs it to be complete already. That covers the ordinary case, where
                // OnMethodEnd either returns the completed task we handed it or a new completed one
                // carrying a different result.
                if (RuntimeAsyncHelper.TryGetCompletedResult<TReturn, TDeclaredReturn>(returned, out var unwrapped))
                {
                    returnValue = unwrapped;
                }
                else
                {
                    ReportUnsupportedSubstitution();
                }
            }
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }

    /// <summary>
    /// Reports an OnMethodEnd that returned a task we cannot take a result from - one that is still
    /// running, faulted, cancelled, or null.
    /// </summary>
    /// <remarks>
    /// Deliberately not a disable: unlike an async OnAsyncMethodEnd, the callback did run, so
    /// nothing is left half-initialised and the rest of the integration still works. What is lost
    /// is only the substitution, so this reports and carries on with the original value. The
    /// generic <see cref="Exception"/> type matters - <see cref="IntegrationOptions{TIntegration, TTarget}.LogException"/>
    /// disables the integration for a <see cref="CallTargetInvokerException"/> but not for this.
    /// <para>
    /// Latched because it would otherwise fire on every call. The race on the flag is benign: the
    /// worst case is a duplicate log line.
    /// </para>
    /// </remarks>
    private static void ReportUnsupportedSubstitution()
    {
        if (_reportedUnsupportedSubstitution)
        {
            return;
        }

        _reportedUnsupportedSubstitution = true;
        IntegrationOptions<TIntegration, TTarget>.LogException(new NotSupportedException(
            $"Integration '{typeof(TIntegration).FullName}' returned a replacement {typeof(TDeclaredReturn).FullName} from 'OnMethodEnd' that had not already completed successfully. "
          + $"The target '{typeof(TTarget).FullName}' is a .NET 11 runtime-async method, whose body returns the unwrapped result and lets the runtime build the task, so a still-running, faulted or cancelled replacement cannot be honoured and the original result is used instead. "
          + "Use 'OnAsyncMethodEnd' to substitute a result on a runtime-async target."));
    }
}
#endif
