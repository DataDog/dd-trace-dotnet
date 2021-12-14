using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    public static class StringAndIntRefModificationVoidIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance, ref string stringValue, ref int intValue)
        {
            CallTargetState returnValue = CallTargetState.GetDefault();
            Console.WriteLine($"ProfilerOK: BeginMethod(2)<{typeof(StringAndIntRefModificationVoidIntegration)}, {typeof(TTarget)}>({instance})");
            
            // Let's modify the method arguments
            stringValue = stringValue + " (Modified)";
            intValue = 42;

            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, ref CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(StringAndIntRefModificationVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
            return returnValue;
        }
    }
}
