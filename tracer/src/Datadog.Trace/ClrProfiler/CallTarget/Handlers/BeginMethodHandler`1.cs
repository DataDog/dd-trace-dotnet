// <copyright file="BeginMethodHandler`1.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static class BeginMethodHandler<TIntegration, TTarget, TArg1>
    {
        private static readonly InvokeDelegate _invokeDelegate;

        static BeginMethodHandler()
        {
            try
            {
                Type tArg1ByRef = typeof(TArg1).IsByRef ? typeof(TArg1) : typeof(TArg1).MakeByRefType();
                DynamicMethod dynMethod = IntegrationMapper.CreateBeginMethodDelegate(typeof(TIntegration), typeof(TTarget), new[] { tArg1ByRef });
                if (dynMethod != null)
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
                if (_invokeDelegate is null)
                {
                    _invokeDelegate = (TTarget instance, ref TArg1 arg1) => CallTargetState.GetDefault();
                }
            }
        }

        internal delegate CallTargetState InvokeDelegate(TTarget instance, ref TArg1 arg1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CallTargetState Invoke(TTarget instance, ref TArg1 arg1)
        {
            var activeScope = Tracer.Instance.InternalActiveScope;
            // We don't use Tracer.Instance.DistributedSpanContext directly because we already retrieved the
            // active scope from an AsyncLocal instance, and we want to avoid retrieving twice.
            var spanContextRaw = DistributedTracer.Instance.GetSpanContextRaw() ?? activeScope?.Span.GetContext();
            return new CallTargetState(activeScope, spanContextRaw, _invokeDelegate(instance, ref arg1));
        }
    }
}
