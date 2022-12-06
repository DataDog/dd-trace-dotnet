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
    // https://www.w3.org/TR/trace-context/
    internal class W3CTraceContextPropagator : IContextInjector, IContextExtractor
    {
        /// <summary>
        /// W3C traceparent header name
        /// </summary>
        public const string TraceParentHeaderName = "traceparent";

        /// <summary>
        /// W3C tracestate header name
        /// </summary>
        public const string TraceStateHeaderName = "tracestate";

        public static readonly W3CTraceContextPropagator Instance = new();

        public void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            var traceparent = CreateTraceParentHeader(context);
            carrierSetter.Set(carrier, TraceParentHeaderName, traceparent);

            var tracestate = CreateTraceStateHeader(context);

            if (!string.IsNullOrWhiteSpace(tracestate))
            {
                carrierSetter.Set(carrier, TraceStateHeaderName, tracestate);
            }
        }

        internal static string CreateTraceParentHeader(SpanContext context)
        {
            var traceId = IsValidHexString(context.RawTraceId, length: 32) ? context.RawTraceId : context.TraceId.ToString("x32");
            var spanId = IsValidHexString(context.RawSpanId, length: 16) ? context.RawSpanId : context.SpanId.ToString("x16");
            var samplingPriority = context.TraceContext?.SamplingPriority ?? context.SamplingPriority ?? SamplingPriorityValues.AutoKeep;
            var sampled = samplingPriority > 0 ? "01" : "00";

            return $"00-{traceId}-{spanId}-{sampled}";
        }

        internal static string CreateTraceStateHeader(SpanContext context)
        {
            var samplingPriority = SamplingPriorityToString(context.TraceContext?.SamplingPriority ?? context.SamplingPriority);
            StringBuilder? sb = null;

            try
            {
                sb = StringBuilderCache.Acquire(100);
                sb.Append("dd=");

                if (samplingPriority != null)
                {
                    sb.Append("s:").Append(samplingPriority).Append(';');
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
#if NETCOREAPP
                            var key = tag.Key.AsSpan(start: 6);
#else
                            var key = tag.Key.Substring(startIndex: 6);
#endif

                            sb.Append("t.").Append(key).Append(':').Append(tag.Value).Append(';');
                        }
                    }
                }

                if (sb.Length == 3)
                {
                    // "dd=", we never appended anything
                    return string.Empty;
                }

                // remove trailing ";"
                if (sb[sb.Length - 1] == ';')
                {
                    sb.Length--;
                }

                return StringBuilderCache.GetStringAndRelease(sb);
            }
            finally
            {
                StringBuilderCache.Release(sb);
            }
        }

        internal static bool TryParseTraceParent(string header, out W3CTraceParent traceParent)
        {
            traceParent = default;

            if (header == null!)
            {
                return false;
            }

            if (header.Length != 55 || header[2] != '-' || header[35] != '-' || header[52] != '-')
            {
                // validate format
                return false;
            }

            if (header[0] != '0' || header[1] != '0')
            {
                // we only support traceparent version "00"
                return false;
            }

            char sampled = header[54];

            if (header[53] != '0' || (sampled != '0' && sampled != '1'))
            {
                return false;
            }

            ulong traceId;
            ulong parentId;
            string rawTraceId;
            string rawSpanId;

#if NETCOREAPP
            var w3cTraceId = header.AsSpan(start: 3, length: 32);
            var w3cSpanId = header.AsSpan(start: 36, length: 16);
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
            rawTraceId = header.Substring(startIndex: 3, length: 32);
            rawSpanId = header.Substring(startIndex: 36, length: 16);
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

            traceParent = new W3CTraceParent(
                traceId,
                parentId,
                sampled == '1',
                rawTraceId,
                rawSpanId);

            return true;
        }

        internal static bool TryParseTraceState(string header, out W3CTraceState traceState)
        {
            traceState = default;

            if (string.IsNullOrWhiteSpace(header))
            {
                return false;
            }

            header = header.Trim();

            if (header.Length < 6 || !header.StartsWith("dd=", StringComparison.Ordinal))
            {
                // shorted valid length is 6: "dd=s:1"
                return false;
            }

            int? samplingPriority = null;
            string? origin = null;
            StringBuilder? propagatedTagsBuilder = null;

            try
            {
                propagatedTagsBuilder = StringBuilderCache.Acquire(20);

                // name1:value1;
                //            ^ endIndex
                //      ^ colonIndex
                // ^ startIndex
                var startIndex = 3;

                while (true)
                {
                    if (startIndex > header.Length - 3)
                    {
                        // not enough chars left in the header value
                        break;
                    }

                    var endIndex = header.IndexOf(';', startIndex);

                    if (endIndex < 0)
                    {
                        // no more semicolons left in the header value,
                        // this pair goes on to the end of the string
                        endIndex = header.Length - 1;
                    }
                    else
                    {
                        // we want the char before the semicolon
                        endIndex--;
                    }

                    var length = endIndex - startIndex + 1;
                    var colonIndex = header.IndexOf(':', startIndex, length);

                    if (colonIndex <= startIndex || endIndex <= colonIndex)
                    {
                        // not a valid key/value pair, skip past the semicolon
                        // conditions:
                        // - colon not found, or
                        // - key length is 0, or
                        // - value length is 0
                        startIndex = endIndex + 2;
                        continue;
                    }

#if NETCOREAPP
                    var name = header.AsSpan(start: startIndex, length: colonIndex - startIndex);
                    var value = header.AsSpan(start: colonIndex + 1, length: endIndex - colonIndex);

                    if (name.Equals("s", StringComparison.Ordinal))
                    {
                        // SamplingPriorityToInt32(ReadOnlySpan<char>)
                        samplingPriority = SamplingPriorityToInt32(value);
                    }
                    else if (name.Equals("o", StringComparison.Ordinal))
                    {
                        origin = value.ToString();
                    }
                    else if (name.StartsWith("t.", StringComparison.Ordinal))
                    {
                        propagatedTagsBuilder.Append("_dd.p.").Append(name[2..]).Append('=').Append(value).Append(',');
                    }
#else
                    var name = header.Substring(startIndex, colonIndex - startIndex);
                    var value = header.Substring(colonIndex + 1, endIndex - colonIndex);

                    if (name == "s")
                    {
                        // SamplingPriorityToInt32(string)
                        samplingPriority = SamplingPriorityToInt32(value);
                    }
                    else if (name == "o")
                    {
                        origin = value;
                    }
                    else if (name.StartsWith("t.", StringComparison.Ordinal))
                    {
                        propagatedTagsBuilder.Append("_dd.p.").Append(name.Substring(2)).Append('=').Append(value).Append(',');
                    }
#endif

                    // skip past the semicolon
                    startIndex = endIndex + 2;
                }

                string? propagatedTags;

                if (propagatedTagsBuilder.Length > 0)
                {
                    // we can't use [^1] in .NET Framework without access to the Index and Range types
                    // ReSharper disable once UseIndexFromEndExpression
                    if (propagatedTagsBuilder[propagatedTagsBuilder.Length - 1] == ',')
                    {
                        propagatedTagsBuilder.Length--;
                    }

                    propagatedTags = StringBuilderCache.GetStringAndRelease(propagatedTagsBuilder);
                }
                else
                {
                    propagatedTags = null;
                }

                traceState = new W3CTraceState(samplingPriority, origin, propagatedTags);
                return true;
            }
            finally
            {
                StringBuilderCache.Release(propagatedTagsBuilder);
            }
        }

        [return: NotNullIfNotNull("samplingPriority")]
        private static string? SamplingPriorityToString(int? samplingPriority)
        {
            return samplingPriority switch
                   {
                       2 => "2",
                       1 => "1",
                       0 => "0",
                       -1 => "-1",
                       null => null,
                       not null => samplingPriority.Value.ToString(CultureInfo.InvariantCulture)
                   };
        }

        private static int? SamplingPriorityToInt32(string? samplingPriority)
        {
            return samplingPriority switch
                   {
                       "2" => 2,
                       "1" => 1,
                       "0" => 0,
                       "-1" => 1,
                       null or "" => null,
                       not null => int.TryParse(samplingPriority, out var result) ? result : null
                   };
        }

