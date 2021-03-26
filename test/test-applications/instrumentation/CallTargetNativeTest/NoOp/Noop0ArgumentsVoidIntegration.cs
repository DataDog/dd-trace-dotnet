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
            Console.WriteLine($"ProfilerOK: BeginMethod(0)<{typeof(Noop0ArgumentsVoidIntegration)}, {typeof(TTarget)}>({instance})");
            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(Noop0ArgumentsVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
            return returnValue;
        }
    }
}
