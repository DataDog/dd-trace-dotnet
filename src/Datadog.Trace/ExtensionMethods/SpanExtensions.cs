using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for the <see cref="Span"/> class.
    /// </summary>
    public static class SpanExtensions
    {
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

            tags.SpanKind = SpanKinds.Server;
            tags.HttpMethod = method;
            tags.HttpRequestHeadersHost = host;
            tags.HttpUrl = httpUrl;
            tags.Language = TracerConstants.Language;

            foreach (KeyValuePair<string, string> kvp in tagsFromHeaders)
            {
                span.SetTag(kvp.Key, kvp.Value);
            }
        }

        internal static void SetServerStatusCode(this Span span, int statusCode)
        {
            string statusCodeString = HttpTags.ConvertStatusCodeToString(statusCode);
            span.SetTag(Tags.HttpStatusCode, statusCodeString);

            // 5xx codes are server-side errors
            if (statusCode / 100 == 5)
            {
                span.Error = true;

                // if an error message already exists (e.g. from a previous exception), don't replace it
                if (string.IsNullOrEmpty(span.GetTag(Tags.ErrorMsg)))
                {
                    span.SetTag(Tags.ErrorMsg, $"The HTTP response has status code {statusCodeString}.");
                }
            }
        }
    }
}