#if NETCOREAPP
        private static int? SamplingPriorityToInt32(ReadOnlySpan<char> samplingPriority)
        {
            if (samplingPriority.Equals("2", StringComparison.Ordinal))
            {
                return 2;
            }

            if (samplingPriority.Equals("1", StringComparison.Ordinal))
            {
                return 1;
            }

            if (samplingPriority.Equals("0", StringComparison.Ordinal))
            {
                return 0;
            }

            if (samplingPriority.Equals("-1", StringComparison.Ordinal))
            {
                return -1;
            }

            return int.TryParse(samplingPriority, out var result) ? result : null;
        }
#endif

        public bool TryExtract<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter carrierGetter, out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            // get the "traceparent" header
            var traceParentHeader = ParseUtility.ParseString(carrier, carrierGetter, TraceParentHeaderName)?.Trim();

            if (string.IsNullOrEmpty(traceParentHeader) || !TryParseTraceParent(traceParentHeader!, out var traceParent))
            {
                // "traceparent" header is required
                return false;
            }

            var traceStateHeader = ParseUtility.ParseString(carrier, carrierGetter, TraceStateHeaderName)?.Trim();

            if (string.IsNullOrEmpty(traceStateHeader) || !TryParseTraceState(traceStateHeader!, out var traceState))
            {
                // "tracestate" header is optional
                traceState = default;
            }

            // if we can't get the more specific sampling priority from "tracestate",
            // then fallback to the boolean in "traceparent"
            var samplingPriority = traceState.SamplingPriority ??
                                   (traceParent.Sampled ?
                                        SamplingPriorityValues.AutoKeep :
                                        SamplingPriorityValues.AutoReject);

            spanContext = new SpanContext(
                traceParent.TraceId,
                traceParent.ParentId,
                samplingPriority,
                serviceName: null,
                origin: traceState.Origin,
                traceParent.RawTraceId,
                traceParent.RawParentId);

            spanContext.PropagatedTags = traceState.PropagatedTags;
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
