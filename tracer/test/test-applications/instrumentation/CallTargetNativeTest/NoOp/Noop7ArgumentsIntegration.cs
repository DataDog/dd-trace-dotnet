using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 7 Arguments
    /// </summary>
    public static class Noop7ArgumentsIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7)
            where TTarget : IInstance, IDuckType
            where TArg3 : IArg
        {
            CallTargetState returnValue = new CallTargetState(null, ((IDuckType)instance).Instance);
            Console.WriteLine($"ProfilerOK: BeginMethod(7)<{typeof(Noop7ArgumentsIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}, {typeof(TArg4)}, {typeof(TArg5)}, {typeof(TArg6)}, {typeof(TArg7)}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5}, {arg6}, {arg7})");
            if (instance.Instance?.GetType().Name.Contains("ThrowOnBegin") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }

            return returnValue;
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue
        {
            CallTargetReturn<TReturn> rValue = new CallTargetReturn<TReturn>(returnValue);
            Console.WriteLine($"ProfilerOK: EndMethod(1)<{typeof(Noop7ArgumentsIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            if (instance.Instance?.GetType().Name.Contains("ThrowOnEnd") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }

            return rValue;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue
        {
            Console.WriteLine($"ProfilerOK: EndMethodAsync(1)<{typeof(Noop7ArgumentsIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            if (instance.Instance?.GetType().Name.Contains("ThrowOnAsyncEnd") == true)
            {
                Console.WriteLine("Exception thrown.");
                throw new Exception();
            }

            return returnValue;
        }

        public interface IInstance
        {
        }

        public interface IArg
        {
        }

        public interface IReturnValue
        {
        }
    }
}
