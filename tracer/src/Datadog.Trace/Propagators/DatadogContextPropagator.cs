// <copyright file="DatadogContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;

namespace Datadog.Trace.Propagators
{
    internal class DatadogContextPropagator : IContextInjector, IContextExtractor
    {
        public static readonly DatadogContextPropagator Instance = new();

        public void Inject<TCarrier, TCarrierSetter>(
            IPropagatedSpanContext context,
            TCarrier carrier,
            TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            var invariantCulture = CultureInfo.InvariantCulture;

            carrierSetter.Set(carrier, HttpHeaderNames.TraceId, context.TraceId.ToString(invariantCulture));
            carrierSetter.Set(carrier, HttpHeaderNames.ParentId, context.SpanId.ToString(invariantCulture));

            if (!string.IsNullOrWhiteSpace(context.Origin))
            {
                carrierSetter.Set(carrier, HttpHeaderNames.Origin, context.Origin!);
            }

            var samplingPriority = context.SamplingPriority switch
                                   {
                                       -1 => "-1",
                                       0 => "0",
                                       1 => "1",
                                       2 => "2",
                                       var priority => priority?.ToString(invariantCulture)
                                   };

            if (samplingPriority != null)
            {
                carrierSetter.Set(carrier, HttpHeaderNames.SamplingPriority, samplingPriority);
            }

            var propagatedTags = context is SpanContext { TraceContext.Tags: { } tags } ?
                                     tags.ToPropagationHeader() :
                                     context.PropagatedTags;

            if (!string.IsNullOrWhiteSpace(propagatedTags))
            {
                carrierSetter.Set(carrier, HttpHeaderNames.PropagatedTags, propagatedTags!);
            }
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(
            TCarrier carrier,
            TCarrierGetter carrierGetter,
            out IPropagatedSpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            var traceId = ParseUtility.ParseUInt64(carrier, carrierGetter, HttpHeaderNames.TraceId);
            if (traceId is null or 0)
            {
                // a valid traceId is required to use distributed tracing
                return false;
            }

            var parentId = ParseUtility.ParseUInt64(carrier, carrierGetter, HttpHeaderNames.ParentId) ?? 0;
            var samplingPriority = ParseUtility.ParseInt32(carrier, carrierGetter, HttpHeaderNames.SamplingPriority);
            var origin = ParseUtility.ParseString(carrier, carrierGetter, HttpHeaderNames.Origin);
            var propagatedTags = ParseUtility.ParseString(carrier, carrierGetter, HttpHeaderNames.PropagatedTags);

            spanContext = new PropagatedSpanContext(
                traceId.Value,
                parentId,
                rawTraceId: null,
                rawSpanId: null,
                samplingPriority,
                origin: origin,
                propagatedTags);

            return true;
        }
    }
}
