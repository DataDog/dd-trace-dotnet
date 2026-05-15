// <copyright file="StartActiveSpanWithParentSpanIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    /// <summary>
    /// Instrumentation for the <c>OpenTelemetry.Trace.Tracer.StartActiveSpan</c> overload that takes
    /// a <c>TelemetrySpan</c> parent. Captures the parent's Datadog <see cref="Scope"/> at the API
    /// boundary so <c>ActivityStartIntegration</c> can attach the new span to the parent's
    /// <c>TraceContext</c>, instead of treating the activity as a remote-parent root after the OTel
    /// SDK lowers the parent into an <c>ActivityContext</c>.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "OpenTelemetry.Api",
        TypeName = "OpenTelemetry.Trace.Tracer",
        MethodName = "StartActiveSpan",
        ReturnTypeName = "OpenTelemetry.Trace.TelemetrySpan",
        ParameterTypeNames = new[] { ClrNames.String, "OpenTelemetry.Trace.SpanKind", "OpenTelemetry.Trace.TelemetrySpan&", "OpenTelemetry.Trace.SpanAttributes", "System.Collections.Generic.IEnumerable`1[OpenTelemetry.Trace.Link]", "System.DateTimeOffset" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.0.0",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class StartActiveSpanWithParentSpanIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;

        // Sentinel placed on CallTargetState.State to signal that OnMethodBegin pushed an entry on
        // the explicit-parent stack. OnMethodEnd checks for this rather than re-reading the
        // integration-enabled flag, so a config flip mid-call doesn't leak a stack entry.
        private static readonly object PushedMarker = new();

        internal static CallTargetState OnMethodBegin<TTracer, TSpanKind, TParentSpan, TSpanAttributes, TLinks>(TTracer instance, string name, TSpanKind kind, in TParentSpan parentSpan, TSpanAttributes spanAttributes, TLinks links, DateTimeOffset startTime)
        {
            if (!Tracer.Instance.CurrentTraceSettings.Settings.IsIntegrationEnabled(IntegrationId))
            {
                return CallTargetState.GetDefault();
            }

            // Resolve the parent's Datadog Scope from the TelemetrySpan wrapper, then push it on the
            // thread-local stash so ActivityStartIntegration can find it inside Activity.Start()
            // (the OTel SDK strips the parent reference down to an ActivityContext before then).
            // We push unconditionally — including null — so OnMethodEnd can pop unconditionally.
            // The duck-cast hop via IActivity5 keeps this compiling on older TFMs whose pinned
            // System.Diagnostics.DiagnosticSource doesn't expose Activity.GetCustomProperty.
            Scope? parentScope = null;
            if (parentSpan is not null
             && parentSpan.TryDuckCast<ITelemetrySpan>(out var duckSpan)
             && duckSpan.Activity is { } parentActivity
             && parentActivity.TryDuckCast<IActivity5>(out var parentActivity5))
            {
                parentScope = parentActivity5.GetCustomProperty(ActivityCustomPropertyKeys.Span) as Scope;
            }

            OpenTelemetryInterceptionState.PushExplicitParent(parentScope);

            return new CallTargetState(scope: null, state: PushedMarker);
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            if (ReferenceEquals(state.State, PushedMarker))
            {
                // Pop the entry we pushed in OnMethodBegin.
                OpenTelemetryInterceptionState.PopExplicitParent();

                // StartActiveSpan is meant to leave the new span as the active span, so unlike
                // StartSpan / StartRootSpan we do NOT dispose the scope ActivityStartIntegration
                // pushed onto the active stack.
            }

            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
