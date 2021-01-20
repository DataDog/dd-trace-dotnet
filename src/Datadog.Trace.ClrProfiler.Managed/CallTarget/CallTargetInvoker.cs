using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.CallTarget.Handlers;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.CallTarget
{
    /// <summary>
    /// CallTarget Invoker
    /// </summary>
    public static class CallTargetInvoker
    {
#if DEBUG
        private static readonly bool IsTestMode = EnvironmentHelpers.GetEnvironmentVariable("DD_CTARGET_TESTMODE") == "True";
#endif

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <returns>Call target state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState BeginMethod<TIntegration, TTarget>(TTarget instance)
        {
            DebugLog($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TTarget)}>({instance})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return BeginMethodHandler<TIntegration, TTarget>.Invoke(instance);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <typeparam name="TArg1">First argument type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arg1">First argument value</param>
        /// <returns>Call target state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1>(TTarget instance, TArg1 arg1)
        {
            DebugLog($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}>({instance}, {arg1})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return BeginMethodHandler<TIntegration, TTarget, TArg1>.Invoke(instance, arg1);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <typeparam name="TArg1">First argument type</typeparam>
        /// <typeparam name="TArg2">Second argument type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arg1">First argument value</param>
        /// <param name="arg2">Second argument value</param>
        /// <returns>Call target state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2>(TTarget instance, TArg1 arg1, TArg2 arg2)
        {
            DebugLog($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}>({instance}, {arg1}, {arg2})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2>.Invoke(instance, arg1, arg2);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <typeparam name="TArg1">First argument type</typeparam>
        /// <typeparam name="TArg2">Second argument type</typeparam>
        /// <typeparam name="TArg3">Third argument type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arg1">First argument value</param>
        /// <param name="arg2">Second argument value</param>
        /// <param name="arg3">Third argument value</param>
        /// <returns>Call target state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3)
        {
            DebugLog($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}>({instance}, {arg1}, {arg2}, {arg3})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3>.Invoke(instance, arg1, arg2, arg3);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4)
        {
            DebugLog($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}, {typeof(TArg4)}>({instance}, {arg1}, {arg2}, {arg3}, {arg4})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4>.Invoke(instance, arg1, arg2, arg3, arg4);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5)
        {
            DebugLog($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}, {typeof(TArg4)}, {typeof(TArg5)}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5>.Invoke(instance, arg1, arg2, arg3, arg4, arg5);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState BeginMethod<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>(TTarget instance, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6)
        {
            DebugLog($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TTarget)}, {typeof(TArg1)}, {typeof(TArg2)}, {typeof(TArg3)}, {typeof(TArg4)}, {typeof(TArg5)}, {typeof(TArg6)}>({instance}, {arg1}, {arg2}, {arg3}, {arg4}, {arg5}, {arg6})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return BeginMethodHandler<TIntegration, TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6>.Invoke(instance, arg1, arg2, arg3, arg4, arg5, arg6);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// Begin Method Invoker Slow Path
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="arguments">Object arguments array</param>
        /// <returns>Call target state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetState BeginMethod<TIntegration, TTarget>(TTarget instance, object[] arguments)
        {
            DebugLog($"ProfilerOK: BeginMethod<{typeof(TIntegration)}, {typeof(TTarget)}>({instance}, args: {arguments?.Length})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return BeginMethodSlowHandler<TIntegration, TTarget>.Invoke(instance, arguments);
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// End Method with Void return value invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">CallTarget state</param>
        /// <returns>CallTarget return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetReturn EndMethod<TIntegration, TTarget>(TTarget instance, Exception exception, CallTargetState state)
        {
            DebugLog($"ProfilerOK: EndMethod<{typeof(TIntegration)}, {typeof(TTarget)}>({instance}, {exception?.ToString() ?? "(null)"}, {state})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return EndMethodHandler<TIntegration, TTarget>.Invoke(instance, exception, state);
            }

            return CallTargetReturn.GetDefault();
        }

        /// <summary>
        /// End Method with Return value invoker
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <typeparam name="TReturn">Return type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">CallTarget state</param>
        /// <returns>CallTarget return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CallTargetReturn<TReturn> EndMethod<TIntegration, TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            DebugLog($"ProfilerOK: EndMethod<{typeof(TIntegration)}, {typeof(TTarget)}, {typeof(TReturn)}>({instance}, {returnValue}, {exception?.ToString() ?? "(null)"}, {state})");

            if (IntegrationOptions<TIntegration, TTarget>.IsIntegrationEnabled)
            {
                return EndMethodHandler<TIntegration, TTarget, TReturn>.Invoke(instance, returnValue, exception, state);
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }

        /// <summary>
        /// Log integration exception
        /// </summary>
        /// <typeparam name="TIntegration">Integration type</typeparam>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="exception">Integration exception instance</param>
        /// <param name="sourceLine">Auto-generated <see cref="CallerLineNumberAttribute"/> for log rate limiting. Must be left unspecified</param>
        /// <param name="sourceFile">Auto-generated <see cref="CallerFilePathAttribute"/> for log rate limiting. Must be left unspecified</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException<TIntegration, TTarget>(Exception exception, [CallerLineNumber] int sourceLine = 0, [CallerFilePath] string sourceFile = "")
        {
            DebugLog($"ProfilerOK: LogException<{typeof(TIntegration)}, {typeof(TTarget)}>({exception})");
            IntegrationOptions<TIntegration, TTarget>.LogException(exception, sourceLine, sourceFile);
        }

        /// <summary>
        /// Gets the default value of a type
        /// </summary>
        /// <typeparam name="T">Type to get the default value</typeparam>
        /// <returns>Default value of T</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetDefaultValue<T>() => default;

        [Conditional("DEBUG")]
        private static void DebugLog(string message)
        {
#if DEBUG
            if (IsTestMode)
            {
                Console.WriteLine(message);
            }
#endif
        }
    }
}
