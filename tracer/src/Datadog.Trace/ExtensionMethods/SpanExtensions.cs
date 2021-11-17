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

        internal static void SetHttpStatusCode(this Span span, int statusCode, bool isServer, ImmutableTracerSettings tracerSettings)
        {
            string statusCodeString = ConvertStatusCodeToString(statusCode);

            if (span.Tags is IHasStatusCode statusCodeTags)
            {
                statusCodeTags.HttpStatusCode = statusCodeString;
            }
            else
            {
                span.SetTag(Tags.HttpStatusCode, statusCodeString);
            }

            // Check the customers http statuses that should be marked as errors
            if (tracerSettings.IsErrorStatusCode(statusCode, isServer))
            {
                span.Error = true;

                // if an error message already exists (e.g. from a previous exception), don't replace it
                if (string.IsNullOrEmpty(span.GetTag(Tags.ErrorMsg)))
                {
                    span.SetTag(Tags.ErrorMsg, $"The HTTP response has status code {statusCodeString}.");
                }
            }
        }

        private static string ConvertStatusCodeToString(int statusCode)
        {
            if (statusCode == 200)
            {
                return "200";
            }

            if (statusCode == 302)
            {
                return "302";
            }

            if (statusCode == 401)
            {
                return "401";
            }

            if (statusCode == 403)
            {
                return "403";
            }

            if (statusCode == 404)
            {
                return "404";
            }

            if (statusCode == 500)
            {
                return "500";
            }

            if (statusCode == 503)
            {
                return "503";
            }

            return statusCode.ToString();
        }
    }
}
