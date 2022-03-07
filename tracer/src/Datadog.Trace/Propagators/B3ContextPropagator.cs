// <copyright file="B3ContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Propagators
{
    internal class B3ContextPropagator : IContextInjector, IContextExtractor
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

        public void Inject<TCarrier>(SpanContext context, TCarrier carrier, Action<TCarrier, string, string> setter)
        {
            var traceId = IsValidTraceId(context.RawTraceId) ? context.RawTraceId : context.TraceId.ToString("x32");
            var spanId = IsValidSpanId(context.RawSpanId) ? context.RawSpanId : context.SpanId.ToString("x16");
            var sampled = context.SamplingPriority > 0 ? "1" : "0";

            setter(carrier, TraceId, traceId);
            setter(carrier, SpanId, spanId);
            setter(carrier, Sampled, sampled);
        }

        public bool TryExtract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, out SpanContext? spanContext)
        {
            spanContext = null;

            var rawTraceId = ParseUtility.ParseString(carrier, getter, TraceId)?.Trim();
            var rawSpanId = ParseUtility.ParseString(carrier, getter, SpanId)?.Trim();
            var samplingPriority = ParseUtility.ParseInt32(carrier, getter, Sampled);
            if (IsValidTraceId(rawTraceId) && IsValidSpanId(rawSpanId))
            {
                var traceId = rawTraceId!.Length == 32 ?
                                  Convert.ToUInt64(rawTraceId.Substring(16), 16) :
                                  Convert.ToUInt64(rawTraceId, 16);
                var parentId = Convert.ToUInt64(rawSpanId, 16);

                spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, null)
                {
                    RawTraceId = rawTraceId,
                    RawSpanId = rawSpanId
                };

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

            if (traceId!.Length != 16 && traceId!.Length != 32)
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
