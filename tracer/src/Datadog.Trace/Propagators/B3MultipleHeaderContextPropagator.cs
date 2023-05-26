// <copyright file="B3MultipleHeaderContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal class B3MultipleHeaderContextPropagator : IContextInjector, IContextExtractor
    {
        /// <summary>
        /// B3 TraceId header
        /// </summary>
        public const string TraceId = "x-b3-traceid";

        /// <summary>
        /// B3 SpanId header
        /// </summary>
        public const string SpanId = "x-b3-spanid";

        /// <summary>
        /// B3 SpanId header
        /// </summary>
        public const string Sampled = "x-b3-sampled";

        public static readonly B3MultipleHeaderContextPropagator Instance = new();

        private B3MultipleHeaderContextPropagator()
        {
        }

        public void Inject<TCarrier, TCarrierSetter>(ISpanContextInternal context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            CreateHeaders(context, out var traceId, out var spanId, out var sampled);

            carrierSetter.Set(carrier, TraceId, traceId);
            carrierSetter.Set(carrier, SpanId, spanId);
            carrierSetter.Set(carrier, Sampled, sampled);
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out ISpanContextInternal? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            var rawTraceId = ParseUtility.ParseString(carrier, carrierGetter, TraceId)?.Trim();

            if (rawTraceId == null || !HexString.TryParseTraceId(rawTraceId, out var traceId) || traceId == Trace.TraceId.Zero)
            {
                return false;
            }

            var rawSpanId = ParseUtility.ParseString(carrier, carrierGetter, SpanId)?.Trim();

            if (rawSpanId == null || !HexString.TryParseUInt64(rawSpanId, out var parentId))
            {
                return false;
            }

            var samplingPriority = ParseUtility.ParseInt32(carrier, carrierGetter, Sampled);
            spanContext = Span.CreateSpanContext(traceId, parentId, samplingPriority, serviceName: null, null, rawTraceId, rawSpanId);
            return true;
        }

        internal static void CreateHeaders(ISpanContextInternal context, out string traceId, out string spanId, out string sampled)
        {
            var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;
            sampled = samplingPriority > 0 ? "1" : "0";

            traceId = context.RawTraceId;
            spanId = context.RawSpanId;
        }
    }
}
