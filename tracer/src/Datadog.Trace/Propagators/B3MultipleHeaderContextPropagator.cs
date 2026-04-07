// <copyright file="B3MultipleHeaderContextPropagator.cs" company="Datadog">
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
    internal sealed class B3MultipleHeaderContextPropagator : IContextInjector, IContextExtractor
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

        public PropagatorType PropagatorType => PropagatorType.TraceContext;

        public string DisplayName => "b3multi";

        public void Inject<TCarrier, TCarrierSetter>(PropagationContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            if (context.SpanContext is not { } spanContext)
            {
                // nothing to inject
                return;
            }

            TelemetryFactory.Metrics.RecordCountContextHeaderStyleInjected(MetricTags.ContextHeaderStyle.B3Multi);

            CreateHeaders(spanContext, out var traceId, out var spanId, out var sampled);

            carrierSetter.Set(carrier, TraceId, traceId);
            carrierSetter.Set(carrier, SpanId, spanId);
            carrierSetter.Set(carrier, Sampled, sampled);
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out PropagationContext context)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            context = default;

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
            var spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, null, rawTraceId, rawSpanId, isRemote: true);
            context = new PropagationContext(spanContext, baggage: null);

            TelemetryFactory.Metrics.RecordCountContextHeaderStyleExtracted(MetricTags.ContextHeaderStyle.B3Multi);
            return true;
        }

        internal static void CreateHeaders(SpanContext context, out string traceId, out string spanId, out string sampled)
        {
            traceId = context.RawTraceId;
            spanId = context.RawSpanId;

            var samplingPriority = context.GetOrMakeSamplingDecision() ?? SamplingPriorityValues.Default;
            sampled = SamplingPriorityValues.IsKeep(samplingPriority) ? "1" : "0";
        }
    }
}
