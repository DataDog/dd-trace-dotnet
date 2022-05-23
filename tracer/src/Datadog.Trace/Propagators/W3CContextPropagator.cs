// <copyright file="W3CContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Datadog.Trace.Propagators
{
    internal class W3CContextPropagator : IContextInjector, IContextExtractor
    {
        /// <summary>
        /// W3C TraceParent header
        /// </summary>
        public const string TraceParent = "traceparent";

        public void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            var traceId = IsValidTraceId(context.RawTraceId) ? context.RawTraceId : context.TraceId.ToString("x32");
            var spanId = IsValidSpanId(context.RawSpanId) ? context.RawSpanId : context.SpanId.ToString("x16");
            var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;
            var sampled = samplingPriority > 0 ? "01" : "00";
            carrierSetter.Set(carrier, TraceParent, $"00-{traceId}-{spanId}-{sampled}");
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            var traceParent = ParseUtility.ParseString(carrier, carrierGetter, TraceParent)?.Trim();
            if (!string.IsNullOrEmpty(traceParent))
            {
                // We found a trace parent (we are reading from the Http Headers)

                /* (https://www.w3.org/TR/trace-context/)
                    Valid traceparent when caller sampled this request:

                    Value = 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
                    base16(version) = 00
                    base16(trace-id) = 4bf92f3577b34da6a3ce929d0e0e4736
                    base16(parent-id) = 00f067aa0ba902b7
                    base16(trace-flags) = 01  // sampled
                    Valid traceparent when caller didn’t sample this request:

                    Value = 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00
                    base16(version) = 00
                    base16(trace-id) = 4bf92f3577b34da6a3ce929d0e0e4736
                    base16(parent-id) = 00f067aa0ba902b7
                    base16(trace-flags) = 00  // not sampled
                 */

                if (traceParent!.Length != 55 || traceParent[2] != '-' || traceParent[35] != '-' || traceParent[52] != '-')
                {
                    return false;
                }

                char w3cSampled = traceParent[54];
                if (traceParent[53] != '0' || (w3cSampled != '0' && w3cSampled != '1'))
                {
                    return false;
                }

                var samplingPriority = w3cSampled == '0' ? 0 : 1;

#if NETCOREAPP
                var w3cTraceId = traceParent.AsSpan(3, 32);
                var w3cSpanId = traceParent.AsSpan(36, 16);
                var traceId = ParseUtility.ParseFromHexOrDefault(w3cTraceId.Slice(16));
                if (traceId == 0)
                {
                    return false;
                }

                var parentId = ParseUtility.ParseFromHexOrDefault(w3cSpanId);

                spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, null, w3cTraceId.ToString(), w3cSpanId.ToString());
#else
                var w3cTraceId = traceParent.Substring(3, 32);
                var w3cSpanId = traceParent.Substring(36, 16);
                var traceId = ParseUtility.ParseFromHexOrDefault(w3cTraceId.Substring(16));
                if (traceId == 0)
                {
                    return false;
                }

                var parentId = ParseUtility.ParseFromHexOrDefault(w3cSpanId);

                spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, null, w3cTraceId, w3cSpanId);
#endif

                return true;
            }

            return false;
        }

        private bool IsValidTraceId([NotNullWhen(true)] string? traceId)
        {
            if (string.IsNullOrEmpty(traceId))
            {
                return false;
            }

            if (traceId!.Length != 32)
            {
                return false;
            }

            return true;
        }

        private bool IsValidSpanId([NotNullWhen(true)] string? spanId)
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
