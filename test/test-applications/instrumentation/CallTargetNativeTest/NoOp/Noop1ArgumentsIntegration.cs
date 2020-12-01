using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace CallTargetNativeTest.NoOp
{
    /// <summary>
    /// NoOp Integration for 1 Arguments
    /// </summary>
    public static class Noop1ArgumentsIntegration
    {
        public static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 arg1)
            where TTarget : IInstance, IDuckType
        {
            CallTargetState returnValue = new CallTargetState(instance.Instance);
            string msg = $"{returnValue} {nameof(Noop1ArgumentsIntegration)}.OnMethodBegin<{typeof(TTarget).FullName}, {typeof(TArg1).FullName}>({instance}, {arg1})";
            Console.WriteLine(msg);
            return returnValue;
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue, IDuckType
        {
            CallTargetReturn<TReturn> rValue = new CallTargetReturn<TReturn>(returnValue);
            string msg = $"{rValue} {nameof(Noop1ArgumentsIntegration)}.OnMethodEnd<{typeof(TTarget).FullName}, {typeof(TReturn).FullName}>({instance}, {returnValue}, {exception}, {state})";
            Console.WriteLine(msg);
            return rValue;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue, IDuckType
        {
            string msg = $"{returnValue} {nameof(Noop1ArgumentsIntegration)}.OnAsyncMethodEnd<{typeof(TTarget).FullName}, {typeof(TReturn).FullName}>({instance}, {returnValue}, {exception}, {state})";
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
