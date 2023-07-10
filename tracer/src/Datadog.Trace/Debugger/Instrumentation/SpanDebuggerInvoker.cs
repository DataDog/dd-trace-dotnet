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
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

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
            operationName ??= "dd.dynamic.span";

            var tags = new CommonTags();
            tags.SetTag("debugger.probeid", probeId);
            tags.SetTag("component", "trace");
            var scope = Tracer.Instance.StartActiveInternal(operationName, tags: tags);
            scope.Span.ResourceName = resourceName;
            TelemetryFactory.Metrics.RecordCountSpanCreated(MetricTags.IntegrationName.DebuggerSpanProbe);
            return new SpanDebuggerState(scope);
        }

        /// <summary>
        /// [Non Async] End Method with Void return value invoker
        /// </summary>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndSpan(Exception exception, ref SpanDebuggerState state)
        {
            state.Scope.DisposeWithException(exception);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BeginSpan(string probeId, string resourceName, string operationName, ref AsyncDebuggerState isReEntryToMoveNext)
        {
            if (isReEntryToMoveNext.SpanState.HasValue)
            {
                // Re-enter.
                return;
            }

            var spanState = BeginSpan(probeId, resourceName, operationName);
            isReEntryToMoveNext.SpanState = spanState;
        }

        /// <summary>
        /// [Async] End Method with Void return value invoker
        /// </summary>
        /// <param name="exception">Exception value</param>
        /// <param name="state">Debugger state</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndSpan(Exception exception, ref AsyncDebuggerState state)
        {
            if (!state.SpanState.HasValue || !state.SpanState.Value.IsActive)
            {
                return;
            }

            state.SpanState.Value.Scope.DisposeWithException(exception);
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
                if (state.SpanState.HasValue && state.SpanState.Value.IsActive)
                {
                    // Already encountered `LogException`
                    return;
                }

                Log.Warning(exception, "Error caused by our instrumentation");
                state.SpanState = new SpanDebuggerState { IsActive = false };
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
