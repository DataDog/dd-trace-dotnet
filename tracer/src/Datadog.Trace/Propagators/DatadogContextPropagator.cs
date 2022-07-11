// <copyright file="DatadogContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Globalization;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Propagators
{
    internal class DatadogContextPropagator : IContextInjector, IContextExtractor
    {
        public void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            var invariantCulture = CultureInfo.InvariantCulture;

            carrierSetter.Set(carrier, HttpHeaderNames.TraceId, context.TraceId.ToString(invariantCulture));
            carrierSetter.Set(carrier, HttpHeaderNames.ParentId, context.SpanId.ToString(invariantCulture));

            if (context.Origin != null)
            {
                carrierSetter.Set(carrier, HttpHeaderNames.Origin, context.Origin);
            }

            var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;
            if (samplingPriority != null)
            {
#pragma warning disable SA1118 // Parameter should not span multiple lines
                carrierSetter.Set(
                    carrier,
                    HttpHeaderNames.SamplingPriority,
                    samplingPriority.Value switch
                    {
                        -1 => "-1",
                        0 => "0",
                        1 => "1",
                        2 => "2",
                        _ => samplingPriority.Value.ToString(invariantCulture)
                    });
#pragma warning restore SA1118 // Parameter should not span multiple lines
            }

            var propagationHeaderMaxLength = context.TraceContext?.Tracer.Settings.TagPropagationHeaderMaxLength ?? TagPropagation.OutgoingPropagationHeaderMaxLength;
            var propagatedTraceTags = context.TraceContext?.Tags.ToPropagationHeader(propagationHeaderMaxLength) ?? context.PropagatedTags;

            if (propagatedTraceTags != null)
            {
                carrierSetter.Set(carrier, HttpHeaderNames.PropagatedTags, propagatedTraceTags);
            }
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
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
            var propagatedTraceTags = ParseUtility.ParseString(carrier, carrierGetter, HttpHeaderNames.PropagatedTags);

            spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin)
                          {
                              PropagatedTags = propagatedTraceTags
                          };
            return true;
        }
    }
}
