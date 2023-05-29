// <copyright file="BeginMethodSlowHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers
{
    internal static unsafe class BeginMethodSlowHandler<TIntegration, TTarget>
    {
        private static readonly DynamicMethod _dynamicMethod;
        private static readonly delegate*<TTarget, object[], CallTargetState> _invokePointer;

        static BeginMethodSlowHandler()
        {
            _invokePointer = &EmptyInvoke;

            try
            {
                _dynamicMethod = IntegrationMapper.CreateSlowBeginMethodDelegate(typeof(TIntegration), typeof(TTarget));
                if (_dynamicMethod != null)
                {
                    _invokePointer = (delegate*<TTarget, object[], CallTargetState>)_dynamicMethod.GetFunctionPointer();
                }
            }
            catch (Exception ex)
            {
                throw new CallTargetInvokerException(ex);
            }
        }

        private static CallTargetState EmptyInvoke(TTarget instance, object[] arguments) => CallTargetState.GetDefault();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CallTargetState Invoke(TTarget instance, object[] arguments)
        {
            var activeScope = Tracer.Instance.InternalActiveScope;
            // We don't use Tracer.Instance.DistributedSpanContext directly because we already retrieved the
            // active scope from an AsyncLocal instance, and we want to avoid retrieving twice.
            var spanContextRaw = DistributedTracer.Instance.GetSpanContextRaw() ?? activeScope?.Span?.Context;
            return new CallTargetState(activeScope, spanContextRaw, _invokePointer(instance, arguments));
        }
    }
}
