// <copyright file="DistributedContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Propagators
{
    internal class DistributedContextExtractor : IContextExtractor
    {
        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            if (carrier is not IReadOnlyDictionary<string, string?>)
            {
                return false;
            }

            var traceId = ParseUtility.ParseUInt64(carrier, carrierGetter, SpanContext.Keys.TraceId);
            if (traceId is null or 0)
            {
                // a valid traceId is required to use distributed tracing
                return false;
            }

            var parentId = ParseUtility.ParseUInt64(carrier, carrierGetter, SpanContext.Keys.ParentId) ?? 0;
            var samplingPriority = ParseUtility.ParseInt32(carrier, carrierGetter, SpanContext.Keys.SamplingPriority);
            var origin = ParseUtility.ParseString(carrier, carrierGetter, SpanContext.Keys.Origin);
            var rawTraceId = ParseUtility.ParseString(carrier, carrierGetter, SpanContext.Keys.RawTraceId);
            var rawSpanId = ParseUtility.ParseString(carrier, carrierGetter, SpanContext.Keys.RawSpanId);
            var propagatedTraceTags = ParseUtility.ParseString(carrier, carrierGetter, SpanContext.Keys.PropagatedTags);

            spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin, rawTraceId, rawSpanId)
                          {
                              PropagatedTags = propagatedTraceTags
                          };

            return true;
        }
    }
}
