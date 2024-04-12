// <copyright file="ContinuationGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using VendoredUnsafe = Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe.Unsafe;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

internal abstract class ContinuationGenerator<TTarget, TReturn>
{
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ContinuationGenerator<TTarget, TReturn>));

    internal delegate object? ObjectContinuationMethodDelegate(TTarget? target, object? returnValue, Exception? exception, in CallTargetState state);

    internal delegate Task<object?> AsyncObjectContinuationMethodDelegate(TTarget? target, object? returnValue, Exception? exception, in CallTargetState state);

    public abstract TReturn? SetContinuation(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static TReturn ToTReturn<TFrom>(TFrom returnValue)
    {
#if NETCOREAPP3_1_OR_GREATER
        return Unsafe.As<TFrom, TReturn>(ref returnValue);
#else
            return VendoredUnsafe.As<TFrom, TReturn>(ref returnValue);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static TTo FromTReturn<TTo>(TReturn returnValue)
    {
#if NETCOREAPP3_1_OR_GREATER
        return Unsafe.As<TReturn, TTo>(ref returnValue);
#else
            return VendoredUnsafe.As<TReturn, TTo>(ref returnValue);
#endif
    }

    internal abstract class CallbackHandler
    {
        public abstract TReturn? ExecuteCallback(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state);
    }

    internal class NoOpCallbackHandler : CallbackHandler
    {
        public override TReturn? ExecuteCallback(TTarget? instance, TReturn? returnValue, Exception? exception, in CallTargetState state)
        {
            return returnValue;
        }
    }
}

#pragma warning disable SA1402
internal abstract class ContinuationGenerator<TTarget, TReturn, TResult> : ContinuationGenerator<TTarget, TReturn>
#pragma warning restore SA1402
{
    internal delegate TResult? ContinuationMethodDelegate(TTarget? target, TResult? returnValue, Exception? exception, in CallTargetState state);

    internal delegate Task<TResult?> AsyncContinuationMethodDelegate(TTarget? target, TResult? returnValue, Exception? exception, in CallTargetState state);
}
