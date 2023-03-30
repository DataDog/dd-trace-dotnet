// <copyright file="DatadogContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal class DatadogContextPropagator : IContextInjector, IContextExtractor
    {
        public static readonly DatadogContextPropagator Instance = new();

        private DatadogContextPropagator()
        {
        }

        public void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            var invariantCulture = CultureInfo.InvariantCulture;

            // x-datadog-trace-id only supports 64-bit trace ids, truncate by using TraceId128.Lower
            carrierSetter.Set(carrier, HttpHeaderNames.TraceId, context.TraceId128.Lower.ToString(invariantCulture));
            carrierSetter.Set(carrier, HttpHeaderNames.ParentId, context.SpanId.ToString(invariantCulture));

            if (context.Origin != null)
            {
                carrierSetter.Set(carrier, HttpHeaderNames.Origin, context.Origin);
            }

            var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;

            if (samplingPriority != null)
            {
                var samplingPriorityString = samplingPriority.Value switch
                                             {
                                                 -1 => "-1",
                                                 0 => "0",
                                                 1 => "1",
                                                 2 => "2",
                                                 _ => samplingPriority.Value.ToString(invariantCulture)
                                             };

                carrierSetter.Set(carrier, HttpHeaderNames.SamplingPriority, samplingPriorityString);
            }

            var propagatedTags = GetPropagatedTags(context);

            // we need to call this even if the trace id is 64-bit,
            // because we may need to replace or remove the tag if it's present
            // (e.g. a bug in an upstream tracer)
            propagatedTags?.FixTraceIdTag(context.TraceId128);

            var propagatedTagsHeader = propagatedTags?.ToPropagationHeader();

            if (!string.IsNullOrEmpty(propagatedTagsHeader))
            {
                carrierSetter.Set(carrier, HttpHeaderNames.PropagatedTags, propagatedTagsHeader!);
            }
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

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
            var traceId = GetFullTraceId((ulong)traceIdLower, traceTags);

            spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin)
                          {
                              PropagatedTags = traceTags
                          };

            return true;
        }

        private static TraceTagCollection? GetPropagatedTags(SpanContext context)
        {
            // prioritize the trace context's tags in a local context,
            // the SpanContext's tags are only used in a propagated context
            var propagatedTags = context.TraceContext?.Tags ?? context.PropagatedTags;

            if (propagatedTags != null)
            {
                return propagatedTags;
            }

            if (context.TraceId128.Upper == 0)
            {
                // if the trace id is 64-bit, we won't add any tag so we don't need to initialize a new collection
                return null;
            }

            var maxHeaderLength = Tracer.Instance?.Settings?.OutgoingTagPropagationHeaderMaxLength ??
                                  TagPropagation.OutgoingTagPropagationHeaderMaxLength;

            // if there is no tag collection, it means there was no trace context,
            // so this is a propagated context (or a test), so initialize
            // the span context's tag collection
            context.PropagatedTags = new TraceTagCollection(maxHeaderLength);
            return context.PropagatedTags;
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
