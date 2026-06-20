// <copyright file="EndMethodHandler`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

internal static class EndMethodHandler<TIntegration, TTarget, TReturn>
{
    private static readonly InvokeDelegate? _invokeDelegate;
    private static readonly MethodInfo? _aotInvokeMethod;
    private static readonly ContinuationGenerator<TTarget, TReturn>? _continuationGenerator;

    static EndMethodHandler()
    {
        var returnType = typeof(TReturn);
        try
        {
            if (CallTargetAot.IsAotMode())
            {
                _aotInvokeMethod = CallTargetAotEngine.GetEndRegistration(typeof(TIntegration), typeof(TTarget), returnType).Method;
            }
            else if (IntegrationMapper.CreateEndMethodDelegate(typeof(TIntegration), typeof(TTarget), returnType) is { } dynMethod)
            {
                _invokeDelegate = (InvokeDelegate)dynMethod.CreateDelegate(typeof(InvokeDelegate));
            }
        }
        catch (Exception ex)
        {
            throw new CallTargetInvokerException(ex);
        }

        if (CallTargetAot.IsAotMode() && returnType.IsGenericType)
        {
            if (typeof(Task).IsAssignableFrom(returnType))
            {
                _continuationGenerator = new AotTaskResultContinuationGenerator<TIntegration, TTarget, TReturn>();
            }
#if NETCOREAPP3_1_OR_GREATER
            else if (ValueTaskHelper.IsGenericValueTask(returnType))
            {
                _continuationGenerator = new AotValueTaskResultContinuationGenerator<TIntegration, TTarget, TReturn>();
            }
#endif
        }
        else if (returnType.IsGenericType)
        {
            if (typeof(Task).IsAssignableFrom(returnType))
            {
                _continuationGenerator = (ContinuationGenerator<TTarget, TReturn>?)Activator.CreateInstance(typeof(TaskContinuationGenerator<,,,>).MakeGenericType(typeof(TIntegration), typeof(TTarget), returnType, ContinuationsHelper.GetResultType(returnType)));
            }
            else if (ValueTaskHelper.IsGenericValueTask(returnType))
            {
#if NETCOREAPP3_1_OR_GREATER
                _continuationGenerator = (ContinuationGenerator<TTarget, TReturn>?)Activator.CreateInstance(typeof(ValueTaskContinuationGenerator<,,,>).MakeGenericType(typeof(TIntegration), typeof(TTarget), returnType, ContinuationsHelper.GetResultType(returnType)));
#else
                _continuationGenerator = (ContinuationGenerator<TTarget, TReturn>?)Activator.CreateInstance(typeof(ValueTaskContinuationGenerator<,,,>).MakeGenericType(typeof(TIntegration), typeof(TTarget), returnType, ContinuationsHelper.GetResultType(returnType)));
#endif
            }
        }
        else
        {
            if (returnType == typeof(Task))
            {
                // The type is a Task
                _continuationGenerator = new TaskContinuationGenerator<TIntegration, TTarget, TReturn>();
            }
            else if (ValueTaskHelper.IsValueTask(returnType))
            {
                // The type is a ValueTask
                _continuationGenerator = new ValueTaskContinuationGenerator<TIntegration, TTarget, TReturn>();
            }
        }
    }

    internal delegate CallTargetReturn<TReturn> InvokeDelegate(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CallTargetReturn<TReturn> Invoke(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
    {
        if (_continuationGenerator != null)
        {
            returnValue = _continuationGenerator.SetContinuation(instance, returnValue, exception, in state);
            IntegrationOptions.RestoreScopeFromAsyncExecution(in state);
        }

        if (_aotInvokeMethod != null)
        {
            returnValue = (TReturn?)_aotInvokeMethod.Invoke(null, [instance, returnValue, exception, state]);
        }

        if (_invokeDelegate != null)
        {
            var returnWrap = _invokeDelegate(instance, returnValue, exception, in state);
            returnValue = returnWrap.GetReturnValue();
        }

        return new CallTargetReturn<TReturn>(returnValue);
    }
}
