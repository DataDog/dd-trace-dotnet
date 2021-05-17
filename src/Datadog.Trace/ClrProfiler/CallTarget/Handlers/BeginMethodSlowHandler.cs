// <copyright file="BeginMethodSlowHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static class BeginMethodSlowHandler<TIntegration, TTarget>
    {
        private static readonly InvokeDelegate _invokeDelegate;

        static BeginMethodSlowHandler()
        {
            try
            {
                DynamicMethod dynMethod = IntegrationMapper.CreateSlowBeginMethodDelegate(typeof(TIntegration), typeof(TTarget));
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
                    _invokeDelegate = (instance, arguments) => CallTargetState.GetDefault();
                }
            }
        }

        internal delegate CallTargetState InvokeDelegate(TTarget instance, object[] arguments);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CallTargetState Invoke(TTarget instance, object[] arguments)
        {
            return new CallTargetState(Tracer.Instance.ActiveScope, _invokeDelegate(instance, arguments));
        }
    }
}
