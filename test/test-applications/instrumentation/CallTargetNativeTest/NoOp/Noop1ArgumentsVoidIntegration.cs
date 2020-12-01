using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 1 Arguments and Void Return
    /// </summary>
    public static class Noop1ArgumentsVoidIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 arg1)
        {
            CallTargetState returnValue = CallTargetState.GetDefault();
            string msg = $"{returnValue} {nameof(Noop1ArgumentsVoidIntegration)}.OnMethodBegin<{typeof(TTarget).FullName}, {typeof(TArg1).FullName}>({instance}, {arg1})";
            Console.WriteLine(msg);
            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            string msg = $"{returnValue} {nameof(Noop1ArgumentsVoidIntegration)}.OnMethodEnd<{typeof(TTarget).FullName}>({instance}, {exception}, {state})";
            Console.WriteLine(msg);
            return returnValue;
        }
    }
}
