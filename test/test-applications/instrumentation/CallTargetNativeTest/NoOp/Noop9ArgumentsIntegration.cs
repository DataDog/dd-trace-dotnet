using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 9 Arguments
    /// </summary>
    public static class Noop9ArgumentsIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9)
            where TTarget : IInstance, IDuckType
            where TArg3 : IArg
        {
            CallTargetState returnValue = new CallTargetState(null, ((IDuckType)instance).Instance);
            Console.WriteLine($"ProfilerOK: BeginMethod(Array)<{typeof(Noop9ArgumentsIntegration)}, {typeof(TTarget)}>({instance})");
            return returnValue;
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance
            where TReturn : IReturnValue
        {
            CallTargetReturn<TReturn> rValue = new CallTargetReturn<TReturn>(returnValue);
            Console.WriteLine($"ProfilerOK: EndMethod(1)<{typeof(Noop9ArgumentsIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
            return rValue;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance
            where TReturn : IReturnValue
        {
            Console.WriteLine($"ProfilerOK: EndMethodAsync(1)<{typeof(Noop9ArgumentsIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");
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
