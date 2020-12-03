using System;
using System.Collections.Generic;
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
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<SpanContextPropagator>();

        private static readonly int[] SamplingPriorities;

        static SpanContextPropagator()
        {
            SamplingPriorities = Enum.GetValues(typeof(SamplingPriority)).Cast<int>().ToArray();
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
        /// <typeparam name="T">Type of header collection</typeparam>
        public void Inject<T>(SpanContext context, T headers)
            where T : IHeadersCollection
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }

            if (headers == null) { throw new ArgumentNullException(nameof(headers)); }

            // lock sampling priority when span propagates.
            context.TraceContext?.LockSamplingPriority();

            headers.Set(HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));
            headers.Set(HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));

            // avoid writing origin header if not set, keeping the previous behavior.
            if (context.Origin != null)
            {
                headers.Set(HttpHeaderNames.Origin, context.Origin);
            }

            var samplingPriority = (int?)(context.TraceContext?.SamplingPriority ?? context.SamplingPriority);

            headers.Set(
                HttpHeaderNames.SamplingPriority,
                samplingPriority?.ToString(InvariantCulture));
        }

        /// <summary>
        /// Propagates the specified context by adding new headers to a <see cref="IHeadersCollection"/>.
        /// This locks the sampling priority for <paramref name="context"/>.
        /// </summary>
        /// <param name="context">A <see cref="SpanContext"/> value that will be propagated into <paramref name="carrier"/>.</param>
        /// <param name="carrier">The headers to add to.</param>
        /// <param name="setter">The action that can set a header in the carrier.</param>
        /// <typeparam name="T">Type of header collection</typeparam>
        public void Inject<T>(SpanContext context, T carrier, Action<T, string, string> setter)
        {
            if (context == null) { throw new ArgumentNullException(nameof(context)); }

            if (carrier == null) { throw new ArgumentNullException(nameof(carrier)); }

            if (setter == null) { throw new ArgumentNullException(nameof(setter)); }

            // lock sampling priority when span propagates.
            context.TraceContext?.LockSamplingPriority();

            setter(carrier, HttpHeaderNames.TraceId, context.TraceId.ToString(InvariantCulture));
            setter(carrier, HttpHeaderNames.ParentId, context.SpanId.ToString(InvariantCulture));

            // avoid writing origin header if not set, keeping the previous behavior.
            if (context.Origin != null)
            {
                setter(carrier, HttpHeaderNames.Origin, context.Origin);
            }

            var samplingPriority = (int?)(context.TraceContext?.SamplingPriority ?? context.SamplingPriority);

            setter(carrier, HttpHeaderNames.SamplingPriority, samplingPriority?.ToString(InvariantCulture));
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
            var origin = ParseString(headers, HttpHeaderNames.Origin);

            return new SpanContext(traceId, parentId, samplingPriority, null, origin);
        }

        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the values found in the specified headers.
        /// </summary>
        /// <param name="carrier">The headers that contain the values to be extracted.</param>
        /// <param name="getter">The function that can extract a list of values for a given header name.</param>
        /// <typeparam name="T">Type of header collection</typeparam>
        /// <returns>A new <see cref="SpanContext"/> that contains the values obtained from <paramref name="carrier"/>.</returns>
        public SpanContext Extract<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (carrier == null) { throw new ArgumentNullException(nameof(carrier)); }

            if (getter == null) { throw new ArgumentNullException(nameof(getter)); }

            var traceId = ParseUInt64(carrier, getter, HttpHeaderNames.TraceId);

            if (traceId == 0)
            {
                // a valid traceId is required to use distributed tracing
                return null;
            }

            var parentId = ParseUInt64(carrier, getter, HttpHeaderNames.ParentId);
            var samplingPriority = ParseSamplingPriority(carrier, getter, HttpHeaderNames.SamplingPriority);
            var origin = ParseString(carrier, getter, HttpHeaderNames.Origin);

            return new SpanContext(traceId, parentId, samplingPriority, null, origin);
        }

        public IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags<T>(T headers, IEnumerable<KeyValuePair<string, string>> headerToTagMap)
            where T : IHeadersCollection
        {
            foreach (KeyValuePair<string, string> headerNameToTagName in headerToTagMap)
            {
                string headerValue = ParseString(headers, headerNameToTagName.Key);

                if (headerValue != null)
                {
                    yield return new KeyValuePair<string, string>(headerNameToTagName.Value, headerValue);
                }
            }
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

        private static ulong ParseUInt64<T>(T carrier, Func<T, string, IEnumerable<string>> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);

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
                if (int.TryParse(headerValue, out var result))
                {
                    foreach (var validValue in SamplingPriorities)
                    {
                        if (validValue == result)
                        {
                            return (SamplingPriority)result;
                        }
                    }
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

        private static SamplingPriority? ParseSamplingPriority<T>(T carrier, Func<T, string, IEnumerable<string>> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);

            bool hasValue = false;

            foreach (string headerValue in headerValues)
            {
                if (int.TryParse(headerValue, out var result))
                {
                    foreach (var validValue in SamplingPriorities)
                    {
                        if (validValue == result)
                        {
                            return (SamplingPriority)result;
                        }
                    }
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

        private static string ParseString(IHeadersCollection headers, string headerName)
        {
            var headerValues = headers.GetValues(headerName);

            foreach (string headerValue in headerValues)
            {
                if (!string.IsNullOrEmpty(headerValue))
                {
                    return headerValue;
                }
            }

            return null;
        }

        private static string ParseString<T>(T carrier, Func<T, string, IEnumerable<string>> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);

            foreach (string headerValue in headerValues)
            {
                if (!string.IsNullOrEmpty(headerValue))
                {
                    return headerValue;
                }
            }

            return null;
        }
    }
}
