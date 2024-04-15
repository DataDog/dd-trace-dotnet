// <copyright file="DistributedContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal class DistributedContextExtractor : IContextExtractor
    {
        public static readonly DistributedContextExtractor Instance = new();

        private DistributedContextExtractor()
        {
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            if (carrier is not IReadOnlyDictionary<string, string?>)
            {
                return false;
            }

            var traceIdLower = ParseUtility.ParseUInt64(carrier, carrierGetter, SpanContext.Keys.TraceId);

            if (traceIdLower is null or 0)
            {
                // a valid traceId is required to use distributed tracing
                return false;
            }

            var rawTraceId = ParseUtility.ParseString(carrier, carrierGetter, SpanContext.Keys.RawTraceId);
            TraceId traceId = default;

            if (!string.IsNullOrEmpty(rawTraceId))
            {
                _ = HexString.TryParseTraceId(rawTraceId!, out traceId);
            }

            var parentId = ParseUtility.ParseUInt64(carrier, carrierGetter, SpanContext.Keys.ParentId) ?? 0;
            var samplingPriority = ParseUtility.ParseInt32(carrier, carrierGetter, SpanContext.Keys.SamplingPriority);
            var origin = ParseUtility.ParseString(carrier, carrierGetter, SpanContext.Keys.Origin);
            var rawSpanId = ParseUtility.ParseString(carrier, carrierGetter, SpanContext.Keys.RawSpanId);
            var propagatedTraceTags = ParseUtility.ParseString(carrier, carrierGetter, SpanContext.Keys.PropagatedTags);
            var w3CTraceState = ParseUtility.ParseString(carrier, carrierGetter, SpanContext.Keys.AdditionalW3CTraceState);
            var traceTags = TagPropagation.ParseHeader(propagatedTraceTags);

            if (traceId == TraceId.Zero)
            {
                traceId = GetFullTraceId((ulong)traceIdLower, traceTags);
            }

            // we don't consider contexts coming from this as "remote" as it could be from a version conflict scenario
            spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin, rawTraceId, rawSpanId, isRemote: false)
                          {
                              PropagatedTags = traceTags,
                              AdditionalW3CTraceState = w3CTraceState,
                          };

            return true;
        }

        // combine the lower 64 bits from "x-datadog-trace-id" with the
        // upper 64 bits from "_dd.p.tid" into a 128-bit trace id
        private static TraceId GetFullTraceId(ulong lower, TraceTagCollection tags)
        {
            var upperHex = tags.GetTag(Tags.Propagated.TraceIdUpper);

            if (!string.IsNullOrEmpty(upperHex) && HexString.TryParseUInt64(upperHex!, out var upper))
            {
                return new TraceId(upper, lower);
            }

            return (TraceId)lower;
        }
    }
}
