// <copyright file="SpanContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal sealed class SpanContextPropagator
    {
        internal const string HttpRequestHeadersTagPrefix = "http.request.headers";
        internal const string HttpResponseHeadersTagPrefix = "http.response.headers";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanContextPropagator>();

        private readonly ConcurrentDictionary<Key, string?> _defaultTagMappingCache = new();
        private readonly IContextInjector[] _injectors;
        private readonly IContextExtractor[] _extractors;
        private readonly bool _propagationExtractFirstOnly;
        private readonly ExtractBehavior _extractBehavior;

        internal SpanContextPropagator(
            IEnumerable<IContextInjector>? injectors,
            IEnumerable<IContextExtractor>? extractors,
            bool propagationExtractFirstValue,
            ExtractBehavior extractBehavior = default)
        {
            _propagationExtractFirstOnly = propagationExtractFirstValue;
            _extractBehavior = extractBehavior;
            _injectors = injectors?.ToArray() ?? [];
            _extractors = extractors?.ToArray() ?? [];
        }

        /// <summary>
        /// Propagates the specified context by adding new headers to a <see cref="IHeadersCollection"/>.
        /// This locks the sampling priority for <paramref name="context"/>.
        /// </summary>
        /// <param name="context">A <see cref="PropagationContext"/> that will be propagated into <paramref name="headers"/>.</param>
        /// <param name="headers">A <see cref="IHeadersCollection"/> to add new headers to.</param>
        /// <typeparam name="TCarrier">Type of header collection</typeparam>
        public void Inject<TCarrier>(PropagationContext context, TCarrier headers)
            where TCarrier : IHeadersCollection
        {
            Inject(context, headers, headers.GetAccessor());
        }

        /// <summary>
        /// Propagates the specified context by adding new headers to a <paramref name="carrier"/>.
        /// </summary>
        /// <param name="context">A <see cref="PropagationContext"/> with values that will be propagated into <paramref name="carrier"/>.</param>
        /// <param name="carrier">The headers to add to.</param>
        /// <param name="setter">The action that can set a header in the carrier.</param>
        /// <typeparam name="TCarrier">Type of header collection.</typeparam>
        public void Inject<TCarrier>(PropagationContext context, TCarrier carrier, Action<TCarrier, string, string> setter)
        {
            if (carrier == null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }
            if (setter == null!) { ThrowHelper.ThrowArgumentNullException(nameof(setter)); }

            Inject(context, carrier, new ActionSetter<TCarrier>(setter));
        }

        internal void Inject<TCarrier, TCarrierSetter>(PropagationContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            if (carrier == null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }

            if (context.SpanContext is null && context.Baggage is null)
            {
                // nothing to inject
                return;
            }

            if (context.SpanContext is { } spanContext)
            {
                // If apm tracing is disabled and no other product owns the trace -> stop propagation
                if (spanContext.TraceContext is { Tracer.Settings.ApmTracingEnabled: false } trace &&
                    !trace.Tags.HasTraceSources())
                {
                    return;
                }

                // trigger a sampling decision if it hasn't happened yet
                _ = spanContext.GetOrMakeSamplingDecision();
            }

            foreach (var injector in _injectors)
            {
                injector.Inject(context, carrier, carrierSetter);
            }
        }

        /// <summary>
        /// Extracts a <see cref="PropagationContext"/> from the values found in the specified headers.
        /// </summary>
        /// <param name="headers">The headers that contain the values to be extracted.</param>
        /// <typeparam name="TCarrier">Type of header collection</typeparam>
        /// <returns>A new <see cref="PropagationContext"/> that contains the values obtained from <paramref name="headers"/>.</returns>
        public PropagationContext Extract<TCarrier>(TCarrier headers)
            where TCarrier : IHeadersCollection
        {
            return Extract(headers, headers.GetAccessor());
        }

        /// <summary>
        /// Extracts a <see cref="PropagationContext"/> from the values found in the specified headers.
        /// </summary>
        /// <param name="carrier">The headers that contain the values to be extracted.</param>
        /// <param name="getter">The function that can extract a list of values for a given header name.</param>
        /// <typeparam name="TCarrier">Type of header collection</typeparam>
        /// <returns>A new <see cref="PropagationContext"/> that contains the values obtained from <paramref name="carrier"/>.</returns>
        public PropagationContext Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter)
        {
            if (carrier == null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }
            if (getter == null!) { ThrowHelper.ThrowArgumentNullException(nameof(getter)); }

            return Extract(carrier, new FuncGetter<TCarrier>(getter));
        }

        internal PropagationContext Extract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            if (carrier is null) { ThrowHelper.ThrowArgumentNullException(nameof(carrier)); }

            if (_extractBehavior == ExtractBehavior.Ignore)
            {
                return new PropagationContext(spanContext: null, baggage: null);
            }

            // as we extract values from the carrier using multiple extractors,
            // we will accumulate them in this context
            SpanContext? cumulativeSpanContext = null;
            Baggage? cumulativeBaggage = null;
            List<SpanLink> spanLinks = new();
            string? initialExtractorDisplayName = null;

            foreach (var extractor in _extractors)
            {
                if (_propagationExtractFirstOnly &&
                    cumulativeSpanContext is not null &&
                    extractor.PropagatorType == PropagatorType.TraceContext)
                {
                    // skip this extractor if
                    // - we are configured to extract only the first trace context (aka SpanContext)
                    // - we already extracted trace context
                    // - this extractor is a trace context extractor
                    continue;
                }

                if (!extractor.TryExtract(carrier, carrierGetter, out var currentExtractedContext))
                {
                    // extractor failed to extract
                    continue;
                }

                // handle extracted baggage (PropagationContext.Baggage)
                if (currentExtractedContext.Baggage?.Count > 0)
                {
                    cumulativeBaggage ??= new Baggage();

                    // in practice, there should only ever be zero or one baggage extractors, but since we're
                    // treating this as a list of generic extractors, we handle the possibility of multiple
                    // baggage extractors by merging all extracted baggage items into `cumulativeBaggage`
                    currentExtractedContext.Baggage?.MergeInto(cumulativeBaggage);
                }

                // handle extracted trace context (PropagationContext.SpanContext)
                if (cumulativeSpanContext == null)
                {
                    cumulativeSpanContext = currentExtractedContext.SpanContext;
                    initialExtractorDisplayName = extractor.DisplayName;
                }
                else if (currentExtractedContext.SpanContext is { } extractedSpanContext)
                {
                    if (cumulativeSpanContext.RawTraceId != extractedSpanContext.RawTraceId)
                    {
                        spanLinks.Add(new SpanLink(extractedSpanContext, attributes: [new("reason", "terminated_context"), new("context_headers", extractor.DisplayName)]));
                    }
                    else if (extractor is W3CTraceContextPropagator)
                    {
                        MergeExtractedW3CSpanContext(cumulativeSpanContext, extractedSpanContext);
                    }
                }
            }

            return _extractBehavior switch
            {
                ExtractBehavior.Restart when cumulativeSpanContext is not null => new PropagationContext(default, cumulativeBaggage, [new SpanLink(cumulativeSpanContext, attributes: [new("reason", "propagation_behavior_extract"), new("context_headers", initialExtractorDisplayName!)])]),
                ExtractBehavior.Restart => new PropagationContext(default, cumulativeBaggage, []),
                _ => new PropagationContext(cumulativeSpanContext, cumulativeBaggage, spanLinks),

            };
        }

        /// <summary>
        /// Extracts a <see cref="PropagationContext"/> from its serialized dictionary.
        /// </summary>
        /// <param name="serializedSpanContext">The serialized dictionary.</param>
        /// <returns>A new <see cref="PropagationContext"/> that contains the values obtained from the serialized dictionary.</returns>
        internal PropagationContext Extract(IReadOnlyDictionary<string, string?>? serializedSpanContext)
        {
            if (serializedSpanContext is { Count: > 0 })
            {
                return Extract(serializedSpanContext, default(ReadOnlyDictionaryGetter));
            }

            return default;
        }

        private static void MergeExtractedW3CSpanContext(SpanContext cumulativeSpanContext, SpanContext extractedSpanContext)
        {
            if (cumulativeSpanContext.RawTraceId == extractedSpanContext.RawTraceId)
            {
                cumulativeSpanContext.AdditionalW3CTraceState += extractedSpanContext.AdditionalW3CTraceState;

                if (cumulativeSpanContext.RawSpanId != extractedSpanContext.RawSpanId)
                {
                    if (!string.IsNullOrEmpty(extractedSpanContext.LastParentId) && extractedSpanContext.LastParentId != W3CTraceContextPropagator.ZeroLastParent)
                    {
                        cumulativeSpanContext.LastParentId = extractedSpanContext.LastParentId;
                    }
                    else
                    {
                        cumulativeSpanContext.LastParentId = HexString.ToHexString(cumulativeSpanContext.SpanId);
                    }

                    cumulativeSpanContext.SpanId = extractedSpanContext.SpanId;
                    cumulativeSpanContext.RawSpanId = extractedSpanContext.RawSpanId;
                }
            }
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

        internal void AddBaggageToSpanAsTags(Span span, Baggage? baggage, HashSet<string> baggageTagKeys)
        {
            if (baggage is null or { Count: 0 })
            {
                return;
            }

            if (baggageTagKeys.Count == 0)
            {
                // feature disabled
                return;
            }

            try
            {
                var addAllItems = baggageTagKeys.Count == 1 && baggageTagKeys.Contains("*");

                foreach (var item in baggage)
                {
                    if (addAllItems || baggageTagKeys.Contains(item.Key))
                    {
                        span.SetTag("baggage." + item.Key, item.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error adding baggage tags to span.");
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
