// <copyright file="DatadogHangfireClientFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Hangfire
{
    /// <summary>
    /// The Datadog client-side Hangfire job filter.
    /// </summary>
    public class DatadogHangfireClientFilter
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogHangfireClientFilter));

        /// <summary>
        /// Called before the job is created.
        /// </summary>
        /// <param name="context">The creating context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Client.IClientFilter, Hangfire.Core" })]
        public void OnCreating(object context)
        {
            Log.Debug("Mock generate OnCreating Span.");
        }

        /// <summary>
        /// Called after the job is created.
        /// </summary>
        /// <param name="context">The created context.</param>
        [DuckReverseMethod(ParameterTypeNames = new[] { "Hangfire.Client.IClientFilter, Hangfire.Core" })]
        public void OnCreated(object context)
        {
            Log.Debug("Mock generate OnCreated Span.");
        }
    }
}
