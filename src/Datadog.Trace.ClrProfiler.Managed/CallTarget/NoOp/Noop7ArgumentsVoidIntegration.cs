using System;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements must be documented

namespace Datadog.Trace.ClrProfiler.CallTarget.NoOp
{
    /// <summary>
    /// NoOp Integration for 7 Arguments and Void Return
    /// </summary>
    public static class Noop7ArgumentsVoidIntegration
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(Noop7ArgumentsVoidIntegration));

        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7)
        {
            CallTargetState returnValue = CallTargetState.GetDefault();
            string msg = $"{returnValue} {nameof(Noop7ArgumentsVoidIntegration)}.OnMethodBegin<{typeof(TTarget).FullName}, {typeof(TArg1).FullName}, {typeof(TArg2).FullName}, {typeof(TArg3).FullName}, {typeof(TArg4).FullName}, {typeof(TArg5).FullName}, {typeof(TArg6).FullName}, {typeof(TArg7).FullName}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5}, {arg6}, {arg7})";
            Log.Information(msg);
            Console.WriteLine(msg);
            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            string msg = $"{returnValue} {nameof(Noop7ArgumentsVoidIntegration)}.OnMethodEnd<{typeof(TTarget).FullName}>({instance}, {exception}, {state})";
            Log.Information(msg);
            Console.WriteLine(msg);
            return returnValue;
        }

        public static CallTargetReturn OnAsyncMethodEnd<TTarget>(TTarget instance, Exception exception, Task originalTask, CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            string msg = $"{returnValue} {nameof(Noop7ArgumentsVoidIntegration)}.OnAsyncMethodEnd<{typeof(TTarget).FullName}>({instance}, {exception}, {originalTask}, {state})";
            Log.Information(msg);
            Console.WriteLine(msg);
            return returnValue;
        }
    }
}
