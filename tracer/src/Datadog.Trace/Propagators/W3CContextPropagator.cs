// <copyright file="W3CContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Propagators
{
    internal class W3CContextPropagator : IContextInjector, IContextExtractor
    {
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

            return false;
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
