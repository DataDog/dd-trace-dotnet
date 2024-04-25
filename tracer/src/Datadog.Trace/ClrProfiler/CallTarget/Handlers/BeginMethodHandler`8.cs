// <copyright file="BeginMethodHandler`8.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.CompilerServices;

#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

internal static class BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>
{
    private static readonly InvokeDelegate _invokeDelegate;

    static BeginMethodHandler()
    {
        try
        {
            var tArg1ByRef = typeof(TArg1).IsByRef ? typeof(TArg1) : typeof(TArg1).MakeByRefType();
            var tArg2ByRef = typeof(TArg2).IsByRef ? typeof(TArg2) : typeof(TArg2).MakeByRefType();
            var tArg3ByRef = typeof(TArg3).IsByRef ? typeof(TArg3) : typeof(TArg3).MakeByRefType();
            var tArg4ByRef = typeof(TArg4).IsByRef ? typeof(TArg4) : typeof(TArg4).MakeByRefType();
            var tArg5ByRef = typeof(TArg5).IsByRef ? typeof(TArg5) : typeof(TArg5).MakeByRefType();
            var tArg6ByRef = typeof(TArg6).IsByRef ? typeof(TArg6) : typeof(TArg6).MakeByRefType();
            var tArg7ByRef = typeof(TArg7).IsByRef ? typeof(TArg7) : typeof(TArg7).MakeByRefType();
            var tArg8ByRef = typeof(TArg8).IsByRef ? typeof(TArg8) : typeof(TArg8).MakeByRefType();
            if (IntegrationMapper.CreateBeginMethodDelegate(typeof(TIntegration), typeof(TTarget), [tArg1ByRef, tArg2ByRef, tArg3ByRef, tArg4ByRef, tArg5ByRef, tArg6ByRef, tArg7ByRef, tArg8ByRef]) is { } dynMethod)
            {
                _invokeDelegate = (InvokeDelegate)dynMethod.CreateDelegate(typeof(InvokeDelegate));
            }
        }
        catch (Exception ex)
        {
            throw new CallTargetInvokerException(ex);
        }
        finally
        {
            _invokeDelegate ??= (TTarget? _, ref TArg1? _, ref TArg2? _, ref TArg3? _, ref TArg4? _, ref TArg5? _, ref TArg6? _, ref TArg7? _, ref TArg8? _) => CallTargetState.GetDefault();
        }
    }

    internal delegate CallTargetState InvokeDelegate(TTarget? instance, ref TArg1? arg1, ref TArg2? arg2, ref TArg3? arg3, ref TArg4? arg4, ref TArg5? arg5, ref TArg6? arg6, ref TArg7? arg7, ref TArg8? arg8);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CallTargetState Invoke(TTarget? instance, ref TArg1? arg1, ref TArg2? arg2, ref TArg3? arg3, ref TArg4? arg4, ref TArg5? arg5, ref TArg6? arg6, ref TArg7? arg7, ref TArg8? arg8)
    {
        var activeScope = Tracer.Instance.InternalActiveScope;
        // We don't use Tracer.Instance.DistributedSpanContext directly because we already retrieved the
        // active scope from an AsyncLocal instance, and we want to avoid retrieving twice.
        var spanContextRaw = DistributedTracer.Instance.GetSpanContextRaw() ?? activeScope?.Span?.Context;
        return new CallTargetState(activeScope, spanContextRaw, _invokeDelegate(instance, ref arg1, ref arg2, ref arg3, ref arg4, ref arg5, ref arg6, ref arg7, ref arg8));
    }
}
