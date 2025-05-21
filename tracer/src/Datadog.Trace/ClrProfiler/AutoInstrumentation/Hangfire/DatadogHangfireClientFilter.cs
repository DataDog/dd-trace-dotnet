// <copyright file="DatadogHangfireClientFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;

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
            var creatingContext = context.DuckCast<ICreatingContextProxy>();
            var createContext = context.DuckCast<ICreateContextProxy>();
            Scope scope = HangfireCommon.CreateScope(Tracer.Instance, HangfireConstants.OnCreateOperation, SpanKinds.Client);
            if (scope is not null)
            {
                if (createContext is not null && creatingContext is not null)
                {
                    HangfireCommon.PopulateCreateSpanTags(scope, creatingContext, createContext);
                    ((Dictionary<string, object>)createContext.Items).Add(HangfireConstants.DatadogScopeKey, scope);
                    PropagationContext contextToInject = new PropagationContext(scope.Span.Context, null, null);
                    var contextDetails = new Dictionary<string, string>();
                    Tracer.Instance.TracerManager.SpanContextPropagator.Inject(contextToInject, contextDetails, HangfireCommon.InjectSpanProperties);
                    creatingContext.SetJobParameter(HangfireConstants.DatadogContextKey, contextDetails);
                }
            }
        }

        /// <summary>
        /// Called after the job is created.
        /// </summary>
        /// <param name="context">The created context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Client.IClientFilter, Hangfire.Core" })]
        public void OnCreated(object context)
        {
            var createContext = context.DuckCast<ICreateContextProxy>();
            // ((ReadOnlyDictionary<string, object>)createdContext.Parameters).TryGetValue("DD_SCOPE", out var scope);
            ((Dictionary<string, object>)createContext.Items).TryGetValue(HangfireConstants.DatadogScopeKey, out var scope);
            ((Scope)scope)?.Dispose();
        }
    }
}
