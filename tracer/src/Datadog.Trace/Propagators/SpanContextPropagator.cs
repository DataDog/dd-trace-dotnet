// <copyright file="SpanContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal class SpanContextPropagator
    {
        internal const string HttpRequestHeadersTagPrefix = "http.request.headers";
        internal const string HttpResponseHeadersTagPrefix = "http.response.headers";

        private static readonly object GlobalLock = new();
        private static SpanContextPropagator? _instance;

        private readonly ConcurrentDictionary<Key, string?> _defaultTagMappingCache = new();
        private readonly Func<IReadOnlyDictionary<string, string?>?, string, IEnumerable<string?>> _readOnlyDictionaryValueGetterDelegate;
        private readonly IContextInjector[] _injectors;
        private readonly IContextExtractor[] _extractors;

        internal SpanContextPropagator(IEnumerable<IContextInjector>? injectors, IEnumerable<IContextExtractor>? extractors)
        {
            _readOnlyDictionaryValueGetterDelegate = static (carrier, name) => carrier != null && carrier.TryGetValue(name, out var value) ? new[] { value } : Enumerable.Empty<string?>();
            _injectors = injectors?.ToArray() ?? Array.Empty<IContextInjector>();
            _extractors = extractors?.ToArray() ?? Array.Empty<IContextExtractor>();
        }

        public static SpanContextPropagator Instance
        {
            get
            {
                if (_instance is not null)
                {
                    return _instance;
                }

                lock (GlobalLock)
                {
                    var distributedContextPropagator = (IContextExtractor)new DistributedContextExtractor();
                    var datadogPropagator = new DatadogContextPropagator();
                    _instance ??= new SpanContextPropagator(new[] { datadogPropagator }, new[] { distributedContextPropagator, datadogPropagator });
                    return _instance;
                }
            }

            internal set
            {
                if (value is null)
                {
                    ThrowHelper.ThrowArgumentNullException("value");
                }

                lock (GlobalLock)
                {
                    _instance = value;
                }
            }
        }

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
            if (context is null) { ThrowHelper.ThrowArgumentNullException(nameof(context)); }
            if (carrier is null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }
            if (setter is null) { ThrowHelper.ThrowArgumentNullException(nameof(setter)); }

            for (var i = 0; i < _injectors.Length; i++)
            {
                _injectors[i].Inject(context, carrier, setter);
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
            if (carrier is null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }
            if (getter is null) { ThrowHelper.ThrowArgumentNullException(nameof(getter)); }

            for (var i = 0; i < _extractors.Length; i++)
            {
                if (_extractors[i].TryExtract(carrier, getter, out var spanContext))
                {
                    return spanContext;
                }
            }

            return null;
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

            return Extract(serializedSpanContext, _readOnlyDictionaryValueGetterDelegate);
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
                    // A specific case for the user agent as it is splitted in .net framework web api.
                    headerValue = useragent;
                }
                else
                {
                    headerValue = ParseUtility.ParseString(headers, headerName);
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
                    var tagNameResult = _defaultTagMappingCache.GetOrAdd(cacheKey, key =>
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

        private record struct Key(string HeaderName, string TagPrefix);

        private static class DelegateCache<THeaders>
            where THeaders : IHeadersCollection
        {
            public static readonly Func<THeaders, string, IEnumerable<string?>> Getter = static (headers, name) => headers.GetValues(name);
            public static readonly Action<THeaders, string, string?> Setter = static (headers, name, value) => headers.Set(name, value);
        }
    }
}
