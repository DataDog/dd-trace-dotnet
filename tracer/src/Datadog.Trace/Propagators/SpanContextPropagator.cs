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
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
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
        private readonly IContextInjector[] _injectors;
        private readonly IContextExtractor[] _extractors;
        private readonly bool _propagationExtractFirstOnly;

        internal SpanContextPropagator(IEnumerable<IContextInjector>? injectors, IEnumerable<IContextExtractor>? extractors, bool propagationExtractFirsValue)
        {
            _propagationExtractFirstOnly = propagationExtractFirsValue;
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
                    if (_instance is not null)
                    {
                        return _instance;
                    }

                    _instance = new SpanContextPropagator(
                        new IContextInjector[] { DatadogContextPropagator.Instance },
                        new IContextExtractor[]
                        {
                            DistributedContextExtractor.Instance,
                            DatadogContextPropagator.Instance
                        },
                        false);

                    return _instance;
                }
            }

            internal set
            {
                if (value == null!)
                {
                    ThrowHelper.ThrowArgumentNullException(nameof(value));
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
            Inject(context, headers, default(HeadersCollectionGetterAndSetter<TCarrier>));
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
            if (context == null!) { ThrowHelper.ThrowArgumentNullException(nameof(context)); }
            if (carrier == null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }
            if (setter == null!) { ThrowHelper.ThrowArgumentNullException(nameof(setter)); }

            Inject(context, carrier, new ActionSetter<TCarrier>(setter));
        }

        internal void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            if (context == null!) { ThrowHelper.ThrowArgumentNullException(nameof(context)); }
            if (carrier == null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }

            // If appsec standalone is enabled and appsec propagation is disabled (no ASM events) -> stop propagation
            if (context.TraceContext?.Tracer.Settings?.AppsecStandaloneEnabledInternal == true && context.TraceContext.Tags.GetTag(Tags.Propagated.AppSec) != "1")
            {
                return;
            }

            // trigger a sampling decision if it hasn't happened yet
            _ = context.GetOrMakeSamplingDecision();

            for (var i = 0; i < _injectors.Length; i++)
            {
                _injectors[i].Inject(context, carrier, carrierSetter);
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
            return Extract(headers, default(HeadersCollectionGetterAndSetter<TCarrier>));
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
            if (getter == null!) { ThrowHelper.ThrowArgumentNullException(nameof(getter)); }

            return Extract(carrier, new FuncGetter<TCarrier>(getter));
        }

        internal SpanContext? Extract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            if (carrier is null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }

            SpanContext? localSpanContext = null;

            for (var i = 0; i < _extractors.Length; i++)
            {
                if (_extractors[i].TryExtract(carrier, carrierGetter, out var spanContext))
                {
                    if (_propagationExtractFirstOnly)
                    {
                        return spanContext;
                    }

                    if (localSpanContext is not null && spanContext is not null && _extractors[i] is W3CTraceContextPropagator)
                    {
                        if (localSpanContext.RawTraceId == spanContext.RawTraceId)
                        {
                            localSpanContext.AdditionalW3CTraceState += spanContext.AdditionalW3CTraceState;

                            if (localSpanContext.RawSpanId != spanContext.RawSpanId)
                            {
                                if (!string.IsNullOrEmpty(spanContext.LastParentId) && spanContext.LastParentId != W3CTraceContextPropagator.ZeroLastParent)
                                {
                                    localSpanContext.LastParentId = spanContext.LastParentId;
                                }
                                else
                                {
                                    localSpanContext.LastParentId = HexString.ToHexString(localSpanContext.SpanId);
                                }

                                localSpanContext.SpanId = spanContext.SpanId;
                                localSpanContext.RawSpanId = spanContext.RawSpanId;
                            }
                        }
                    }

                    if (localSpanContext is null)
                    {
                        localSpanContext = spanContext;
                    }
                }
            }

            return localSpanContext;
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

            return Extract(serializedSpanContext, default(ReadOnlyDictionaryGetter));
        }

        public void AddHeadersToSpanAsTags<THeaders>(ISpan span, THeaders headers, IEnumerable<KeyValuePair<string, string?>> headerToTagMap, string defaultTagPrefix)
            where THeaders : IHeadersCollection
        {
            var processor = new SpanTagHeaderTagProcessor(span);
            ExtractHeaderTags(ref processor, headers, headerToTagMap, defaultTagPrefix, string.Empty);
        }

        public void AddHeadersToSpanAsTags<THeaders>(ISpan span, THeaders headers, IEnumerable<KeyValuePair<string, string?>> headerToTagMap, string defaultTagPrefix, string useragent)
            where THeaders : IHeadersCollection
        {
            var processor = new SpanTagHeaderTagProcessor(span);
            ExtractHeaderTags(ref processor, headers, headerToTagMap, defaultTagPrefix, useragent);
        }

        internal void ExtractHeaderTags<THeaders, TProcessor>(ref TProcessor processor, THeaders headers, IEnumerable<KeyValuePair<string, string?>> headerToTagMap, string defaultTagPrefix)
            where THeaders : IHeadersCollection
            where TProcessor : struct, IHeaderTagProcessor
        {
            ExtractHeaderTags(ref processor, headers, headerToTagMap, defaultTagPrefix, string.Empty);
        }

        internal void ExtractHeaderTags<THeaders, TProcessor>(ref TProcessor processor, THeaders headers, IEnumerable<KeyValuePair<string, string?>> headerToTagMap, string defaultTagPrefix, string useragent)
            where THeaders : IHeadersCollection
            where TProcessor : struct, IHeaderTagProcessor
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
                    processor.ProcessTag(providedTagName!, headerValue);
                }
                else
                {
                    // Since the header name was saved to do the lookup in the input headers,
                    // convert the header to its final tag name once per prefix
                    var cacheKey = new Key(headerName, defaultTagPrefix);
                    var tagNameResult = _defaultTagMappingCache.GetOrAdd(cacheKey, key =>
                    {
                        if (SpanTagHelper.TryNormalizeTagName(key.HeaderName, normalizeSpaces: true, out var normalizedHeaderTagName))
                        {
                            return key.TagPrefix + "." + normalizedHeaderTagName;
                        }

                        return null;
                    });

                    if (tagNameResult != null)
                    {
                        processor.ProcessTag(tagNameResult, headerValue);
                    }
                }
            }
        }

