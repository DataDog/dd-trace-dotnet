// <copyright file="DatadogHangfireClientFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire
{
    /// <summary>
    /// The Datadog client-side Hangfire job filter.
    /// </summary>
    public class DatadogHangfireClientFilter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DatadogHangfireClientFilter>();

        /// <summary>
        /// Called before the job is created.
        /// </summary>
        /// <param name="context">The creating context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Client.IClientFilter, Hangfire.Core" })]
        public void OnCreating(object context)
        {
            Scope scope = (Scope)Tracer.Instance.ActiveScope;
            if (scope is null)
            {
                return;
            }

            if (context.TryDuckCast<ICreatingContextProxy>(out var creatingContext))
            {
                PropagationContext contextToInject = new PropagationContext(scope.Span.Context, Baggage.Current, null);
                var contextDetails = new Dictionary<string, string>();
                Tracer.Instance.TracerManager.SpanContextPropagator.Inject(contextToInject, contextDetails, HangfireCommon.InjectSpanProperties);
                creatingContext.SetJobParameter(HangfireConstants.DatadogContextKey, contextDetails);
            }
        }

        /// <summary>
        /// Called after the job is created.
        /// </summary>
        /// <param name="context">The created context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Client.IClientFilter, Hangfire.Core" })]
        public void OnCreated(object context)
        {
        }
    }
}
