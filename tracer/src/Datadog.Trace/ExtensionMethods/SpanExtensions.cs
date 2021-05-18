// <copyright file="SpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for the <see cref="Span"/> class.
    /// </summary>
    public static class SpanExtensions
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanExtensions));

        /// <summary>
        /// Sets the sampling priority for the trace that contains the specified <see cref="Span"/>.
        /// </summary>
        /// <param name="span">A span that belongs to the trace.</param>
        /// <param name="samplingPriority">The new sampling priority for the trace.</param>
        public static void SetTraceSamplingPriority(this Span span, SamplingPriority samplingPriority)
        {
            if (span == null) { throw new ArgumentNullException(nameof(span)); }

            if (span.Context.TraceContext != null)
            {
                span.Context.TraceContext.SamplingPriority = samplingPriority;
            }
        }

        /// <summary>
        /// Adds standard tags to a span with values taken from the specified <see cref="DbCommand"/>.
        /// </summary>
        /// <param name="span">The span to add the tags to.</param>
        /// <param name="command">The db command to get tags values from.</param>
        public static void AddTagsFromDbCommand(this Span span, IDbCommand command)
        {
            span.ResourceName = command.CommandText;
            span.Type = SpanTypes.Sql;

            var tags = DbCommandCache.GetTagsFromDbCommand(command);

            foreach (var pair in tags)
            {
                span.SetTag(pair.Key, pair.Value);
            }
        }

        internal static void DecorateWebServerSpan(
            this Span span,
            string resourceName,
            string method,
            string host,
            string httpUrl,
            WebTags tags,
            IEnumerable<KeyValuePair<string, string>> tagsFromHeaders)
        {
            span.Type = SpanTypes.Web;
            span.ResourceName = resourceName?.Trim();

            tags.HttpMethod = method;
            tags.HttpRequestHeadersHost = host;
            tags.HttpUrl = httpUrl;

            foreach (KeyValuePair<string, string> kvp in tagsFromHeaders)
            {
                span.SetTag(kvp.Key, kvp.Value);
            }
        }

        // Added to avoid a breaking change
        [Obsolete("Use SetHeaderTags<T> instead")]
        internal static void SetHeaderTags(this Span span, IHeadersCollection headers, IDictionary<string, string> headerTags, string defaultTagPrefix)
        {
            SetHeaderTags<IHeadersCollection>(span, headers, headerTags, defaultTagPrefix);
        }

        internal static void SetHeaderTags<T>(this Span span, T headers, IDictionary<string, string> headerTags, string defaultTagPrefix)
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

        internal static void SetHttpStatusCode(this Span span, int statusCode, bool isServer)
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
            if (Tracer.Instance.Settings.IsErrorStatusCode(statusCode, isServer))
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
