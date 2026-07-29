// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
        public static void SetTraceSamplingPriority(this ISpan span, SamplingPriority samplingPriority)
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
            WebTags tags,
            bool otelSemanticsEnabled = false)
        {
            span.Type = SpanTypes.Web;
            span.ResourceName = resourceName?.Trim();

            if (tags is null)
            {
                return;
            }

            // These properties are declared with both a Datadog and an OpenTelemetry tag name,
            // so the wire name is chosen at serialization time.
            tags.HttpMethod = method;
            tags.HttpUserAgent = userAgent;

            if (!otelSemanticsEnabled)
            {
                // OpenTelemetry has no equivalent of these attributes: it splits the same information
                // into url.scheme/url.path/url.query and server.address/server.port, which the caller sets.
                tags.HttpRequestHeadersHost = host;
                tags.HttpUrl = httpUrl;
            }
        }

        internal static void SetHeaderTags<T>(this ISpan span, T headers, IReadOnlyDictionary<string, string> headerTags, string defaultTagPrefix)
            where T : IHeadersCollection
        {
            if (headerTags is not null && !headerTags.IsEmpty())
            {
                try
                {
                    Tracer.Instance.TracerManager.SpanContextPropagator.AddHeadersToSpanAsTags(span, headers, headerTags, defaultTagPrefix);
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
                return span.GetHttpStatusCodeString() is not null;
            }
        }

        internal static string GetHttpStatusCodeString(this Span span)
            => span.OpenTelemetrySemanticsEnabled
                   ? span.GetTag(Tags.HttpResponseStatusCode)
                   : span.GetTag(Tags.HttpStatusCode);

        internal static int? GetHttpStatusCode(this Span span)
        {
            if (span.Tags is IHasStatusCode statusCodeTags)
            {
                return statusCodeTags.HttpStatusCode;
            }
            else
            {
                var rawHttpStatusCode = span.GetHttpStatusCodeString();
                if (rawHttpStatusCode == null || !int.TryParse(rawHttpStatusCode, out var httpStatusCode))
                {
                    return null;
                }

                return httpStatusCode;
            }
        }

        internal static void SetHttpStatusCode(this Span span, int statusCode, bool isServer, MutableSettings tracerSettings)
        {
            if (statusCode < 100 || statusCode >= 600)
            {
                // not a valid status code. Likely the default integer value
                return;
            }

            if (span.Tags is IHasStatusCode statusCodeTags)
            {
                statusCodeTags.HttpStatusCode = statusCode;
            }
            else
            {
                var tagName = span.OpenTelemetrySemanticsEnabled ? Tags.HttpResponseStatusCode : Tags.HttpStatusCode;
                span.SetTag(tagName, IntStringCache.ToInvariantString(statusCode));
            }

            // Check the customers http statuses that should be marked as errors
            if (tracerSettings.IsErrorStatusCode(statusCode, isServer))
            {
                span.Error = true;

                if (span.OpenTelemetrySemanticsEnabled)
                {
                    if (string.IsNullOrEmpty(span.GetTag(Tags.ErrorType)))
                    {
                        span.SetTag(Tags.ErrorType, IntStringCache.ToInvariantString(statusCode));
                    }
                }
                else
                {
                    // if an error message already exists (e.g. from a previous exception), don't replace it
                    if (string.IsNullOrEmpty(span.GetTag(Tags.ErrorMsg)))
                    {
                        span.SetTag(Tags.ErrorMsg, $"The HTTP response has status code {IntStringCache.ToInvariantString(statusCode)}.");
                    }
                }
            }
        }
    }
}
