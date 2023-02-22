// <copyright file="B3MultipleHeaderContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
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

        public void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;
            var sampled = samplingPriority > 0 ? "1" : "0";

            carrierSetter.Set(carrier, TraceId, context.RawTraceId);
            carrierSetter.Set(carrier, SpanId, context.RawSpanId);
            carrierSetter.Set(carrier, Sampled, sampled);
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            var rawTraceId = ParseUtility.ParseString(carrier, carrierGetter, TraceId)?.Trim();
            var rawSpanId = ParseUtility.ParseString(carrier, carrierGetter, SpanId)?.Trim();
            var samplingPriority = ParseUtility.ParseInt32(carrier, carrierGetter, Sampled);

            if (IsValidTraceId(rawTraceId) && IsValidSpanId(rawSpanId))
            {
                var success = HexString.TryParseTraceId(rawTraceId, out var traceId);

                if (!success || traceId == 0)
                {
                    return false;
                }

                if (!HexString.TryParseUInt64(rawSpanId, out var parentId))
                {
                    parentId = 0;
                }

                spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, null, rawTraceId, rawSpanId);
                return true;
            }

            return false;
        }

        private static bool IsValidTraceId([NotNullWhen(true)] string? traceId)
        {
            if (string.IsNullOrEmpty(traceId))
            {
                return false;
            }

            if (traceId!.Length != 16 && traceId.Length != 32)
            {
                return false;
            }

            return true;
        }

        private static bool IsValidSpanId([NotNullWhen(true)] string? spanId)
        {
            if (string.IsNullOrEmpty(spanId))
            {
                return false;
            }

            if (spanId!.Length != 16)
            {
                return false;
            }

            return true;
        }
    }
}
