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

            var propagatedTags = context.TraceContext?.Tags ?? context.PropagatedTags;

            // if needed, inject the upper 64 bits of the trace id into the propagated tags
            if (context.TraceId128.Upper > 0 && propagatedTags?.GetTag(Tags.Propagated.TraceIdUpper) == null)
            {
                if (propagatedTags == null)
                {
                    // try to get the max header length from:
                    // 1. the tracer associated to this span context
                    // 2. the global tracer
                    // 3. fallback to the default value
                    var settings = context.TraceContext?.Tracer?.Settings ?? Tracer.Instance?.Settings;
                    var maxHeaderLength = settings?.OutgoingTagPropagationHeaderMaxLength ?? TagPropagation.OutgoingTagPropagationHeaderMaxLength;
                    propagatedTags = new TraceTagCollection(maxHeaderLength);
                }

                propagatedTags.SetTag(Tags.Propagated.TraceIdUpper, HexString.ToHexString(context.TraceId128.Upper));
            }

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

            var traceTags = TagPropagation.ParseHeader(propagatedTraceTags, TagPropagation.OutgoingTagPropagationHeaderMaxLength);
            var traceId = GetFullTraceId((ulong)traceIdLower, traceTags);

            spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin)
                          {
                              PropagatedTags = traceTags
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
