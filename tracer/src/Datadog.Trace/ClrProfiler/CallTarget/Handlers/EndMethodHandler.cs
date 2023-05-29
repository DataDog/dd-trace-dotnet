// <copyright file="EndMethodHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Datadog.Trace.AppSec;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static unsafe class EndMethodHandler<TIntegration, TTarget>
    {
        private static readonly DynamicMethod _dynamicMethod;
        private static readonly delegate*<TTarget, Exception, in CallTargetState, CallTargetReturn> _invokePointer;

        static EndMethodHandler()
        {
            _invokePointer = &EmptyInvoke;

            try
            {
                _dynamicMethod = IntegrationMapper.CreateEndMethodDelegate(typeof(TIntegration), typeof(TTarget));
                if (_dynamicMethod != null)
                {
                    _invokePointer = (delegate*<TTarget, Exception, in CallTargetState, CallTargetReturn>)_dynamicMethod.GetFunctionPointer();
                }
            }
            catch (Exception ex) when (ex is not BlockException)
            {
                throw new CallTargetInvokerException(ex);
            }
        }

        private static CallTargetReturn EmptyInvoke(TTarget instance, Exception exception, in CallTargetState state) => CallTargetReturn.GetDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CallTargetReturn Invoke(TTarget instance, Exception exception, in CallTargetState state)
        {
            return _invokePointer(instance, exception, in state);
        }
    }
}
