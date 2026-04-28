// <copyright file="NotifyFaultedIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

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

            // Set the exception directly on the active scope — AsyncLocal ensures it is exactly
            // the scope for the faulted operation.
            var scope = Tracer.Instance.ActiveScope as Scope;

            if (scope != null && exception != null)
            {
                MassTransitCommon.SetException(scope, exception);
                Log.Debug(
                    "NotifyFaultedIntegration.OnMethodBegin: Set exception on span SpanId={SpanId}, ExceptionType={ExceptionType}",
                    scope.Span.SpanId,
                    exception.GetType().Name);
            }

            return CallTargetState.GetDefault();
        }
    }
}
