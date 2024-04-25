// <copyright file="EndMethodHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Datadog.Trace.AppSec;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

internal static class EndMethodHandler<TIntegration, TTarget>
{
    private static readonly InvokeDelegate _invokeDelegate;

    static EndMethodHandler()
    {
        try
        {
            if (IntegrationMapper.CreateEndMethodDelegate(typeof(TIntegration), typeof(TTarget)) is { } dynMethod)
            {
                _invokeDelegate = (InvokeDelegate)dynMethod.CreateDelegate(typeof(InvokeDelegate));
            }
        }
        catch (Exception ex) when (ex is not BlockException)
        {
            throw new CallTargetInvokerException(ex);
        }
        finally
        {
            _invokeDelegate ??= (TTarget? instance, Exception? exception, in CallTargetState state) => CallTargetReturn.GetDefault();
        }
    }

    internal delegate CallTargetReturn InvokeDelegate(TTarget? instance, Exception? exception, in CallTargetState state);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CallTargetReturn Invoke(TTarget? instance, Exception? exception, in CallTargetState state)
    {
        return _invokeDelegate(instance, exception, in state);
    }
}
