// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for the <see cref="ISpan"/> class.
    /// </summary>
    public static class SpanExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanExtensions));

        /// <summary>
        /// Sets the sampling priority for the trace that contains the specified <see cref="ISpan"/>.
        /// </summary>
        /// <param name="span">A span that belongs to the trace.</param>
        /// <param name="samplingPriority">The new sampling priority for the trace.</param>
        /// <remarks>
        /// This public extension method is meant for external users only. Internal Datadog calls should
        /// use the methods on <see cref="TraceContext"/> instead.</remarks>
        [PublicApi]
        public static void SetTraceSamplingPriority(this ISpan span, SamplingPriority samplingPriority)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.SpanExtensions_SetTraceSamplingPriority);
            SetTraceSamplingPriorityInternal(span, samplingPriority);
        }

        internal static void SetTraceSamplingPriorityInternal(this ISpan span, SamplingPriority samplingPriority)
        {
            if (span == null) { ThrowHelper.ThrowArgumentNullException(nameof(span)); }

            if (span.Context is SpanContext { TraceContext: { } traceContext })
            {
                traceContext.SetSamplingPriority((int)samplingPriority, SamplingMechanism.Manual);
            }
        }

        internal static void DecorateWebServerSpan(
            this ISpan span,
            string resourceName,
            string method,
            string host,
            string httpUrl,
            string userAgent,
            WebTags tags)
        {
            span.Type = SpanTypes.Web;
            span.ResourceName = resourceName?.Trim();

            if (tags is not null)
            {
                tags.HttpMethod = method;
                tags.HttpRequestHeadersHost = host;
                tags.HttpUrl = httpUrl;
                tags.HttpUserAgent = userAgent;
            }
        }

        internal static void SetHeaderTags<T>(this ISpan span, T headers, IReadOnlyDictionary<string, string> headerTags, string defaultTagPrefix)
            where T : IHeadersCollection
        {
            if (headerTags is not null && !headerTags.IsEmpty())
            {
                try
                {
                    SpanContextPropagator.Instance.AddHeadersToSpanAsTags(span, headers, headerTags, defaultTagPrefix);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated HTTP headers.");
                }
            }
        }

        internal static bool HasHttpStatusCode(this Span span)
        {
            if (span.Tags is IHasStatusCode statusCodeTags)
            {
                return statusCodeTags.HttpStatusCode is not null;
            }
            else
            {
                return span.GetTag(Tags.HttpStatusCode) is not null;
            }
        }

        internal static void SetHttpStatusCode(this Span span, int statusCode, bool isServer, ImmutableTracerSettings tracerSettings)
        {
            if (statusCode < 100 || statusCode >= 600)
            {
                // not a valid status code. Likely the default integer value
                return;
            }

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

        internal static string GetTraceIdStringForLogs(this ISpan span)
        {
            if (span is not Span s)
            {
                return span?.TraceId.ToString(CultureInfo.InvariantCulture);
            }

            var context = s.Context;
            var use128Bits = context.TraceContext?.Tracer?.Settings?.TraceId128BitLoggingEnabled ?? false;

            if (use128Bits && context.TraceId128.Upper > 0)
            {
                // encode all 128 bits of the trace id as a hex string
                return context.RawTraceId;
            }

            // encode only the lower 64 bits of the trace ids as decimal (not hex)
            return context.TraceId128.Lower.ToString(CultureInfo.InvariantCulture);
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
