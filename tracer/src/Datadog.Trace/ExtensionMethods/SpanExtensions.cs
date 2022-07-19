// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for the <see cref="ISpan"/> class.
    /// </summary>
    public static class SpanExtensions
    {
        /// <summary>
        /// Sets the sampling priority for the trace that contains the specified <see cref="ISpan"/>.
        /// </summary>
        /// <param name="span">A span that belongs to the trace.</param>
        /// <param name="samplingPriority">The new sampling priority for the trace.</param>
        /// <remarks>
        /// This public method is for SDK users only (aka custom instrumentation).
        /// Internal Datadog calls should use SetTraceSamplingDecision(this ISpan, SamplingPriority, SamplingMechanism).
        /// </remarks>
        public static void SetTraceSamplingPriority(this ISpan span, SamplingPriority samplingPriority)
        {
            SetTraceSamplingDecision(span, (int)samplingPriority, SamplingMechanism.Manual);
        }

        /// <summary>
        /// Sets the sampling priority for the trace that contains the specified <see cref="ISpan"/>.
        /// </summary>
        /// <param name="span">A span that belongs to the trace.</param>
        /// <param name="priority">The new sampling priority for the trace.</param>
        /// <param name="mechanism">The new sampling mechanism for the trace.</param>
        /// <param name="rate">Optional. The sampling rate, if used.</param>
        internal static void SetTraceSamplingDecision(this ISpan span, int priority, int mechanism, float? rate = null)
        {
            if (span == null) { ThrowHelper.ThrowArgumentNullException(nameof(span)); }

            if (span.Context is SpanContext { TraceContext: { } traceContext })
            {
                traceContext.SetSamplingDecision(priority, mechanism, rate);
            }
        }

        internal static void DecorateWebServerSpan(
            this ISpan span,
            string resourceName,
            string method,
            string host,
            string httpUrl,
            string userAgent,
            WebTags tags,
            IEnumerable<KeyValuePair<string, string>> tagsFromHeaders)
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

            foreach (var kvp in tagsFromHeaders)
            {
                span.SetTag(kvp.Key, kvp.Value);
            }
        }

        internal static void SetHeaderTags<T>(this ISpan span, T headers, IReadOnlyDictionary<string, string> headerTags, string defaultTagPrefix)
            where T : IHeadersCollection
        {
            if (headerTags is not null && !headerTags.IsEmpty())
            {
                try
                {
                    var tagsFromHeaders = SpanContextPropagator.Instance.ExtractHeaderTags(headers, headerTags, defaultTagPrefix);
                    foreach (KeyValuePair<string, string> kvp in tagsFromHeaders)
                    {
                        span.SetTag(kvp.Key, kvp.Value);
                    }
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
