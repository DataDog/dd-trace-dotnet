// <copyright file="SpanContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace
{
    internal class SpanContextPropagator
    {
        internal const string HttpRequestHeadersTagPrefix = "http.request.headers";
        internal const string HttpResponseHeadersTagPrefix = "http.response.headers";

        private const NumberStyles NumberStyles = System.Globalization.NumberStyles.Integer;

        private readonly CultureInfo _invariantCulture = CultureInfo.InvariantCulture;
        private readonly IDatadogLogger _log = DatadogLogging.GetLoggerFor<SpanContextPropagator>();
        private readonly ConcurrentDictionary<Key, string?> _defaultTagMappingCache = new();

        private SpanContextPropagator()
        {
        }

        public static SpanContextPropagator Instance { get; } = new();

        /// <summary>
        /// Propagates the specified context by adding new headers to a <see cref="IHeadersCollection"/>.
        /// This locks the sampling priority for <paramref name="context"/>.
        /// </summary>
        /// <param name="context">A <see cref="SpanContext"/> value that will be propagated into <paramref name="headers"/>.</param>
        /// <param name="headers">A <see cref="IHeadersCollection"/> to add new headers to.</param>
        /// <typeparam name="TCarrier">Type of header collection</typeparam>
        public void Inject<TCarrier>(SpanContext context, TCarrier headers)
            where TCarrier : IHeadersCollection
        {
            Inject(context, headers, DelegateCache<TCarrier>.Setter);
        }

        /// <summary>
        /// Propagates the specified context by adding new headers to a <see cref="IHeadersCollection"/>.
        /// This locks the sampling priority for <paramref name="context"/>.
        /// </summary>
        /// <param name="context">A <see cref="SpanContext"/> value that will be propagated into <paramref name="carrier"/>.</param>
        /// <param name="carrier">The headers to add to.</param>
        /// <param name="setter">The action that can set a header in the carrier.</param>
        /// <typeparam name="TCarrier">Type of header collection</typeparam>
        public void Inject<TCarrier>(SpanContext context, TCarrier carrier, Action<TCarrier, string, string> setter)
        {
            if (context == null) { ThrowHelper.ThrowArgumentNullException(nameof(context)); }
            if (carrier == null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }
            if (setter == null) { ThrowHelper.ThrowArgumentNullException(nameof(setter)); }

            setter(carrier, HttpHeaderNames.TraceId, context.TraceId.ToString(_invariantCulture));
            setter(carrier, HttpHeaderNames.ParentId, context.SpanId.ToString(_invariantCulture));

            if (context.Origin != null)
            {
                setter(carrier, HttpHeaderNames.Origin, context.Origin);
            }

            var samplingPriority = (int?)(context.TraceContext?.SamplingPriority ?? context.SamplingPriority);

            if (samplingPriority != null)
            {
                setter(carrier, HttpHeaderNames.SamplingPriority, samplingPriority.Value.ToString(_invariantCulture));
            }
        }

        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the values found in the specified headers.
        /// </summary>
        /// <param name="headers">The headers that contain the values to be extracted.</param>
        /// <typeparam name="TCarrier">Type of header collection</typeparam>
        /// <returns>A new <see cref="SpanContext"/> that contains the values obtained from <paramref name="headers"/>.</returns>
        public SpanContext? Extract<TCarrier>(TCarrier headers)
            where TCarrier : IHeadersCollection
        {
            return Extract(headers, DelegateCache<TCarrier>.Getter);
        }

        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the values found in the specified headers.
        /// </summary>
        /// <param name="carrier">The headers that contain the values to be extracted.</param>
        /// <param name="getter">The function that can extract a list of values for a given header name.</param>
        /// <typeparam name="TCarrier">Type of header collection</typeparam>
        /// <returns>A new <see cref="SpanContext"/> that contains the values obtained from <paramref name="carrier"/>.</returns>
        public SpanContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter)
        {
            if (carrier == null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }
            if (getter == null) { ThrowHelper.ThrowArgumentNullException(nameof(getter)); }

            var traceId = ParseUInt64(carrier, getter, HttpHeaderNames.TraceId);

            if (traceId is null or 0)
            {
                // a valid traceId is required to use distributed tracing
                return null;
            }

            var parentId = ParseUInt64(carrier, getter, HttpHeaderNames.ParentId) ?? 0;
            var samplingPriority = (SamplingPriority?)ParseInt32(carrier, getter, HttpHeaderNames.SamplingPriority);
            var origin = ParseString(carrier, getter, HttpHeaderNames.Origin);

            return new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin);
        }

        public IEnumerable<KeyValuePair<string, string?>> ExtractHeaderTags<T>(T headers, IEnumerable<KeyValuePair<string, string?>> headerToTagMap, string defaultTagPrefix)
            where T : IHeadersCollection
        {
            return ExtractHeaderTags(headers, headerToTagMap, defaultTagPrefix, string.Empty);
        }

        public IEnumerable<KeyValuePair<string, string?>> ExtractHeaderTags<T>(T headers, IEnumerable<KeyValuePair<string, string?>> headerToTagMap, string defaultTagPrefix, string useragent)
            where T : IHeadersCollection
        {
            foreach (KeyValuePair<string, string?> headerNameToTagName in headerToTagMap)
            {
                string? headerValue;
                if (string.Equals(headerNameToTagName.Key, HttpHeaderNames.UserAgent, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(useragent))
                {
                    // A specific case for the user agent as it is splitted in .net framework web api.
                    headerValue = useragent;
                }
                else
                {
                    headerValue = ParseString(headers, headerNameToTagName.Key);
                }

                if (headerValue is null)
                {
                    continue;
                }

                // Tag name is normalized during Tracer instantiation so use as-is
                if (!string.IsNullOrWhiteSpace(headerNameToTagName.Value))
                {
                    yield return new KeyValuePair<string, string?>(headerNameToTagName.Value!, headerValue);
                }
                else
                {
                    // Since the header name was saved to do the lookup in the input headers,
                    // convert the header to its final tag name once per prefix
                    var cacheKey = new Key(headerNameToTagName.Key, defaultTagPrefix);
                    string? tagNameResult = _defaultTagMappingCache.GetOrAdd(cacheKey, key =>
                    {
                        if (key.HeaderName.TryConvertToNormalizedHeaderTagName(out string normalizedHeaderTagName))
                        {
                            return key.TagPrefix + "." + normalizedHeaderTagName;
                        }

                        return null;
                    });

                    if (tagNameResult != null)
                    {
                        yield return new KeyValuePair<string, string?>(tagNameResult, headerValue);
                    }
                }
            }
        }

        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from its serialized dictionary.
        /// </summary>
        /// <param name="serializedSpanContext">The serialized dictionary.</param>
        /// <returns>A new <see cref="SpanContext"/> that contains the values obtained from the serialized dictionary.</returns>
        internal SpanContext? Extract(IReadOnlyDictionary<string, string?>? serializedSpanContext)
        {
            if (serializedSpanContext == null)
            {
                return null;
            }

            ulong traceId = 0;

            if (serializedSpanContext.TryGetValue(HttpHeaderNames.TraceId, out var rawTraceId))
            {
                ulong.TryParse(rawTraceId, out traceId);
            }

            if (traceId == 0)
            {
                // a valid traceId is required to use distributed tracing
                return null;
            }

            ulong parentId = 0;

            if (serializedSpanContext.TryGetValue(HttpHeaderNames.ParentId, out var rawParentId))
            {
                ulong.TryParse(rawParentId, out parentId);
            }

            SamplingPriority? samplingPriority = null;

            if (serializedSpanContext.TryGetValue(HttpHeaderNames.SamplingPriority, out var rawSamplingPriority))
            {
                if (int.TryParse(rawSamplingPriority, out var intSamplingPriority))
                {
                    samplingPriority = (SamplingPriority)intSamplingPriority;
                }
            }

            serializedSpanContext.TryGetValue(HttpHeaderNames.Origin, out var origin);

            return new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin);
        }

        private ulong? ParseUInt64<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);
            bool hasValue = false;

            foreach (string? headerValue in headerValues)
            {
                if (ulong.TryParse(headerValue, NumberStyles, _invariantCulture, out var result))
                {
                    return result;
                }

                hasValue = true;
            }

            if (hasValue)
            {
                _log.Warning("Could not parse {HeaderName} headers: {HeaderValues}", headerName, string.Join(",", headerValues));
            }

            return null;
        }

        private int? ParseInt32<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);
            bool hasValue = false;

            foreach (string? headerValue in headerValues)
            {
                if (int.TryParse(headerValue, out var result))
                {
                    // note this int value may not be defined in the enum,
                    // but we should pass it along without validation
                    // for forward compatibility
                    return result;
                }

                hasValue = true;
            }

            if (hasValue)
            {
                _log.Warning(
                    "Could not parse {HeaderName} headers: {HeaderValues}",
                    headerName,
                    string.Join(",", headerValues));
            }

            return null;
        }

        private string? ParseString<TCarrier>(TCarrier headers, string headerName)
            where TCarrier : IHeadersCollection
        {
            var headerValues = headers.GetValues(headerName);

            foreach (string? headerValue in headerValues)
            {
                if (!string.IsNullOrEmpty(headerValue))
                {
                    return headerValue;
                }
            }

            return null;
        }

        private string? ParseString<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);

            foreach (string? headerValue in headerValues)
            {
                if (!string.IsNullOrEmpty(headerValue))
                {
                    return headerValue;
                }
            }

            return null;
        }

        private readonly struct Key : IEquatable<Key>
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
            public override bool Equals(object? obj)
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

        private static class DelegateCache<THeaders>
            where THeaders : IHeadersCollection
        {
            public static readonly Func<THeaders, string, IEnumerable<string?>> Getter = (headers, name) => headers.GetValues(name);
            public static readonly Action<THeaders, string, string?> Setter = (headers, name, value) => headers.Set(name, value);
        }
    }
}
