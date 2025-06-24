using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 1 Arguments and Void Return
    /// </summary>
    public static class Noop1ArgumentsVoidIntegration
    {
        public const string SKIPMETHODBODY = "SKIPMETHODBODY";

        public static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 arg1)
        {
            Console.WriteLine($"ProfilerOK: BeginMethod(1)<{typeof(Noop1ArgumentsVoidIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}>({instance}, {arg1})");
            if (instance?.GetType().Name.Contains("ThrowOnBegin") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }

            Console.WriteLine(arg1 as string);
            if (arg1 as string == SKIPMETHODBODY)
            {
                return new CallTargetState(null, null, skipMethodBody: true);
            }

            return CallTargetState.GetDefault();
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, in CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(Noop1ArgumentsVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
            if (instance?.GetType().Name.Contains("ThrowOnEnd") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }
            
            return returnValue;
        }
    }
}
