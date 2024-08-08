// <copyright file="DatadogContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using Datadog.Trace.Internal;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal class DatadogContextPropagator : IContextInjector, IContextExtractor
    {
        public static readonly DatadogContextPropagator Instance = new();

        private DatadogContextPropagator()
        {
        }

        public void Inject<TCarrier, TCarrierSetter>(InternalSpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            TelemetryFactory.Metrics.RecordCountContextHeaderStyleInjected(MetricTags.ContextHeaderStyle.Datadog);
            var invariantCulture = CultureInfo.InvariantCulture;

            // x-datadog-trace-id only supports 64-bit trace ids, truncate by using TraceId128.Lower
            carrierSetter.Set(carrier, InternalHttpHeaderNames.TraceId, context.TraceId128.Lower.ToString(invariantCulture));
            carrierSetter.Set(carrier, InternalHttpHeaderNames.ParentId, context.SpanId.ToString(invariantCulture));

            if (!string.IsNullOrEmpty(context.Origin))
            {
                carrierSetter.Set(carrier, InternalHttpHeaderNames.Origin, context.Origin);
            }

            if (context.GetOrMakeSamplingDecision() is { } samplingPriority)
            {
                var samplingPriorityString = InternalSamplingPriorityValues.ToString(samplingPriority);
                carrierSetter.Set(carrier, InternalHttpHeaderNames.SamplingPriority, samplingPriorityString);
            }

            var propagatedTagsHeader = context.PrepareTagsHeaderForPropagation();
            if (!string.IsNullOrEmpty(propagatedTagsHeader))
            {
                carrierSetter.Set(carrier, InternalHttpHeaderNames.PropagatedTags, propagatedTagsHeader!);
            }
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out InternalSpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            var traceIdLower = ParseUtility.ParseUInt64(carrier, carrierGetter, InternalHttpHeaderNames.TraceId);

            if (traceIdLower is null or 0)
            {
                // a valid traceId is required to use distributed tracing
                return false;
            }

            var parentId = ParseUtility.ParseUInt64(carrier, carrierGetter, InternalHttpHeaderNames.ParentId) ?? 0;
            var samplingPriority = ParseUtility.ParseInt32(carrier, carrierGetter, InternalHttpHeaderNames.SamplingPriority);
            var origin = ParseUtility.ParseString(carrier, carrierGetter, InternalHttpHeaderNames.Origin);
            var propagatedTraceTags = ParseUtility.ParseString(carrier, carrierGetter, InternalHttpHeaderNames.PropagatedTags);

            var traceTags = TagPropagation.ParseHeader(propagatedTraceTags);

            // reconstruct 128-bit trace id from the lower 64 bits in "x-datadog-traceid"
            // and the upper 64 bits in "_dd.p.tid"
            var traceId = GetFullTraceId((ulong)traceIdLower, traceTags);

            spanContext = new InternalSpanContext(traceId, parentId, samplingPriority, serviceName: null, origin, isRemote: true)
                          {
                              PropagatedTags = traceTags,
                          };

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
