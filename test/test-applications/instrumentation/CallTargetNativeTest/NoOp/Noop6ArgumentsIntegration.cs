using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 6 Arguments
    /// </summary>
    public static class Noop6ArgumentsIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6)
            where TTarget : IInstance, IDuckType
            where TArg3 : IArg, IDuckType
        {
            CallTargetState returnValue = new CallTargetState(instance.Instance);
            string msg = $"{returnValue} {nameof(Noop6ArgumentsIntegration)}.OnMethodBegin<{typeof(TTarget).FullName}, {typeof(TArg1).FullName}, {typeof(TArg2).FullName}, {typeof(TArg3).FullName}, {typeof(TArg4).FullName}, {typeof(TArg5).FullName}, {typeof(TArg6).FullName}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5}, {arg6})";
            Console.WriteLine(msg);
            return returnValue;
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue, IDuckType
        {
            CallTargetReturn<TReturn> rValue = new CallTargetReturn<TReturn>(returnValue);
            string msg = $"{rValue} {nameof(Noop6ArgumentsIntegration)}.OnMethodEnd<{typeof(TTarget).FullName}, {typeof(TReturn).FullName}>({instance}, {returnValue}, {exception}, {state})";
            Console.WriteLine(msg);
            return rValue;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue, IDuckType
        {
            string msg = $"{returnValue} {nameof(Noop6ArgumentsIntegration)}.OnAsyncMethodEnd<{typeof(TTarget).FullName}, {typeof(TReturn).FullName}>({instance}, {returnValue}, {exception}, {state})";
            Console.WriteLine(msg);
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
