// <copyright file="DatadogContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal sealed class DatadogContextPropagator : IContextInjector, IContextExtractor
    {
        public static readonly DatadogContextPropagator Instance = new();

        private DatadogContextPropagator()
        {
        }

        public PropagatorType PropagatorType => PropagatorType.TraceContext;

        public string DisplayName => "datadog";

        public void Inject<TCarrier, TCarrierSetter>(PropagationContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            if (context.SpanContext is not { } spanContext)
            {
                // nothing to inject
                return;
            }

            TelemetryFactory.Metrics.RecordCountContextHeaderStyleInjected(MetricTags.ContextHeaderStyle.Datadog);
            var invariantCulture = CultureInfo.InvariantCulture;

            // x-datadog-trace-id only supports 64-bit trace ids, truncate by using TraceId128.Lower
            carrierSetter.Set(carrier, HttpHeaderNames.TraceId, spanContext.TraceId128.Lower.ToString(invariantCulture));
            carrierSetter.Set(carrier, HttpHeaderNames.ParentId, spanContext.SpanId.ToString(invariantCulture));

            if (!string.IsNullOrEmpty(spanContext.Origin))
            {
                carrierSetter.Set(carrier, HttpHeaderNames.Origin, spanContext.Origin!);
            }

            if (spanContext.GetOrMakeSamplingDecision() is { } samplingPriority)
            {
                var samplingPriorityString = SamplingPriorityValues.ToString(samplingPriority);
                carrierSetter.Set(carrier, HttpHeaderNames.SamplingPriority, samplingPriorityString);
            }

            var propagatedTagsHeader = spanContext.PrepareTagsHeaderForPropagation();
            if (!string.IsNullOrEmpty(propagatedTagsHeader))
            {
                carrierSetter.Set(carrier, HttpHeaderNames.PropagatedTags, propagatedTagsHeader!);
            }
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out PropagationContext context)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            context = default;

            var traceIdLower = ParseUtility.ParseUInt64(carrier, carrierGetter, HttpHeaderNames.TraceId);

            if (traceIdLower is null or 0)
            {
                // a valid traceId is required to use distributed tracing
                return false;
            }

            var parentId = ParseUtility.ParseUInt64(carrier, carrierGetter, HttpHeaderNames.ParentId) ?? 0;
            var samplingPriority = ParseUtility.ParseInt32(carrier, carrierGetter, HttpHeaderNames.SamplingPriority);
            var origin = ParseUtility.ParseString(carrier, carrierGetter, HttpHeaderNames.Origin);
            var propagatedTraceTags = ParseUtility.ParseString(carrier, carrierGetter, HttpHeaderNames.PropagatedTags);

            var traceTags = TagPropagation.ParseHeader(propagatedTraceTags);

            // reconstruct 128-bit trace id from the lower 64 bits in "x-datadog-traceid"
            // and the upper 64 bits in "_dd.p.tid"
            var traceId = GetFullTraceId((ulong)traceIdLower, traceTags);

            var spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin, isRemote: true)
                              {
                                  PropagatedTags = traceTags,
                              };

            context = new PropagationContext(spanContext, baggage: null);

            TelemetryFactory.Metrics.RecordCountContextHeaderStyleExtracted(MetricTags.ContextHeaderStyle.Datadog);
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
