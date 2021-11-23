// <copyright file="ISpanExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for the <see cref="ISpan"/> class.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static class ISpanExtensions
    {
        /// <summary>
        /// Adds standard tags to a span with values taken from the specified <see cref="DbCommand"/>.
        /// </summary>
        /// <param name="span">The span to add the tags to.</param>
        /// <param name="command">The db command to get tags values from.</param>
        public static void AddTagsFromDbCommand(this ISpan span, IDbCommand command)
        {
            span.ResourceName = command.CommandText;
            span.Type = SpanTypes.Sql;

            var tags = DbCommandCache.GetTagsFromDbCommand(command);

            foreach (var pair in tags)
            {
                span.SetTag(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Add the StackTrace and other exception metadata to the span
        /// </summary>
        /// <param name="span">The span to add the exception to.</param>
        /// <param name="exception">The exception.</param>
        public static void SetException(this ISpan span, Exception exception)
        {
            span.Error = true;

            if (exception != null)
            {
                // for AggregateException, use the first inner exception until we can support multiple errors.
                // there will be only one error in most cases, and even if there are more and we lose
                // the other ones, it's still better than the generic "one or more errors occurred" message.
                if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count > 0)
                {
                    exception = aggregateException.InnerExceptions[0];
                }

                span.SetTag(Trace.Tags.ErrorMsg, exception.Message);
                span.SetTag(Trace.Tags.ErrorStack, exception.ToString());
                span.SetTag(Trace.Tags.ErrorType, exception.GetType().ToString());
            }
        }

        internal static void DecorateWebServerSpan(
            this ISpan span,
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

        /// <summary>
        /// Record the end time of the span and flushes it to the backend.
        /// After the span has been finished all modifications will be ignored.
        /// </summary>
        internal static void Finish(this ISpan span, TimeSpan duration)
        {
            if (span is Span internalSpan)
            {
                internalSpan.Finish(duration);
            }
        }

        internal static bool TryGetTimeSinceStart(this ISpan span, DateTime endTime, out TimeSpan result)
        {
            if (span is Span internalSpan)
            {
                result = internalSpan.StartTime.UtcDateTime - endTime;
                return true;
            }
            else
            {
                result = TimeSpan.MaxValue;
                return false;
            }
        }

        /// <summary>
        /// Sets the sampling priority for the trace that contains the specified <see cref="Span"/>.
        /// </summary>
        /// <param name="span">A span that belongs to the trace.</param>
        internal static void LockSamplingPriority(this ISpan span)
        {
            if (span == null) { throw new ArgumentNullException(nameof(span)); }

            if (span is Span internalSpan && internalSpan.InternalContext.TraceContext != null)
            {
                internalSpan.InternalContext.TraceContext.LockSamplingPriority();
            }
        }

        internal static void ResetStartTime(this ISpan span)
        {
            if (span is Span internalSpan)
            {
                internalSpan.ResetStartTime();
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

        internal static void SetHttpStatusCode(this ISpan span, int statusCode, bool isServer, ImmutableTracerSettings tracerSettings)
        {
            string statusCodeString = ConvertStatusCodeToString(statusCode);

            if (span is IHasTags spanWithTags && spanWithTags.Tags is IHasStatusCode statusCodeTags)
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

        internal static ISpan SetMetric(this ISpan span, string key, double? value)
        {
            if (span is Span internalSpan)
            {
                return internalSpan.SetMetric(key, value);
            }
            else if (span is IHasTags spanWithTags)
            {
                spanWithTags.Tags.SetMetric(key, value);
                return span;
            }
            else
            {
                // Noop
                return span;
            }
        }

        /// <summary>
        /// Sets the sampling priority for the trace that contains the specified <see cref="Span"/>.
        /// </summary>
        /// <param name="span">A span that belongs to the trace.</param>
        /// <param name="samplingPriority">The new sampling priority for the trace.</param>
        internal static void SetTraceSamplingPriority(this ISpan span, SamplingPriority samplingPriority)
        {
            if (span == null) { throw new ArgumentNullException(nameof(span)); }

            if (span is Span internalSpan && internalSpan.InternalContext.TraceContext != null)
            {
                internalSpan.InternalContext.TraceContext.SamplingPriority = samplingPriority;
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
