// <copyright file="NotifyFaultedIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// MassTransit 7 only runs on .NET Core/.NET 5+, so we exclude .NET Framework
#if !NETFRAMEWORK
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.CallTarget
{
    /// <summary>
    /// MassTransit BaseReceiveContext.NotifyFaulted calltarget instrumentation.
    /// This captures exceptions that are not exposed through DiagnosticSource events.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "MassTransit",
        TypeName = "MassTransit.Context.BaseReceiveContext",
        MethodName = "NotifyFaulted",
        ReturnTypeName = "System.Threading.Tasks.Task",
        ParameterTypeNames = new[] { "MassTransit.ConsumeContext`1[!!0]", ClrNames.TimeSpan, ClrNames.String, ClrNames.Exception },
        MinimumVersion = "7.0.0",
        MaximumVersion = "7.*.*",
        IntegrationName = MassTransitConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class NotifyFaultedIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NotifyFaultedIntegration));

        /// <summary>
        /// OnMethodBegin callback.
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TContext">Type of the ConsumeContext</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="context">ConsumeContext instance</param>
        /// <param name="duration">Duration of the operation</param>
        /// <param name="consumerType">Consumer type name</param>
        /// <param name="exception">The exception that occurred</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TContext>(TTarget instance, TContext context, TimeSpan duration, string consumerType, Exception exception)
        {
            if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId.MassTransit))
            {
                return CallTargetState.GetDefault();
            }

            // Get the trace ID from the current Activity to use as a key for storing the exception.
            // We use TraceId instead of Activity.Id because NotifyFaulted may be called
            // from a child activity (e.g., Handle or Saga) while the DiagnosticObserver
            // Stop event fires on the parent activity (Consume). TraceId is shared across
            // the entire activity hierarchy.
            var activity = System.Diagnostics.Activity.Current;
            var traceId = MassTransitCommon.ExtractTraceIdFromActivity(activity);

            if (!string.IsNullOrEmpty(traceId) && exception != null)
            {
                MassTransitExceptionStore.StoreException(traceId!, exception);
                Log.Debug(
                    "NotifyFaultedIntegration.OnMethodBegin: Stored exception for TraceId={TraceId}, ExceptionType={ExceptionType}",
                    traceId,
                    exception.GetType().Name);
            }

            return CallTargetState.GetDefault();
        }
    }
}
#endif
