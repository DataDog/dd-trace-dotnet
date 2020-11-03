using System;
using Datadog.Trace.Logging;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements must be documented

namespace Datadog.Trace.ClrProfiler.CallTarget.NoOp
{
    /// <summary>
    /// NoOp Integration for 6 Arguments
    /// </summary>
    public static class Noop6ArgumentsIntegration
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(Noop6ArgumentsVoidIntegration));

        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6)
        {
            var returnValue = CallTargetState.GetDefault();
            Log.Information($"{returnValue} Noop6ArgumentsVoidIntegration.OnMethodBegin<{typeof(TTarget).FullName}, {typeof(TArg1).FullName}, {typeof(TArg2).FullName}, {typeof(TArg3).FullName}, {typeof(TArg4).FullName}, {typeof(TArg5).FullName}, {typeof(TArg6).FullName}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5}, {arg6})");
            return returnValue;
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            var rValue = new CallTargetReturn<TReturn>(returnValue);
            Log.Information($"{rValue} Noop6ArgumentsVoidIntegration.OnMethodEnd<{typeof(TTarget).FullName}>({instance}, {exception}, {state})");
            return rValue;
        }
    }
}
