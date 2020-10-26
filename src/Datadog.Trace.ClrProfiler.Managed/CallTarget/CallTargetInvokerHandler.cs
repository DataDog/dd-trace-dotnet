using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.CallTarget
{
    internal class CallTargetInvokerHandler
    {
        internal static class IntegrationOptions<TIntegration, TInstance>
        {
            private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(IntegrationOptions<TIntegration, TInstance>));

            private static volatile bool _disableIntegration = false;

            internal static bool IsIntegrationEnabled => !_disableIntegration;

            internal static void DisableIntegration() => _disableIntegration = true;

            internal static void LogException(Exception exception)
            {
                Log.SafeLogError(exception, exception?.Message, null);
                if (exception is DuckTypeException)
                {
                    Log.Warning($"DuckTypeException has been detected, the integration <{typeof(TIntegration)}, {typeof(TInstance)}> will be disabled.");
                    _disableIntegration = true;
                }
                else if (exception is CallTargetInvokerException)
                {
                    Log.Warning($"CallTargetInvokerException has been detected, the integration <{typeof(TIntegration)}, {typeof(TInstance)}> will be disabled.");
                    _disableIntegration = true;
                }
            }
        }

        internal static class BeginMethodHandler<TIntegration, TInstance>
        {
            internal static CallTargetState Invoke(TInstance instance)
            {
                return CallTargetState.GetDefault();
            }
        }

        internal static class BeginMethodHandler<TIntegration, TInstance, TArg1>
        {
            internal static CallTargetState Invoke(TInstance instance, TArg1 arg1)
            {
                return CallTargetState.GetDefault();
            }
        }

        internal static class BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2>
        {
            internal static CallTargetState Invoke(TInstance instance, TArg1 arg1, TArg2 arg2)
            {
                return CallTargetState.GetDefault();
            }
        }

        internal static class BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2, TArg3>
        {
            internal static CallTargetState Invoke(TInstance instance, TArg1 arg1, TArg2 arg2, TArg3 arg3)
            {
                return CallTargetState.GetDefault();
            }
        }

        internal static class BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2, TArg3, TArg4>
        {
            internal static CallTargetState Invoke(TInstance instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
            {
                return CallTargetState.GetDefault();
            }
        }

        internal static class BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2, TArg3, TArg4, TArg5>
        {
            internal static CallTargetState Invoke(TInstance instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
            {
                return CallTargetState.GetDefault();
            }
        }

        internal static class BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>
        {
            internal static CallTargetState Invoke(TInstance instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6)
            {
                return CallTargetState.GetDefault();
            }
        }

        internal static class BeginMethodSlowHandler<TIntegration, TInstance>
        {
            internal static CallTargetState Invoke(TInstance instance, object[] arguments)
            {
                return CallTargetState.GetDefault();
            }
        }

        internal static class EndMethodHandler<TIntegration, TInstance>
        {
            internal static CallTargetReturn Invoke(Exception exception, CallTargetState state)
            {
                return CallTargetReturn.GetDefault();
            }
        }

        internal static class EndMethodHandler<TIntegration, TInstance, TReturn>
        {
            internal static CallTargetReturn<TReturn> Invoke(TReturn returnValue, Exception exception, CallTargetState state)
            {
                return new CallTargetReturn<TReturn>(returnValue);
            }
        }
    }
}
