using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    public static class GenericRefModificationVoidIntegration
    {
        public const string SKIPMETHODBODY = "SKIPMETHODBODY";

        public static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, ref TArg1 arg01)
        {
            Console.WriteLine($"ProfilerOK: BeginMethod(1)<{typeof(GenericRefModificationVoidIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}>({instance}, {arg01})");
            
            if (arg01 is SKIPMETHODBODY)
            {
                return new CallTargetState(null, null, skipMethodBody: true);
            }
            
            return CallTargetState.GetDefault();
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(GenericRefModificationVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
            return CallTargetReturn.GetDefault();
        }
    }
}
