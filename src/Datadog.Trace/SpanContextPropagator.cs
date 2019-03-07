using System;
using System.Globalization;
using System.Linq;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class SpanContextPropagator
    {
        private const NumberStyles NumberStyles = System.Globalization.NumberStyles.Integer;

        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        private static readonly ILog Log = LogProvider.For<SpanContextPropagator>();

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
            // if sampling priority is not set yet, this will determine
            // a value using a Sampler.
            context.TraceContext.LockSamplingPriority();

            headers.Set(HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));
            headers.Set(HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));

            var samplingPriority = (int?)context.TraceContext.SamplingPriority;

            headers.Set(
                HttpHeaderNames.SamplingPriority,
                samplingPriority?.ToString(InvariantCulture));
        }

        /// <summary>
        /// Extracts a propagated <see cref="SpanContext"/> and a <see cref="SamplingPriority"/>
        /// from the values found in the specified headers,
        /// </summary>
        /// <param name="headers">The headers that contain the values to be extracted.</param>
        /// <param name="spanContext">The extracted <see cref="SpanContext"/>.</param>
        /// <param name="samplingPriority">The extracted <see cref="SamplingPriority"/>.</param>
        /// <returns><c>true</c> if values where extracted successfully, otherwise <c>false</c>.</returns>
        public bool Extract(IHeadersCollection headers, out SpanContext spanContext, out SamplingPriority? samplingPriority)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }

            var traceId = ParseUInt64(headers, HttpHeaderNames.TraceId);

            if (traceId == 0)
            {
                // a valid traceId is required to use distributed tracing
                spanContext = null;
                samplingPriority = null;
                return false;
            }

            var parentId = ParseUInt64(headers, HttpHeaderNames.ParentId);

            spanContext = new SpanContext(traceId, parentId);
            samplingPriority = ParseEnum<SamplingPriority>(headers, HttpHeaderNames.SamplingPriority);
            return true;
        }

        private static ulong ParseUInt64(IHeadersCollection headers, string headerName)
        {
            var headerValues = headers.GetValues(headerName).ToList();

            if (headerValues.Count > 0)
            {
                foreach (string headerValue in headerValues)
                {
                    if (ulong.TryParse(headerValue, NumberStyles, InvariantCulture, out var result))
                    {
                        return result;
                    }
                }

                Log.InfoFormat("Could not parse {0} headers: {1}", headerName, string.Join(",", headerValues));
            }

            return 0;
        }

        private static T? ParseEnum<T>(IHeadersCollection headers, string headerName)
            where T : struct
        {
            var headerValues = headers.GetValues(headerName).ToList();

            if (headerValues.Count > 0)
            {
                foreach (string headerValue in headerValues)
                {
                    if (int.TryParse(headerValue, NumberStyles, InvariantCulture, out var result) &&
                        Enum.IsDefined(typeof(T), result))
                    {
                        return (T)Enum.ToObject(typeof(T), result);
                    }
                }

                Log.InfoFormat("Could not parse {0} headers: {1}", headerName, string.Join(",", headerValues));
            }

            return default;
        }
    }
}
