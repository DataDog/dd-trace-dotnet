// <copyright file="W3CTraceContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    internal class W3CTraceContextPropagator : IContextInjector, IContextExtractor
    {
        /// <summary>
        /// W3C traceparent header
        /// </summary>
        public const string TraceParent = "traceparent";

        /// <summary>
        /// W3C tracestate header
        /// </summary>
        public const string TraceState = "tracestate";

        public static readonly W3CTraceContextPropagator Instance = new();

        public void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            InjectTraceParent(context, carrier, carrierSetter);
            InjectTraceState(context, carrier, carrierSetter);
        }

        internal static void InjectTraceParent<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            var traceId = IsValidHexString(context.RawTraceId, length: 32) ? context.RawTraceId : context.TraceId.ToString("x32");
            var spanId = IsValidHexString(context.RawSpanId, length: 16) ? context.RawSpanId : context.SpanId.ToString("x16");
            var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;
            var sampled = samplingPriority > 0 ? "01" : "00";

            carrierSetter.Set(carrier, TraceParent, $"00-{traceId}-{spanId}-{sampled}");
        }

        internal static void InjectTraceState<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority;

            var samplingPriorityString = samplingPriority switch
                                         {
                                            -1 => "-1",
                                            0 => "0",
                                            1 => "1",
                                            2 => "2",
                                            null => null,
                                            not null => samplingPriority.Value.ToString(CultureInfo.InvariantCulture)
                                         };

            StringBuilder? sb = null;

            try
            {
                sb = StringBuilderCache.Acquire(100);

                if (samplingPriorityString != null)
                {
                    sb.Append("s:").Append(samplingPriorityString).Append(';');
                }

                if (!string.IsNullOrWhiteSpace(context.Origin))
                {
                    sb.Append("o:").Append(context.Origin).Append(';');
                }

                if (context.TraceContext?.Tags?.ToArray() is { Length: > 0 } tags)
                {
                    foreach (var tag in tags)
                    {
                        if (tag.Key.StartsWith("_dd.p.", StringComparison.Ordinal))
                        {
                            var key = tag.Key.Substring(6);
                            sb.Append("t.").Append(key).Append(':').Append(tag.Value).Append(';');
                        }
                    }
                }

                var tracestate = StringBuilderCache.GetStringAndRelease(sb);
                carrierSetter.Set(carrier, TraceState, tracestate);
            }
            finally
            {
                StringBuilderCache.Release(sb);
            }
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            var traceParent = ParseUtility.ParseString(carrier, carrierGetter, TraceParent)?.Trim();

            if (string.IsNullOrEmpty(traceParent))
            {
                return false;
            }

            /*
            https://www.w3.org/TR/trace-context/

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
                // validate format
                return false;
            }

            if (traceParent[0] != '0' || traceParent[1] != '0')
            {
                // we only support traceparent version "00"
                return false;
            }

            char w3cSampled = traceParent[54];

            if (traceParent[53] != '0' || (w3cSampled != '0' && w3cSampled != '1'))
            {
                return false;
            }

            var samplingPriority = w3cSampled == '0' ? 0 : 1;
            ulong traceId;
            ulong parentId;
            string rawTraceId;
            string rawSpanId;

#if NETCOREAPP
            var w3cTraceId = traceParent.AsSpan(3, 32);
            var w3cSpanId = traceParent.AsSpan(36, 16);
            traceId = ParseUtility.ParseFromHexOrDefault(w3cTraceId[16..]);

            if (traceId == 0)
            {
                return false;
            }

            parentId = ParseUtility.ParseFromHexOrDefault(w3cSpanId);

            if (parentId == 0)
            {
                return false;
            }

            rawTraceId = w3cTraceId.ToString();
            rawSpanId = w3cSpanId.ToString();
#else
            rawTraceId = traceParent.Substring(3, 32);
            rawSpanId = traceParent.Substring(36, 16);
            traceId = ParseUtility.ParseFromHexOrDefault(rawTraceId.Substring(16));

            if (traceId == 0)
            {
                return false;
            }

            parentId = ParseUtility.ParseFromHexOrDefault(rawSpanId);

            if (parentId == 0)
            {
                return false;
            }
#endif

            spanContext = new SpanContext(traceId, parentId, samplingPriority, serviceName: null, origin: null, rawTraceId, rawSpanId);
            return true;
        }

        private static bool IsValidHexString([NotNullWhen(true)] string? value, int length)
        {
            if (value?.Length != length)
            {
                return false;
            }

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] is (< '0' or > '9') and (< 'a' or > 'f'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
