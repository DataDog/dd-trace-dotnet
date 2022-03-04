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
            if (!string.IsNullOrEmpty(context.TraceParent))
            {
                setter(carrier, HttpHeaderNames.TraceParent, context.TraceParent);
                return;
            }

            var traceId = context.TraceId.ToString("x32");
            var spanId = context.SpanId.ToString("x16");
            var sampled = context.SamplingPriority > 0 ? "01" : "00";
            var traceParent = $"00-{traceId}-{spanId}-{sampled}";
            setter(carrier, HttpHeaderNames.TraceParent, traceParent);
        }

        public bool TryExtract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, out SpanContext? spanContext)
        {
            spanContext = null;

            var traceParent = ParseUtility.ParseString(carrier, getter, HttpHeaderNames.TraceParent)?.Trim();
            if (string.IsNullOrEmpty(traceParent))
            {
                return false;
            }

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

            spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, null) { TraceParent = traceParent };
            return true;
        }
    }
}
