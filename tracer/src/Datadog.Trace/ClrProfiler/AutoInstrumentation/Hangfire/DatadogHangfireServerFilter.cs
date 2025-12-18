// <copyright file="DatadogHangfireServerFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire
{
    /// <summary>
    /// The Datadog server-side Hangfire job filter.
    /// </summary>
    public sealed class DatadogHangfireServerFilter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DatadogHangfireServerFilter>();

        /// <summary>
        /// Called before the job is performed.
        /// </summary>
        /// <param name="context">The performing context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IServerFilter, Hangfire.Core" })]
        public void OnPerforming(object context)
        {
            if (!context.TryDuckCast<IPerformingContextProxy>(out var performingContext))
            {
                return;
            }

            var spanContextData = performingContext.GetJobParameter<Dictionary<string, string?>?>(HangfireConstants.DatadogContextKey);
            PropagationContext propagationContext = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(spanContextData).MergeBaggageInto(Baggage.Current);
            var parentContext = propagationContext.SpanContext;
            Scope? scope = HangfireCommon.CreateScope(Tracer.Instance, new HangfireTags(), performingContext, parentContext);
            ((Dictionary<string, object?>)performingContext.Items).Add(HangfireConstants.DatadogScopeKey, scope);
        }

        /// <summary>
        /// Called after the job is performed.
        /// </summary>
        /// <param name="context">The performed context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Server.IServerFilter, Hangfire.Core" })]
        public void OnPerformed(object context)
        {
            if (context.TryDuckCast<IPerformedContextProxy>(out var performedContext))
            {
                if (performedContext.Items.TryGetValue(HangfireConstants.DatadogScopeKey, out var scope))
                {
                    if (scope is not Scope typedScope)
                    {
                        return;
                    }

                    if (performedContext.Exception is not null)
                    {
                        HangfireCommon.SetStatusAndRecordException(typedScope, performedContext.Exception);
                    }

                    typedScope.Dispose();
                }
            }
        }
    }
}
