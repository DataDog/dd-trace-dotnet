using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.CallTarget
{
    /// <summary>
    /// CallTarget Invoker
    /// </summary>
    public static class CallTargetInvoker
    {
        private static readonly bool IsTestMode = EnvironmentHelpers.GetEnvironmentVariable("DD_CTARGET_TESTMODE") == "True";

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <returns>Call target state</returns>
        public static CallTargetState BeginMethod<TIntegration, TInstance>(TInstance instance)
        {
            if (IsTestMode)
            {
                Console.WriteLine($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TInstance)}>({instance})");
            }

            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.BeginMethodHandler<TIntegration, TInstance>.Invoke(instance);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <typeparam name="TArg1">First argument type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arg1">First argument value</param>
        /// <returns>Call target state</returns>
        public static CallTargetState BeginMethod<TIntegration, TInstance, TArg1>(TInstance instance, TArg1 arg1)
        {
            if (IsTestMode)
            {
                Console.WriteLine($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TInstance)}, {typeof(TArg1)}>({instance}, {arg1})");
            }

            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.BeginMethodHandler<TIntegration, TInstance, TArg1>.Invoke(instance, arg1);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <typeparam name="TArg1">First argument type</typeparam>
        /// <typeparam name="TArg2">Second argument type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arg1">First argument value</param>
        /// <param name="arg2">Second argument value</param>
        /// <returns>Call target state</returns>
        public static CallTargetState BeginMethod<TIntegration, TInstance, TArg1, TArg2>(TInstance instance, TArg1 arg1, TArg2 arg2)
        {
            if (IsTestMode)
            {
                Console.WriteLine($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TInstance)}, {typeof(TArg1)}, {typeof(TArg2)}>({instance}, {arg1}, {arg2})");
            }

            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2>.Invoke(instance, arg1, arg2);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <typeparam name="TArg1">First argument type</typeparam>
        /// <typeparam name="TArg2">Second argument type</typeparam>
        /// <typeparam name="TArg3">Third argument type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arg1">First argument value</param>
        /// <param name="arg2">Second argument value</param>
        /// <param name="arg3">Third argument value</param>
        /// <returns>Call target state</returns>
        public static CallTargetState BeginMethod<TIntegration, TInstance, TArg1, TArg2, TArg3>(TInstance instance, TArg1 arg1, TArg2 arg2, TArg3 arg3)
        {
            if (IsTestMode)
            {
                Console.WriteLine($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TInstance)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}>({instance}, {arg1}, {arg2}, {arg3})");
            }

            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2, TArg3>.Invoke(instance, arg1, arg2, arg3);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <typeparam name="TArg1">First argument type</typeparam>
        /// <typeparam name="TArg2">Second argument type</typeparam>
        /// <typeparam name="TArg3">Third argument type</typeparam>
        /// <typeparam name="TArg4">Fourth argument type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arg1">First argument value</param>
        /// <param name="arg2">Second argument value</param>
        /// <param name="arg3">Third argument value</param>
        /// <param name="arg4">Fourth argument value</param>
        /// <returns>Call target state</returns>
        public static CallTargetState BeginMethod<TIntegration, TInstance, TArg1, TArg2, TArg3, TArg4>(TInstance instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
        {
            if (IsTestMode)
            {
                Console.WriteLine($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TInstance)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}, {typeof(TArg4)}>({instance}, {arg1}, {arg2}, {arg3}, {arg4})");
            }

            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2, TArg3, TArg4>.Invoke(instance, arg1, arg2, arg3, arg4);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <typeparam name="TArg1">First argument type</typeparam>
        /// <typeparam name="TArg2">Second argument type</typeparam>
        /// <typeparam name="TArg3">Third argument type</typeparam>
        /// <typeparam name="TArg4">Fourth argument type</typeparam>
        /// <typeparam name="TArg5">Fifth argument type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arg1">First argument value</param>
        /// <param name="arg2">Second argument value</param>
        /// <param name="arg3">Third argument value</param>
        /// <param name="arg4">Fourth argument value</param>
        /// <param name="arg5">Fifth argument value</param>
        /// <returns>Call target state</returns>
        public static CallTargetState BeginMethod<TIntegration, TInstance, TArg1, TArg2, TArg3, TArg4, TArg5>(TInstance instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
        {
            if (IsTestMode)
            {
                Console.WriteLine($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TInstance)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}, {typeof(TArg4)}, {typeof(TArg5)}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5})");
            }

            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2, TArg3, TArg4, TArg5>.Invoke(instance, arg1, arg2, arg3, arg4, arg5);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <typeparam name="TArg1">First argument type</typeparam>
        /// <typeparam name="TArg2">Second argument type</typeparam>
        /// <typeparam name="TArg3">Third argument type</typeparam>
        /// <typeparam name="TArg4">Fourth argument type</typeparam>
        /// <typeparam name="TArg5">Fifth argument type</typeparam>
        /// <typeparam name="TArg6">Sixth argument type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arg1">First argument value</param>
        /// <param name="arg2">Second argument value</param>
        /// <param name="arg3">Third argument value</param>
        /// <param name="arg4">Fourth argument value</param>
        /// <param name="arg5">Fifth argument value</param>
        /// <param name="arg6">Sixth argument value</param>
        /// <returns>Call target state</returns>
        public static CallTargetState BeginMethod<TIntegration, TInstance, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(TInstance instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6)
        {
            if (IsTestMode)
            {
                Console.WriteLine($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TInstance)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}, {typeof(TArg4)}, {typeof(TArg5)}, {typeof(TArg6)}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5}, {arg6})");
            }

            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.BeginMethodHandler<TIntegration, TInstance, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>.Invoke(instance, arg1, arg2, arg3, arg4, arg5, arg6);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker Slow Path
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arguments">Object arguments array</param>
        /// <returns>Call target state</returns>
        public static CallTargetState BeginMethod<TIntegration, TInstance>(TInstance instance, object[] arguments)
        {
            if (IsTestMode)
            {
                Console.WriteLine($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TInstance)}>({instance}, args: {arguments?.Length})");
            }

            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.BeginMethodSlowHandler<TIntegration, TInstance>.Invoke(instance, arguments);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <param name="exception">Exception value</param>
        /// <param name="state">CallTarget state</param>
        /// <returns>CallTarget return structure</returns>
        public static CallTargetReturn EndMethod<TIntegration, TInstance>(Exception exception, CallTargetState state)
        {
            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.EndMethodHandler<TIntegration, TInstance>.Invoke(exception, state);
            }

            return CallTargetReturn.GetDefault();
        }

        /// <summary>
        /// End Method with Return value invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <typeparam name="TReturn">Return type</typeparam>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">CallTarget state</param>
        /// <returns>CallTarget return structure</returns>
        public static CallTargetReturn<TReturn> EndMethod<TIntegration, TInstance, TReturn>(TReturn returnValue, Exception exception, CallTargetState state)
        {
            if (CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.IsIntegrationEnabled)
            {
                return CallTargetInvokerHandler.EndMethodHandler<TIntegration, TInstance, TReturn>.Invoke(returnValue, exception, state);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }

        /// <summary>
        /// Log integration exception
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TInstance">Instance type</typeparam>
        /// <param name="exception">Integration exception instance</param>
        public static void LogException<TIntegration, TInstance>(Exception exception)
        {
            CallTargetInvokerHandler.IntegrationOptions<TIntegration, TInstance>.LogException(exception);
        }

        /// <summary>
        /// Gets the default value of a type
        /// </summary>
        /// <typeparam name="T">Type to get the default value</typeparam>
        /// <returns>Default value of T</returns>
        public static T GetDefaultValue<T>() => default;
    }
}
