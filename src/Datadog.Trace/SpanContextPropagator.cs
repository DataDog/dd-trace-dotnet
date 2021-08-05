// <copyright file="SpanContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class SpanContextPropagator
    {
        internal static readonly string HttpRequestHeadersTagPrefix = "http.request.headers";
        internal static readonly string HttpResponseHeadersTagPrefix = "http.response.headers";

        private const NumberStyles NumberStyles = System.Globalization.NumberStyles.Integer;
        private const int MinimumSamplingPriority = (int)SamplingPriority.UserReject;
        private const int MaximumSamplingPriority = (int)SamplingPriority.UserKeep;

        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanContextPropagator>();
        private static readonly ConcurrentDictionary<Key, string> DefaultTagMappingCache = new ConcurrentDictionary<Key, string>();

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

            if (samplingPriority != null)
            {
                headers.Set(
                    HttpHeaderNames.SamplingPriority,
                    samplingPriority.Value.ToString(InvariantCulture));
            }
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

            if (samplingPriority != null)
            {
                setter(carrier, HttpHeaderNames.SamplingPriority, samplingPriority?.ToString(InvariantCulture));
            }
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

        [Obsolete("This method is deprecated and will be removed. Use ExtractHeaderTags<T>(T, IEnumerable<KeyValuePair<string, string>>, string) instead. " +
            "Kept for backwards compatability where there is a version mismatch between manual and automatic instrumentation")]
        public IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags<T>(T headers, IEnumerable<KeyValuePair<string, string>> headerToTagMap)
            where T : IHeadersCollection
        {
            foreach (KeyValuePair<string, string> headerNameToTagName in headerToTagMap)
            {
                // Empty tag names were only allowed when the newer API was introduced,
                // so we should never encounter an empty tag name when invoking this API.
                // But just in case we get here, skip the processing of this header:tag mapping
                if (string.IsNullOrWhiteSpace(headerNameToTagName.Value))
                {
                    continue;
                }

                string headerValue = ParseString(headers, headerNameToTagName.Key);

                if (headerValue != null)
                {
                    yield return new KeyValuePair<string, string>(headerNameToTagName.Value, headerValue);
                }
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ExtractHeaderTags<T>(T headers, IEnumerable<KeyValuePair<string, string>> headerToTagMap, string defaultTagPrefix)
            where T : IHeadersCollection
        {
            foreach (KeyValuePair<string, string> headerNameToTagName in headerToTagMap)
            {
                string headerValue = ParseString(headers, headerNameToTagName.Key);
                if (headerValue is null)
                {
                    continue;
                }

                // Tag name is normalized during Tracer instantiation so use as-is
                if (!string.IsNullOrWhiteSpace(headerNameToTagName.Value))
                {
                    yield return new KeyValuePair<string, string>(headerNameToTagName.Value, headerValue);
                }
                else
                {
                    // Since the header name was saved to do the lookup in the input headers,
                    // convert the header to its final tag name once per prefix
                    var cacheKey = new Key(headerNameToTagName.Key, defaultTagPrefix);
                    string tagNameResult = DefaultTagMappingCache.GetOrAdd(cacheKey, key =>
                    {
                        if (key.HeaderName.TryConvertToNormalizedHeaderTagName(out string normalizedHeaderTagName))
                        {
                            return key.TagPrefix + "." + normalizedHeaderTagName;
                        }
                        else
                        {
                            return null;
                        }
                    });

                    if (tagNameResult != null)
                    {
                        yield return new KeyValuePair<string, string>(tagNameResult, headerValue);
                    }
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
                Log.Warning("Could not parse {HeaderName} headers: {HeaderValues}", headerName, string.Join(",", headerValues));
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
                Log.Warning("Could not parse {HeaderName} headers: {HeaderValues}", headerName, string.Join(",", headerValues));
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
                    if (MinimumSamplingPriority <= result && result <= MaximumSamplingPriority)
                    {
                        return (SamplingPriority)result;
                    }
                }

                hasValue = true;
            }

            if (hasValue)
            {
                Log.Warning(
                    "Could not parse {HeaderName} headers: {HeaderValues}",
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
                    if (MinimumSamplingPriority <= result && result <= MaximumSamplingPriority)
                    {
                        return (SamplingPriority)result;
                    }
                }

                hasValue = true;
            }

            if (hasValue)
            {
                Log.Warning(
                    "Could not parse {HeaderName} headers: {HeaderValues}",
                    headerName,
                    string.Join(",", headerValues));
            }

            return default;
        }

        private static string ParseString<T>(T headers, string headerName)
            where T : IHeadersCollection
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

        private struct Key : IEquatable<Key>
        {
            public readonly string HeaderName;
            public readonly string TagPrefix;

            public Key(
                string headerName,
                string tagPrefix)
            {
                HeaderName = headerName;
                TagPrefix = tagPrefix;
            }

            /// <summary>
            /// Gets the struct hashcode
            /// </summary>
            /// <returns>Hashcode</returns>
            public override int GetHashCode()
            {
                unchecked
                {
                    return (HeaderName.GetHashCode() * 397) ^ TagPrefix.GetHashCode();
                }
            }

            /// <summary>
            /// Gets if the struct is equal to other object or struct
            /// </summary>
            /// <param name="obj">Object to compare</param>
            /// <returns>True if both are equals; otherwise, false.</returns>
            public override bool Equals(object obj)
            {
                return obj is Key key &&
                       HeaderName == key.HeaderName &&
                       TagPrefix == key.TagPrefix;
            }

            /// <inheritdoc />
            public bool Equals(Key other)
            {
                return HeaderName == other.HeaderName &&
                       TagPrefix == other.TagPrefix;
            }
        }
    }
}
