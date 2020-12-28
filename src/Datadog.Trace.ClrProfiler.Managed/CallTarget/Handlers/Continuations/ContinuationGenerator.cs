using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations
{
    internal class ContinuationGenerator<TTarget, TReturn>
    {
        public virtual TReturn SetContinuation(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            return returnValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static TReturn ToTReturn<TFrom>(TFrom returnValue)
        {
#if NETCOREAPP3_1 || NET5_0
            return Unsafe.As<TFrom, TReturn>(ref returnValue);
#else
            return ContinuationsHelper.Convert<TFrom, TReturn>(returnValue);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static TTo FromTReturn<TTo>(TReturn returnValue)
        {
#if NETCOREAPP3_1 || NET5_0
            return Unsafe.As<TReturn, TTo>(ref returnValue);
#else
            return ContinuationsHelper.Convert<TReturn, TTo>(returnValue);
#endif
        }
    }
}
