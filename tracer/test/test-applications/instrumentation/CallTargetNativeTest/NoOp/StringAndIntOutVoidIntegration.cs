using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    public static class StringAndIntOutVoidIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, out string stringValue, out int intValue)
        {
            CallTargetState returnValue = CallTargetState.GetDefault();
            Console.WriteLine($"ProfilerOK: BeginMethod(2)<{typeof(StringAndIntOutVoidIntegration)}, {typeof(TTarget)}>({instance})");

            stringValue = "stringValue";
            intValue = 12;

            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, ref CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(StringAndIntOutVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
            return returnValue;
        }
    }
}
