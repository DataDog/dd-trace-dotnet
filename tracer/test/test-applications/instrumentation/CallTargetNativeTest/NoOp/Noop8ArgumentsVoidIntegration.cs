using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 8 Arguments and Void Return
    /// </summary>
    public static class Noop8ArgumentsVoidIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8)
        {
            CallTargetState returnValue = CallTargetState.GetDefault();
            Console.WriteLine($"ProfilerOK: BeginMethod(8)<{typeof(Noop8ArgumentsVoidIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}, {typeof(TArg4)}, {typeof(TArg5)}, {typeof(TArg6)}, {typeof(TArg7)}, {typeof(TArg8)}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5}, {arg6}, {arg7}, {arg8})");
            if (instance?.GetType().Name.Contains("ThrowOnBegin") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }

            return returnValue;
        }

        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            CallTargetReturn returnValue = CallTargetReturn.GetDefault();
            Console.WriteLine($"ProfilerOK: EndMethod(0)<{typeof(Noop8ArgumentsVoidIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");
            if (instance?.GetType().Name.Contains("ThrowOnEnd") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }

            return returnValue;
        }
    }
}
