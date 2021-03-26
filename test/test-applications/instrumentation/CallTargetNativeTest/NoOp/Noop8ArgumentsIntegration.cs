using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 8 Arguments
    /// </summary>
    public static class Noop8ArgumentsIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8)
            where TTarget : IInstance
            where TArg3 : IArg
        {
            CallTargetState returnValue = new CallTargetState(null, ((IDuckType)instance).Instance);
            Console.WriteLine($"ProfilerOK: BeginMethod(8)<{typeof(Noop8ArgumentsIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}, {typeof(TArg4)}, {typeof(TArg5)}, {typeof(TArg6)}, {typeof(TArg7)}, {typeof(TArg8)}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5}, {arg6}, {arg7}, {arg8})");
            return returnValue;
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance
            where TReturn : IReturnValue
        {
            CallTargetReturn<TReturn> rValue = new CallTargetReturn<TReturn>(returnValue);
            Console.WriteLine($"ProfilerOK: EndMethod(1)<{typeof(Noop8ArgumentsIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            return rValue;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance
            where TReturn : IReturnValue
        {
            Console.WriteLine($"ProfilerOK: EndMethodAsync(1)<{typeof(Noop8ArgumentsIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
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
