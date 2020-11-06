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
    /// NoOp Integration for 0 Arguments
    /// </summary>
    public static class Noop0ArgumentsIntegration
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(Noop0ArgumentsIntegration));

        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : IInstance, IDuckType
        {
            CallTargetState returnValue = new CallTargetState(instance.Instance);
            string msg = $"{returnValue} {nameof(Noop0ArgumentsIntegration)}.OnMethodBegin<{typeof(TTarget).FullName}>({instance})";
            Log.Information(msg);
            Console.WriteLine(msg);
            return returnValue;
        }

        public static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue, IDuckType
        {
            CallTargetReturn<TReturn> rValue = new CallTargetReturn<TReturn>(returnValue);
            string msg = $"{rValue} {nameof(Noop0ArgumentsIntegration)}.OnMethodEnd<{typeof(TTarget).FullName}, {typeof(TReturn).FullName}>({instance}, {returnValue}, {exception}, {state})";
            Log.Information(msg);
            Console.WriteLine(msg);
            return rValue;
        }

        public static TReturn OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
            where TTarget : IInstance, IDuckType
            where TReturn : IReturnValue, IDuckType
        {
            string msg = $"{returnValue} {nameof(Noop0ArgumentsIntegration)}.OnAsyncMethodEnd<{typeof(TTarget).FullName}, {typeof(TReturn).FullName}>({instance}, {returnValue}, {exception}, {state})";
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
