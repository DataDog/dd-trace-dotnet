// <copyright file="B3SingleHeaderContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Propagators
{
    internal class B3SingleHeaderContextPropagator : IContextInjector, IContextExtractor
    {
        /// <summary>
        /// B3 single header
        /// </summary>
        public const string B3 = "b3";

        public void Inject<TCarrier>(SpanContext context, TCarrier carrier, Action<TCarrier, string, string> setter)
        {
            var traceId = IsValidTraceId(context.RawTraceId) ? context.RawTraceId : context.TraceId.ToString("x16");
            var spanId = IsValidSpanId(context.RawSpanId) ? context.RawSpanId : context.SpanId.ToString("x16");
            var sampled = context.SamplingPriority > 0 ? "1" : "0";
            var brValue = $"{traceId}-{spanId}-{sampled}";
            setter(carrier, B3, brValue);
        }

        public bool TryExtract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, out SpanContext? spanContext)
        {
            spanContext = null;

            var brValue = ParseUtility.ParseString(carrier, getter, B3)?.Trim();
            if (!string.IsNullOrEmpty(brValue))
            {
                // We found a trace parent (we are reading from the Http Headers)

                // 80f198ee56343ba864fe8b2a57d3eff7-e457b5a2e4d86bd1-1-05e3ac9a4f6e3b90
                // 80f198ee56343ba864fe8b2a57d3eff7-e457b5a2e4d86bd1-1
                // e457b5a2e4d86bd1-e457b5a2e4d86bd1-1-05e3ac9a4f6e3b90
                // e457b5a2e4d86bd1-e457b5a2e4d86bd1-1
                if (brValue!.Length != 68 && brValue!.Length != 51 &&
                    brValue!.Length != 52 && brValue!.Length != 35)
                {
                    return false;
                }

                string? rawTraceId = null;
                string? rawSpanId = null;
                string? rawSampled = null;
                if (brValue.Length > 50 && brValue[32] == '-' && brValue[49] == '-')
                {
                    // 128 bits trace id
                    rawTraceId = brValue.Substring(0, 32);
                    rawSpanId = brValue.Substring(33, 16);
                    rawSampled = brValue.Substring(50, 1);
                }
                else if (brValue.Length > 34 && brValue[16] == '-' && brValue[33] == '-')
                {
                    // 64 bits trace id
                    rawTraceId = brValue.Substring(0, 16);
                    rawSpanId = brValue.Substring(17, 16);
                    rawSampled = brValue.Substring(34, 1);
                }
                else
                {
                    return false;
                }

                var traceId = rawTraceId!.Length == 32 ?
                                  Convert.ToUInt64(rawTraceId.Substring(16), 16) :
                                  Convert.ToUInt64(rawTraceId, 16);
                var parentId = Convert.ToUInt64(rawSpanId, 16);
                var samplingPriority = rawSampled == "1" ? 1 : 0;

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
