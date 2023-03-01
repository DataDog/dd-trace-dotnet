// <copyright file="Tracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Diagnostics;
using Datadog.Trace;

namespace Datadog.DiagnosticSource
{
    /// <summary>
    /// Tracer API
    /// </summary>
    public static class Tracer
    {
        /// <summary>
        /// Gets the current IDatadogActivity
        /// </summary>
        public static IDatadogActivity? ActiveScope => CreateDatadogActivity(CurrentDatadogActivity);

        /// <summary>
        /// Gets the current Activity.
        /// When automatic instrumentation is enabled, this may be an Activity coming from the automatic instrumentation.
        /// </summary>
        internal static object? CurrentDatadogActivity => Activity.Current;

        internal static IDatadogActivity? CreateDatadogActivity(object? input) =>
            input switch
            {
                Activity activity => new DatadogActivityWrapper(activity),
                ISpan span => new SpanActivity(span),
                _ => null
            };
    }
}
