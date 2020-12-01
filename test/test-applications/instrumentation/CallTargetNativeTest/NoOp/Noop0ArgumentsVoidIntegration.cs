using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 0 Arguments and Void Return
    /// </summary>
    public static class Noop0ArgumentsVoidIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            CallTargetState returnValue = CallTargetState.GetDefault();
            string msg = $"{returnValue} {nameof(Noop0ArgumentsVoidIntegration)}.OnMethodBegin<{typeof(TTarget).FullName}>({instance})";
            Console.WriteLine(msg);
            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            string msg = $"{returnValue} {nameof(Noop0ArgumentsVoidIntegration)}.OnMethodEnd<{typeof(TTarget).FullName}>({instance}, {exception}, {state})";
            Console.WriteLine(msg);
            return returnValue;
        }
    }
}
