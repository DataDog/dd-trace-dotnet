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
    internal unsafe class SpanContextPropagator
    {
        internal const string HttpRequestHeadersTagPrefix = "http.request.headers";
        internal const string HttpResponseHeadersTagPrefix = "http.response.headers";

        private const NumberStyles NumberStyles = System.Globalization.NumberStyles.Integer;

        private readonly CultureInfo _invariantCulture = CultureInfo.InvariantCulture;
        private readonly IDatadogLogger _log = DatadogLogging.GetLoggerFor<SpanContextPropagator>();
        private readonly ConcurrentDictionary<Key, string?> _defaultTagMappingCache = new();

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
            Inject(context, headers, &Setter);

            static void Setter(TCarrier headers, string name, string? value)
                => headers.Set(name, value);
        }

        /// <summary>
        /// Propagates the specified context by adding new headers to a <see cref="IHeadersCollection"/>.
        /// This locks the sampling priority for <paramref name="context"/>.
        /// </summary>
        /// <param name="context">A <see cref="SpanContext"/> value that will be propagated into <paramref name="carrier"/>.</param>
        /// <param name="carrier">The headers to add to.</param>
        /// <param name="setter">The action that can set a header in the carrier.</param>
        /// <typeparam name="TCarrier">Type of header collection</typeparam>
        public void Inject<TCarrier>(SpanContext context, TCarrier carrier, delegate*<TCarrier, string, string, void> setter)
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

            var datadogTags = context.TraceContext?.DatadogTags ?? context.DatadogTags;

            if (datadogTags != null)
            {
                setter(carrier, HttpHeaderNames.DatadogTags, datadogTags);
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
            return Extract(headers, &Getter);

            static StringEnumerable Getter(TCarrier headers, string name)
                => headers.GetValues(name);
        }

        /// <summary>
        /// Extracts a <see cref="SpanContext"/> from the values found in the specified headers.
        /// </summary>
        /// <param name="carrier">The headers that contain the values to be extracted.</param>
        /// <param name="getter">The function that can extract a list of values for a given header name.</param>
        /// <typeparam name="TCarrier">Type of header collection</typeparam>
        /// <returns>A new <see cref="SpanContext"/> that contains the values obtained from <paramref name="carrier"/>.</returns>
        public SpanContext? Extract<TCarrier>(TCarrier carrier, delegate*<TCarrier, string, StringEnumerable> getter)
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
            var origin = FirstNotNullOrEmpty(getter(carrier, HttpHeaderNames.Origin));
            var datadogTags = FirstNotNullOrEmpty(getter(carrier, HttpHeaderNames.DatadogTags));

            return new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin) { DatadogTags = datadogTags };
        }

        public IEnumerable<KeyValuePair<string, string?>> ExtractHeaderTags<T>(T headers, IEnumerable<KeyValuePair<string, string?>> headerToTagMap, string defaultTagPrefix)
            where T : IHeadersCollection
        {
            return ExtractHeaderTags(headers, headerToTagMap, defaultTagPrefix, string.Empty);
        }

        public IEnumerable<KeyValuePair<string, string?>> ExtractHeaderTags<T>(T headers, IEnumerable<KeyValuePair<string, string?>> headerToTagMap, string defaultTagPrefix, string useragent)
            where T : IHeadersCollection
        {
            foreach (var headerNameToTagName in headerToTagMap)
            {
                var headerName = headerNameToTagName.Key;
                var providedTagName = headerNameToTagName.Value;

                string? headerValue;
                if (string.Equals(headerName, HttpHeaderNames.UserAgent, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(useragent))
                {
                    // A specific case for the user agent as it is split in .net framework web api.
                    headerValue = useragent;
                }
                else
                {
                    headerValue = FirstNotNullOrEmpty(headers.GetValues(headerName));
                }

                if (headerValue is null)
                {
                    continue;
                }

                // Tag name is normalized during Tracer instantiation so use as-is
                if (!string.IsNullOrWhiteSpace(providedTagName))
                {
                    yield return new KeyValuePair<string, string?>(providedTagName!, headerValue);
                }
                else
                {
                    // Since the header name was saved to do the lookup in the input headers,
                    // convert the header to its final tag name once per prefix
                    var cacheKey = new Key(headerName, defaultTagPrefix);
                    var tagNameResult = _defaultTagMappingCache.GetOrAdd(
                        cacheKey,
                        key =>
                        {
                            if (key.HeaderName.TryConvertToNormalizedTagName(normalizePeriods: true, out var normalizedHeaderTagName))
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

            return Extract(serializedSpanContext, &ReadOnlyDictionaryValueGetter);

            static StringEnumerable ReadOnlyDictionaryValueGetter(IReadOnlyDictionary<string, string?>? carrier, string name)
                => carrier != null && carrier.TryGetValue(name, out var value) ? new StringEnumerable(value) : StringEnumerable.Empty;
        }

        private ulong? ParseUInt64<TCarrier>(TCarrier carrier, delegate*<TCarrier, string, StringEnumerable> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);
            var hasValue = false;

            foreach (var headerValue in headerValues)
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

        private int? ParseInt32<TCarrier>(TCarrier carrier, delegate*<TCarrier, string, StringEnumerable> getter, string headerName)
        {
            var headerValues = getter(carrier, headerName);
            var hasValue = false;

            foreach (var headerValue in headerValues)
            {
                if (int.TryParse(headerValue, out var result))
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

        private string? FirstNotNullOrEmpty(StringEnumerable values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
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
    }
}
