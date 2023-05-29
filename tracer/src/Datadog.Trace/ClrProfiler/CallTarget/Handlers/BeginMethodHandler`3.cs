// <copyright file="BeginMethodHandler`3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Datadog.Trace.ExtensionMethods;

#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static unsafe class BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3>
    {
        private static readonly DynamicMethod _dynamicMethod;
        private static readonly delegate*<TTarget, ref TArg1, ref TArg2, ref TArg3, CallTargetState> _invokePointer;

        static BeginMethodHandler()
        {
            _invokePointer = &EmptyInvoke;

            try
            {
                var tArg1ByRef = typeof(TArg1).IsByRef ? typeof(TArg1) : typeof(TArg1).MakeByRefType();
                var tArg2ByRef = typeof(TArg2).IsByRef ? typeof(TArg2) : typeof(TArg2).MakeByRefType();
                var tArg3ByRef = typeof(TArg3).IsByRef ? typeof(TArg3) : typeof(TArg3).MakeByRefType();
                _dynamicMethod = IntegrationMapper.CreateBeginMethodDelegate(typeof(TIntegration), typeof(TTarget), new[] { tArg1ByRef, tArg2ByRef, tArg3ByRef });
                if (_dynamicMethod != null)
                {
                    _invokePointer = (delegate*<TTarget, ref TArg1, ref TArg2, ref TArg3, CallTargetState>)_dynamicMethod.GetFunctionPointer();
                }
            }
            catch (Exception ex)
            {
                throw new CallTargetInvokerException(ex);
            }
        }

        private static CallTargetState EmptyInvoke(TTarget instance, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3) => CallTargetState.GetDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CallTargetState Invoke(TTarget instance, ref TArg1 arg1, ref TArg2 arg2, ref TArg3 arg3)
        {
            var activeScope = Tracer.Instance.InternalActiveScope;
            // We don't use Tracer.Instance.DistributedSpanContext directly because we already retrieved the
            // active scope from an AsyncLocal instance, and we want to avoid retrieving twice.
            var spanContextRaw = DistributedTracer.Instance.GetSpanContextRaw() ?? activeScope?.Span?.Context;
            return new CallTargetState(activeScope, spanContextRaw, _invokePointer(instance, ref arg1, ref arg2, ref arg3));
        }
    }
}