#pragma warning disable SA1201
        public interface IHeaderTagProcessor
#pragma warning restore SA1201
        {
            void ProcessTag(string key, string? value);
        }

        public readonly struct SpanTagHeaderTagProcessor : IHeaderTagProcessor
        {
            private readonly ISpan _span;

            public SpanTagHeaderTagProcessor(ISpan span)
            {
                _span = span;
            }

            public void ProcessTag(string key, string? value)
            {
                _span.SetTag(key, value);
            }
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

        private readonly struct HeadersCollectionGetterAndSetter<TCarrier> : ICarrierGetter<TCarrier>, ICarrierSetter<TCarrier>
            where TCarrier : IHeadersCollection
        {
            public IEnumerable<string?> Get(TCarrier carrier, string key)
            {
                return carrier.GetValues(key);
            }

            public void Set(TCarrier carrier, string key, string value)
            {
                carrier.Set(key, value);
            }
        }

        private readonly struct FuncGetter<TCarrier> : ICarrierGetter<TCarrier>
        {
            private readonly Func<TCarrier, string, IEnumerable<string?>> _getter;

            public FuncGetter(Func<TCarrier, string, IEnumerable<string?>> getter)
            {
                _getter = getter;
            }

            public IEnumerable<string?> Get(TCarrier carrier, string key)
            {
                return _getter(carrier, key);
            }
        }

        private readonly struct ActionSetter<TCarrier> : ICarrierSetter<TCarrier>
        {
            private readonly Action<TCarrier, string, string> _setter;

            public ActionSetter(Action<TCarrier, string, string> setter)
            {
                _setter = setter;
            }

            public void Set(TCarrier carrier, string key, string value)
            {
                _setter(carrier, key, value);
            }
        }

        private readonly struct ReadOnlyDictionaryGetter : ICarrierGetter<IReadOnlyDictionary<string, string?>?>
        {
            public IEnumerable<string?> Get(IReadOnlyDictionary<string, string?>? carrier, string key)
            {
                if (carrier != null && carrier.TryGetValue(key, out var value))
                {
                    return new[] { value };
                }

                return Enumerable.Empty<string?>();
            }
        }
    }
}
