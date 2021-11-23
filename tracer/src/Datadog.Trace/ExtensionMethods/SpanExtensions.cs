// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for the <see cref="Span"/> class.
    /// </summary>
    internal static class SpanExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanExtensions));

        /// <summary>
        /// Sets the sampling priority for the trace that contains the specified <see cref="Span"/>.
        /// </summary>
        /// <param name="span">A span that belongs to the trace.</param>
        /// <param name="samplingPriority">The new sampling priority for the trace.</param>
        internal static void SetTraceSamplingPriority(this Span span, SamplingPriority samplingPriority)
        {
            if (span == null) { throw new ArgumentNullException(nameof(span)); }

            if (span.InternalContext.TraceContext != null)
            {
                span.InternalContext.TraceContext.SamplingPriority = samplingPriority;
            }
        }
    }
}
