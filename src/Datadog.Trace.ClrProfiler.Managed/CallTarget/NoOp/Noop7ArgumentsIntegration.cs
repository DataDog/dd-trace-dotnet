using System;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements must be documented
#pragma warning disable SA1201 // Elements must appear in the correct order

namespace Datadog.Trace.ClrProfiler.CallTarget.NoOp
{
    /// <summary>
    /// NoOp Integration for 7 Arguments
    /// </summary>
    public static class Noop7ArgumentsIntegration
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(Noop7ArgumentsIntegration));

        public static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7)
            where TTarget : IInstance
            where TArg3 : IArg
        {
            CallTargetState returnValue = new CallTargetState(((IDuckType)instance).Instance);
            string msg = $"{returnValue} {nameof(Noop7ArgumentsIntegration)}.OnMethodBegin<{typeof(TTarget).FullName}, {typeof(TArg1).FullName}, {typeof(TArg2).FullName}, {typeof(TArg3).FullName}, {typeof(TArg4).FullName}, {typeof(TArg5).FullName}, {typeof(TArg6).FullName}, {typeof(TArg7).FullName}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5}, {arg6}, {arg7})";
            Log.Information(msg);
            Console.WriteLine(msg);
            return returnValue;
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance
            where TReturn : IReturnValue
        {
            CallTargetReturn<TReturn> rValue = new CallTargetReturn<TReturn>(returnValue);
            string msg = $"{rValue} {nameof(Noop7ArgumentsIntegration)}.OnMethodEnd<{typeof(TTarget).FullName}, {typeof(TReturn).FullName}>({instance}, {returnValue}, {exception}, {state})";
            Log.Information(msg);
            Console.WriteLine(msg);
            return rValue;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue, IDuckType
        {
            string msg = $"{returnValue} {nameof(Noop7ArgumentsIntegration)}.OnAsyncMethodEnd<{typeof(TTarget).FullName}, {typeof(TReturn).FullName}>({instance}, {returnValue}, {exception}, {state})";
            Log.Information(msg);
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
