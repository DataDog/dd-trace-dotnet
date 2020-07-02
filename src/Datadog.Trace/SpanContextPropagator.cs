using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class SpanContextPropagator
    {
        private const NumberStyles NumberStyles = System.Globalization.NumberStyles.Integer;

        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<SpanContextPropagator>();

        private static readonly Dictionary<string, SamplingPriority> SamplingPriorities;

        static SpanContextPropagator()
        {
            SamplingPriorities = new Dictionary<string, SamplingPriority>();

            foreach (SamplingPriority value in Enum.GetValues(typeof(SamplingPriority)))
            {
                SamplingPriorities.Add(value.ToString(), value);
            }
        }

        private SpanContextPropagator()
        {
        }

        public static SpanContextPropagator Instance { get; } = new SpanContextPropagator();

        /// <summary>
        /// Propagates the specified context by adding new headers to a <see cref="IHeadersCollection"/>.
        /// This locks the sampling priority for <paramref name="context"/>.
        /// </summary>
        /// <param name="context">A <see cref="SpanContext"/> value that will be propagated into <paramref name="headers"/>.</param>
        /// <param name="headers">A <see cref="IHeadersCollection"/> to add new headers to.</param>
        public void Inject(SpanContext context, IHeadersCollection headers)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }

            if (headers == null) { throw new ArgumentNullException(nameof(headers)); }

            // lock sampling priority when span propagates.
            context.TraceContext?.LockSamplingPriority();

            headers.Set(HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));
            headers.Set(HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));

            var samplingPriority = (int?)(context.TraceContext?.SamplingPriority ?? context.SamplingPriority);

            headers.Set(
                HttpHeaderNames.SamplingPriority,
                samplingPriority?.ToString(InvariantCulture));
        }

        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the values found in the specified headers.
        /// </summary>
        /// <param name="headers">The headers that contain the values to be extracted.</param>
        /// <typeparam name="T">Type of header collection</typeparam>
        /// <returns>A new <see cref="SpanContext"/> that contains the values obtained from <paramref name="headers"/>.</returns>
        public SpanContext Extract<T>(T headers)
            where T : IHeadersCollection
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            var traceId = ParseUInt64(headers, HttpHeaderNames.TraceId);

            if (traceId == 0)
            {
                // a valid traceId is required to use distributed tracing
                return null;
            }

            var parentId = ParseUInt64(headers, HttpHeaderNames.ParentId);
            var samplingPriority = ParseSamplingPriority(headers, HttpHeaderNames.SamplingPriority);

            return new SpanContext(traceId, parentId, samplingPriority);
        }

        private static ulong ParseUInt64<T>(T headers, string headerName)
            where T : IHeadersCollection
        {
            var headerValues = headers.GetValues(headerName);

            bool hasValue = false;

            foreach (string headerValue in headerValues)
            {
                if (ulong.TryParse(headerValue, NumberStyles, InvariantCulture, out var result))
                {
                    return result;
                }

                hasValue = true;
            }

            if (hasValue)
            {
                Log.Information("Could not parse {0} headers: {1}", headerName, string.Join(",", headerValues));
            }

            return 0;
        }

        private static SamplingPriority? ParseSamplingPriority<T>(T headers, string headerName)
            where T : IHeadersCollection
        {
            var headerValues = headers.GetValues(headerName);

            bool hasValue = false;

            foreach (string headerValue in headerValues)
            {
                if (SamplingPriorities.TryGetValue(headerValue, out var result))
                {
                    return result;
                }

                hasValue = true;
            }

            if (hasValue)
            {
                Log.Information(
                    "Could not parse {0} headers: {1}",
                    headerName,
                    string.Join(",", headerValues));
            }

            return default;
        }
    }
}
