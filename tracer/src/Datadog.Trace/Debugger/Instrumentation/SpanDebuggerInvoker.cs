// <copyright file="SpanDebuggerInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.AutoInstrumentation;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.TraceAnnotations;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation
{
    /// <summary>
    /// SpanDebuggerInvoker for Async / Non-Async methods.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class SpanDebuggerInvoker
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanDebuggerInvoker));
        internal static readonly IntegrationId IntegrationId = IntegrationId.SpanProbe;

        /// <summary>
        /// [Non Async] Begin Method Invoker
        /// </summary>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="resourceName">The resource name</param>
        /// <param name="operationName">The operation name</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpanDebuggerState BeginSpan(string probeId, string resourceName, string operationName)
        {
            resourceName ??= "unknown-resource";
            operationName ??= "unknown-operation";

            return new SpanDebuggerState(TraceAnnotationsIntegration.CreateSpan(resourceName, operationName, IntegrationId));
        }

        /// <summary>
        /// [Non Async] End Method with Void return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        /// <returns>CallTarget return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn EndSpan<TTarget>(TTarget instance, Exception exception, ref SpanDebuggerState state)
        {
            state.Scope.DisposeWithException(exception);
            return DebuggerReturn.GetDefault();
        }

        /// <summary>
        /// [Non Async] End Method with Return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <typeparam name="TReturn">Return type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<TReturn> EndSpan<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref SpanDebuggerState state)
        {
            state.Scope.DisposeWithException(exception);
            return new DebuggerReturn<TReturn>(returnValue);
        }

        /// <summary>
        /// [Non Async] Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref SpanDebuggerState state)
        {
            try
            {
                if (!state.IsActive)
                {
                    // Already encountered `LogException`
                    return;
                }

                Log.Warning(exception, "Error caused by our instrumentation");
                state.IsActive = false;
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// [Async] Begin Method Invoker
        /// </summary>
        /// <param name="probeId">The id of the probe</param>
        /// <param name="resourceName">The resource name</param>
        /// <param name="operationName">The operation name</param>
        /// <param name="isReEntryToMoveNext">Indication if we are dealing with re-entry</param>
        /// <returns>Live debugger state</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AsyncDebuggerState BeginSpan(string probeId, string resourceName, string operationName, ref AsyncDebuggerState isReEntryToMoveNext)
        {
            if (isReEntryToMoveNext.State != null)
            {
                // Re-enter.
                return isReEntryToMoveNext;
            }

            var spanState = BeginSpan(probeId, resourceName, operationName);
            isReEntryToMoveNext = new AsyncDebuggerState(spanState);
            return isReEntryToMoveNext;
        }

        /// <summary>
        /// [Async] End Method with Void return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        /// <returns>CallTarget return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn EndSpan<TTarget>(TTarget instance, Exception exception, ref AsyncDebuggerState state)
        {
            if (state.State is not AsyncMethodDebuggerState asyncState || !asyncState.IsActive)
            {
                return DebuggerReturn.GetDefault();
            }

            asyncState.Scope.DisposeWithException(exception);
            return DebuggerReturn.GetDefault();
        }

        /// <summary>
        /// [Async] End Method with Return value invoker
        /// </summary>
        /// <typeparam name="TTarget">Target type</typeparam>
        /// <typeparam name="TReturn">Return type</typeparam>
        /// <param name="instance">Instance value</param>
        /// <param name="returnValue">Return value</param>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        /// <returns>LiveDebugger return structure</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DebuggerReturn<TReturn> EndSpan<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, ref AsyncDebuggerState state)
        {
            if (state.State is not AsyncMethodDebuggerState asyncState || !asyncState.IsActive)
            {
                return new DebuggerReturn<TReturn>(returnValue);
            }

            asyncState.Scope.DisposeWithException(exception);
            return new DebuggerReturn<TReturn>(returnValue);
        }

        /// <summary>
        /// [Async] Log exception
        /// </summary>
        /// <param name="exception">Exception instance</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LogException(Exception exception, ref AsyncDebuggerState state)
        {
            try
            {
                if (state.State is not AsyncMethodDebuggerState asyncState || !asyncState.IsActive)
                {
                    // Already encountered `LogException`
                    return;
                }

                Log.Warning(exception, "Error caused by our instrumentation");
                asyncState.IsActive = false;
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Gets the default value of a type
        /// </summary>
        /// <typeparam name="T">Type to get the default value</typeparam>
        /// <returns>Default value of T</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetDefaultValue<T>() => default;
    }
}
