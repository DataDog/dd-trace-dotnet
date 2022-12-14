// <copyright file="StartSpanIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    /// <summary>
    /// Msmq calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "OpenTelemetry.Api",
        TypeName = "OpenTelemetry.Trace.Tracer",
        MethodName = "StartSpan",
        ReturnTypeName = "OpenTelemetry.Trace.TelemetrySpan",
        ParameterTypeNames = new[] { ClrNames.String, "OpenTelemetry.Trace.SpanKind", "OpenTelemetry.Trace.SpanContext&", "OpenTelemetry.Trace.SpanAttributes", "System.Collections.Generic.IEnumerable`1[OpenTelemetry.Trace.Link]", "System.DateTimeOffset" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.0.0",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class StartSpanIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTracer">Type of the tracer</typeparam>
        /// <typeparam name="TSpanKind">Type of the span kind</typeparam>
        /// <typeparam name="TSpanContext">Type of the span context</typeparam>
        /// <typeparam name="TSpanAttributes">Type of the span attributes</typeparam>
        /// <typeparam name="TLinks">Type of the span links</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Span kind.</param>
        /// <param name="parentContext">Parent context for new span.</param>
        /// <param name="spanAttributes">Initial attributes for the span.</param>
        /// <param name="links">Links for the span.</param>
        /// <param name="startTimeinstance">Start time for the span.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTracer, TSpanKind, TSpanContext, TSpanAttributes, TLinks>(TTracer instance, string name, TSpanKind kind, in TSpanContext parentContext, TSpanAttributes spanAttributes, TLinks links, DateTimeOffset startTimeinstance)
        {
            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return new CallTargetState(Tracer.Instance.InternalActiveScope);
            }

            // integration disabled, don't create a scope, skip this trace
            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">Return value.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>CallTargetReturn</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, in CallTargetState state)
        {
            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // If the integration created a new scope, dispose it
                var currentScope = Tracer.Instance.InternalActiveScope;
                if (state.Scope != currentScope)
                {
                    currentScope.Dispose();
                }
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
