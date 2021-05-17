// <copyright file="BeginMethodHandler`3.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static class BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3>
    {
        private static readonly InvokeDelegate _invokeDelegate;

        static BeginMethodHandler()
        {
            try
            {
                DynamicMethod dynMethod = IntegrationMapper.CreateBeginMethodDelegate(typeof(TIntegration), typeof(TTarget), new[] { typeof(TArg1), typeof(TArg2), typeof(TArg3) });
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
                    _invokeDelegate = (instance, arg1, arg2, arg3) => CallTargetState.GetDefault();
                }
            }
        }

        internal delegate CallTargetState InvokeDelegate(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CallTargetState Invoke(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3)
        {
            return new CallTargetState(Tracer.Instance.ActiveScope, _invokeDelegate(instance, arg1, arg2, arg3));
        }
    }
}
