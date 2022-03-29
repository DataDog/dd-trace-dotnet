using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    public static class GenericOutModificationVoidIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, out TArg1 arg01)
        {
            arg01 = default;
            CallTargetState returnValue = CallTargetState.GetDefault();
            Console.WriteLine($"ProfilerOK: BeginMethod(1)<{typeof(GenericOutModificationVoidIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}>({instance}, {arg01})");
            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(GenericOutModificationVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
            return returnValue;
        }
    }
}
