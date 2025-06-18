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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DatadogHangfireServerFilter>();

        /// <summary>
        /// Called before the job is performed.
        /// </summary>
        /// <param name="context">The performing context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IServerFilter, Hangfire.Core" })]
        public void OnPerforming(object context)
        {
            var performingContext = context.DuckCast<IPerformingContextProxy>();
            var performContext = context.DuckCast<IPerformContextProxy>();
            SpanContext parentContext = null;
            if (performContext is not null && performingContext is not null)
            {
                var spanContextData = performContext.GetJobParameter<Dictionary<string, string>>(HangfireConstants.DatadogContextKey);
                Log.Debug("Extracting context from the followiing data: {SpanContextData}", spanContextData);
                PropagationContext propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(spanContextData);
                parentContext = propagationContext.SpanContext;
            }

            Scope scope = HangfireCommon.CreateScope(Tracer.Instance, HangfireConstants.OnPerformOperation, new HangfireTags(SpanKinds.Server), parentContext);
            if (scope is null)
            {
                return;
            }

            HangfireCommon.PopulatePerformSpanTags(scope, performingContext, performContext);
            ((Dictionary<string, object>)performContext?.Items)?.Add(HangfireConstants.DatadogScopeKey, scope);
        }

        /// <summary>
        /// Called after the job is performed.
        /// </summary>
        /// <param name="context">The performed context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IServerFilter, Hangfire.Core" })]
        public void OnPerformed(object context)
        {
            var performContext = context.DuckCast<IPerformContextProxy>();
            var performedContext = context.DuckCast<IPerformedContextProxy>();
            ((Dictionary<string, object>)performContext.Items).TryGetValue(HangfireConstants.DatadogScopeKey, out var scope);
            if (performedContext.Exception is not null)
            {
                HangfireCommon.SetStatusAndRecordException((Scope)scope, performedContext.Exception);
            }

            ((Scope)scope)?.Dispose();
        }
    }
}
