// <copyright file="W3CSpanContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Propagators
{
    internal class W3CSpanContextPropagator : ISpanContextInjector, ISpanContextExtractor
    {
        public string Name => "W3C";

        public void Inject<TCarrier>(SpanContext context, TCarrier carrier, Action<TCarrier, string, string> setter)
        {
            var traceId = IsValidTraceId(context.RawTraceId) ? context.RawTraceId : context.TraceId.ToString("x32");
            var spanId = IsValidSpanId(context.RawSpanId) ? context.RawSpanId : context.SpanId.ToString("x16");
            var sampled = context.SamplingPriority > 0 ? "01" : "00";
            var traceParent = $"00-{traceId}-{spanId}-{sampled}";
            setter(carrier, HttpHeaderNames.TraceParent, traceParent);
        }

        public bool TryExtract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, out SpanContext? spanContext)
        {
            spanContext = null;

            var traceParent = ParseUtility.ParseString(carrier, getter, HttpHeaderNames.TraceParent)?.Trim();
            if (!string.IsNullOrEmpty(traceParent))
            {
                // We found a trace parent (we are reading from the Http Headers)

                if (traceParent!.Length != 55 || traceParent[2] != '-' || traceParent[35] != '-' || traceParent[52] != '-')
                {
                    return false;
                }

                var w3cSampled = traceParent.Substring(53, 2);
                if (w3cSampled != "00" && w3cSampled != "01")
                {
                    return false;
                }

                var w3cTraceId = traceParent.Substring(3, 32);
                var w3cSpanId = traceParent.Substring(36, 16);

                var traceId = Convert.ToUInt64(w3cTraceId.Substring(16), 16);
                var parentId = Convert.ToUInt64(w3cSpanId, 16);
                var samplingPriority = w3cSampled == "00" ? 0 : 1;

                spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, null) { RawTraceId = w3cTraceId };
                return true;
            }

            // If a traceParent cannot be found, we check if the carrier is from a distributed context
            var rawTraceId = ParseUtility.ParseString(carrier, getter, SpanContext.RawTraceIdKey)?.Trim();
            if (!IsValidTraceId(rawTraceId))
            {
                // If the traceId is not valid
                return false;
            }

            var rawSpanId = ParseUtility.ParseString(carrier, getter, SpanContext.RawSpanIdKey)?.Trim();
            if (!IsValidSpanId(rawSpanId))
            {
                // If the spanId is not valid
                return false;
            }

            var tId = Convert.ToUInt64(rawTraceId!.Substring(16), 16);
            var pId = Convert.ToUInt64(rawSpanId, 16);
            var smpPriority = ParseUtility.ParseInt32(carrier, getter, HttpHeaderNames.SamplingPriority);

            spanContext = new SpanContext(tId, pId, smpPriority, serviceName: null, origin: null)
            {
                RawTraceId = rawTraceId,
                RawSpanId = rawSpanId
            };

            return true;
        }

        private bool IsValidTraceId(string? traceId)
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

        private bool IsValidSpanId(string? spanId)
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
