using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 2 Arguments and Void Return
    /// </summary>
    public static class Noop2ArgumentsVoidIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2>(TTarget instance, TArg1 arg1, TArg2 arg2)
        {
            CallTargetState returnValue = CallTargetState.GetDefault();
            string msg = $"{returnValue} {nameof(Noop2ArgumentsVoidIntegration)}.OnMethodBegin<{typeof(TTarget).FullName}, {typeof(TArg1).FullName}, {typeof(TArg2).FullName}>({instance}, {arg1}, {arg2})";
            Console.WriteLine(msg);
            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            string msg = $"{returnValue} {nameof(Noop2ArgumentsVoidIntegration)}.OnMethodEnd<{typeof(TTarget).FullName}>({instance}, {exception}, {state})";
            Console.WriteLine(msg);
            return returnValue;
        }
    }
}
