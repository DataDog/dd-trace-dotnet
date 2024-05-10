// <copyright file="B3SingleHeaderContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal class B3SingleHeaderContextPropagator : IContextInjector, IContextExtractor
    {
        /// <summary>
        /// B3 single header
        /// </summary>
        public const string B3 = "b3";

        public static readonly B3SingleHeaderContextPropagator Instance = new();

        private B3SingleHeaderContextPropagator()
        {
        }

        public void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            TelemetryFactory.Metrics.RecordCountContextHeaderStyleInjected(MetricTags.ContextHeaderStyle.B3SingleHeader);
            var header = CreateHeader(context);
            carrierSetter.Set(carrier, B3, header);
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            var brValue = ParseUtility.ParseString(carrier, carrierGetter, B3)?.Trim();
            if (!string.IsNullOrEmpty(brValue))
            {
                // We found a trace parent (we are reading from the Http Headers)

                // 80f198ee56343ba864fe8b2a57d3eff7-e457b5a2e4d86bd1-1-05e3ac9a4f6e3b90
                // 80f198ee56343ba864fe8b2a57d3eff7-e457b5a2e4d86bd1-1
                // e457b5a2e4d86bd1-e457b5a2e4d86bd1-1-05e3ac9a4f6e3b90
                // e457b5a2e4d86bd1-e457b5a2e4d86bd1-1
                if (brValue!.Length is not 68 and not 51 and not 52 and not 35)
                {
                    return false;
                }

#if NETCOREAPP
                ReadOnlySpan<char> rawTraceId;
                ReadOnlySpan<char> rawSpanId;
                char rawSampled;

                if (brValue.Length > 50 && brValue[32] == '-' && brValue[49] == '-')
                {
                    // 128 bits trace id
                    rawTraceId = brValue.AsSpan(0, 32);
                    rawSpanId = brValue.AsSpan(33, 16);
                    rawSampled = brValue[50];
                }
                else if (brValue.Length > 34 && brValue[16] == '-' && brValue[33] == '-')
                {
                    // 64 bits trace id
                    rawTraceId = brValue.AsSpan(0, 16);
                    rawSpanId = brValue.AsSpan(17, 16);
                    rawSampled = brValue[34];
                }
                else
                {
                    return false;
                }

                var success = HexString.TryParseTraceId(rawTraceId, out var traceId);

                if (!success || traceId == TraceId.Zero)
                {
                    return false;
                }

                if (!HexString.TryParseUInt64(rawSpanId, out var parentId))
                {
                    parentId = 0;
                }

                var samplingPriority = rawSampled == '1' ? 1 : 0;
                spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, null, rawTraceId.ToString(), rawSpanId.ToString(), isRemote: true);
#else
                string? rawTraceId;
                string? rawSpanId;
                char rawSampled;

                if (brValue.Length > 50 && brValue[32] == '-' && brValue[49] == '-')
                {
                    // 128-bit trace id
                    rawTraceId = brValue.Substring(0, 32);
                    rawSpanId = brValue.Substring(33, 16);
                    rawSampled = brValue[50];
                }
                else if (brValue.Length > 34 && brValue[16] == '-' && brValue[33] == '-')
                {
                    // 64-bit trace id
                    rawTraceId = brValue.Substring(0, 16);
                    rawSpanId = brValue.Substring(17, 16);
                    rawSampled = brValue[34];
                }
                else
                {
                    return false;
                }

                var success = HexString.TryParseTraceId(rawTraceId, out var traceId);

                if (!success || traceId == TraceId.Zero)
                {
                    return false;
                }

                if (!HexString.TryParseUInt64(rawSpanId, out var parentId))
                {
                    parentId = 0;
                }

                var samplingPriority = rawSampled == '1' ? 1 : 0;
                spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, null, rawTraceId, rawSpanId, isRemote: true);
#endif

                TelemetryFactory.Metrics.RecordCountContextHeaderStyleExtracted(MetricTags.ContextHeaderStyle.B3SingleHeader);
                return true;
            }

            return false;
        }

        internal static string CreateHeader(SpanContext context)
        {
            var samplingPriority = context.GetOrMakeSamplingDecision() ?? SamplingPriorityValues.Default;
            var sampled = SamplingPriorityValues.IsKeep(samplingPriority) ? "1" : "0";

#if NET6_0_OR_GREATER
            return string.Create(null, stackalloc char[128], $"{context.RawTraceId}-{context.RawSpanId}-{sampled}");
#else
            return $"{context.RawTraceId}-{context.RawSpanId}-{sampled}";
#endif
        }
    }
}
