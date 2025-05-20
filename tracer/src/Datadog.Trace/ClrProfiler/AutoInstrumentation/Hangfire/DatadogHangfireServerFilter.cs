// <copyright file="DatadogHangfireServerFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire
{
    /// <summary>
    /// The Datadog server-side Hangfire job filter.
    /// </summary>
    public class DatadogHangfireServerFilter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogHangfireServerFilter));

        /// <summary>
        /// Called before the job is performed.
        /// </summary>
        /// <param name="context">The performing context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IServerFilter, Hangfire.Core" })]
        public void OnPerforming(object context)
        {
            Log.Debug("Mock generate OnPerforming Span.");
            var performingContext = context.DuckCast<IPerformingContextProxy>();
            var performContext = context.DuckCast<IPerformContextProxy>();
            SpanContext parentContext = null;
            Log.Debug("Creating PropagationContext");

           // NameValueHeadersCollection spanContextData = performContext.GetJobParameter<NameValueHeadersCollection>("ScopeKey");
            var spanContextData = performContext.GetJobParameter<Dictionary<string, string>>("Alt_ScopeKey");
            PropagationContext propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(spanContextData);
            parentContext = propagationContext.SpanContext;
            Baggage.Current = propagationContext.Baggage;

            Scope scope = HangfireCommon.CreateScope(Tracer.Instance, "onPerforming", out HangfireTags tags, parentContext);
            Log.Debug("Creating Perfoming Span");
            if (scope is not null)
            {
                scope.Span.SetTag(Tags.SpanKind, SpanKinds.Server);
                performContext.SetJobParameter("DD_SCOPE", scope);
            }
        }

        /// <summary>
        /// Called after the job is performed.
        /// </summary>
        /// <param name="context">The performed context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IServerFilter, Hangfire.Core" })]
        public void OnPerformed(object context)
        {
            Tracer.Instance.ActiveScope?.Dispose();
            var performContext = context.DuckCast<IPerformContextProxy>();
            var scope = performContext.GetJobParameter<object>("DD_SCOPE");
            if (scope is not null)
            {
                ((Scope)scope).Dispose();
            }

            Log.Debug("Mock generate OnPerformed Span.");
        }
    }
}
