// <copyright file="W3CTraceContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    // https://www.w3.org/TR/trace-context/
    internal class W3CTraceContextPropagator : IContextInjector, IContextExtractor
    {
        private const string TraceStateHeaderValuesSeparator = ",";

        private const char LowerBound = '\u0020'; // decimal: 32, ' ' (space)
        private const char UpperBound = '\u007e'; // decimal: 126, '~' (tilde)
        private const char OutOfBoundsReplacement = '_';

        private static readonly KeyValuePair<char, char>[] InjectOriginReplacements =
        {
            new(',', '_'),
            new(';', '_'),
            new('=', '_'),
        };

        private static readonly KeyValuePair<char, char>[] InjectPropagatedTagKeyReplacements =
        {
            new(' ', '_'),
            new(',', '_'),
            new('=', '_'),
        };

        private static readonly KeyValuePair<char, char>[] InjectPropagatedTagValueReplacements =
        {
            new(',', '_'),
            new(';', '_'),
            new('~', '_'),
            new('=', '~'), // note '=' is encoded as '~' when injecting
        };

        private static readonly KeyValuePair<char, char>[] ExtractPropagatedTagValueReplacements =
        {
            new('~', '='), // note '~' is decoded as '~' when extracting
        };

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
            var sb = StringBuilderCache.Acquire(100);

            try
            {
                sb.Append("dd=");

                // sampling priority ("s:<value>")
                var samplingPriority = SamplingPriorityToString(context.TraceContext?.SamplingPriority);

                if (samplingPriority != null)
                {
                    sb.Append("s:").Append(samplingPriority).Append(';');
                }

                // origin ("o:<value>")
                var origin = context.TraceContext?.Origin;

                if (!string.IsNullOrWhiteSpace(origin))
                {
                    var replacedOrigin = ReplaceCharacters(origin!, LowerBound, UpperBound, OutOfBoundsReplacement, InjectOriginReplacements);
                    sb.Append("o:").Append(replacedOrigin).Append(';');
                }

                // propagated tags ("t.<key>:<value>")
                if (context.TraceContext?.Tags?.ToArray() is { Length: > 0 } tags)
                {
                    foreach (var tag in tags)
                    {
                        if (tag.Key.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.Ordinal))
                        {
#if NETCOREAPP
                            var key = tag.Key.AsSpan(start: 6);
#else
                            var key = tag.Key.Substring(startIndex: 6);
#endif

                            var tagKey = ReplaceCharacters(key, LowerBound, UpperBound, OutOfBoundsReplacement, InjectPropagatedTagKeyReplacements);
                            var tagValue = ReplaceCharacters(tag.Value, LowerBound, UpperBound, OutOfBoundsReplacement, InjectPropagatedTagValueReplacements);
                            sb.Append("t.").Append(tagKey).Append(':').Append(tagValue).Append(';');
                        }
                    }
                }

                if (sb.Length == 3)
                {
                    // remove "dd=" since we never appended anything
                    sb.Clear();
                }
                else if (sb[sb.Length - 1] == ';')
                {
                    // remove trailing ";"
                    sb.Length--;
                }

                // additional tracestate from other vendors
                var additionalState = context.TraceContext?.AdditionalW3CTraceState;

                if (!string.IsNullOrWhiteSpace(additionalState))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(additionalState!.Trim());
                }

                return sb.ToString();
            }
            finally
            {
                StringBuilderCache.Release(sb);
            }
        }

        internal static bool TryParseTraceParent(string header, out W3CTraceParent traceParent)
        {
            // "{version:2}-{trace-id:32}-{parent-id:16}-{trace-flags:2}"
            //             ^ 2           ^ 35           ^ 52            ^ 55

            traceParent = default;

            if (header == null!)
            {
                return false;
            }

            header = header.Trim();

            if (header.Length < 55 || header[2] != '-' || header[35] != '-' || header[52] != '-')
            {
                // too short, or invalid delimiter positions
                return false;
            }

            if (header[0] < '0' || header[0] > 'f' || header[1] < '0' || header[1] > 'f')
            {
                // invalid version value, must contain lower-case hexadecimal characters
                return false;
            }

            if (header[0] == 'f' && header[1] == 'f')
            {
                // while "ff" is valid hex, it is explicitly not allowed as a version value
                return false;
            }

            if (header[0] == '0' && header[1] == '0' && header.Length != 55)
            {
                // for version "00", the length must be exactly 55
                return false;
            }

            if (header.Length > 55 && header[55] != '-')
            {
                // if there is more data than expected (e.g. future version of the spec),
                // it's should to be additive, so there must be another delimiter after `trace-tags`
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

            var traceFlags = header.AsSpan(start: 53, length: 2);
            var sampled = (ParseUtility.ParseFromHexOrDefault(traceFlags) & 1) == 1;
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

            var traceFlags = header.Substring(53, 2);
            var sampled = (ParseUtility.ParseFromHexOrDefault(traceFlags) & 1) == 1;
#endif

            traceParent = new W3CTraceParent(
                traceId: traceId,
                parentId: parentId,
                sampled: sampled,
                rawTraceId: rawTraceId,
                rawParentId: rawSpanId);

            return true;
        }

        internal static W3CTraceState ParseTraceState(string header)
        {
            var otherHeaderValues = string.Empty;

            // header format: "[*,]dd=s:1;o:rum;t.dm:-4;t.usr.id:12345[,*]"
            if (string.IsNullOrWhiteSpace(header))
            {
                return new W3CTraceState(null, null, null, otherHeaderValues);
            }

            SplitTraceStateValues(header, out var ddValues, out otherHeaderValues);

            if (ddValues.Length < 6)
            {
                // "dd" section too short, shortest length is 6, "dd=a:b"
                return new W3CTraceState(null, null, null, otherHeaderValues);
            }

            int? samplingPriority = null;
            string? origin = null;
            var propagatedTagsBuilder = StringBuilderCache.Acquire(50);

            try
            {
                // skip "dd="
                var startIndex = 3;

                // name1:value1;
                //             ^ endIndex
                //      ^ colonIndex
                // ^ startIndex
                while (true)
                {
                    if (startIndex > ddValues.Length - 3)
                    {
                        // not enough chars left, we need at least 3, "a:b"
                        break;
                    }

                    // search for next separator semicolon
                    var endIndex = ddValues.IndexOf(';', startIndex);

                    if (endIndex < 0)
                    {
                        // no more semicolons left,
                        // this key/value pair goes on to the end of ddValues
                        endIndex = ddValues.Length;
                    }

                    var colonIndex = ddValues.IndexOf(':', startIndex, endIndex - startIndex);

                    if (colonIndex <= startIndex || endIndex - 1 <= colonIndex)
                    {
                        // not a valid key/value pair, skip past the semicolon
                        // conditions:
                        // - colon not found, or
                        // - key length is 0, or
                        // - value length is 0
                        startIndex = endIndex + 1;
                        continue;
                    }

#if NETCOREAPP
                    var name = ddValues.AsSpan(start: startIndex, length: colonIndex - startIndex);
                    var value = ddValues.AsSpan(start: colonIndex + 1, length: endIndex - colonIndex - 1);

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
                        value = ReplaceCharacters(value, LowerBound, UpperBound, OutOfBoundsReplacement, ExtractPropagatedTagValueReplacements);
                        propagatedTagsBuilder.Append(TagPropagation.PropagatedTagPrefix).Append(name[2..]).Append('=').Append(value).Append(',');
                    }
#else
                    var name = ddValues.Substring(startIndex: startIndex, length: colonIndex - startIndex);
                    var value = ddValues.Substring(startIndex: colonIndex + 1, length: endIndex - colonIndex - 1);

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
                        value = ReplaceCharacters(value, LowerBound, UpperBound, OutOfBoundsReplacement, ExtractPropagatedTagValueReplacements);
                        propagatedTagsBuilder.Append(TagPropagation.PropagatedTagPrefix).Append(name.Substring(2)).Append('=').Append(value).Append(',');
                    }
#endif

                    // skip past the semicolon
                    startIndex = endIndex + 1;
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

                    propagatedTags = propagatedTagsBuilder.ToString();
                }
                else
                {
                    propagatedTags = null;
                }

                return new W3CTraceState(samplingPriority, origin, propagatedTags, otherHeaderValues);
            }
            finally
            {
                StringBuilderCache.Release(propagatedTagsBuilder);
            }
        }

        internal static void SplitTraceStateValues(string header, out string ddValues, out string additionalValues)
        {
            // header format: "[*,]dd=s:1;o:rum;t.dm:-4;t.usr.id:12345[,*]"
            ddValues = string.Empty;
            additionalValues = string.Empty;

            if (string.IsNullOrWhiteSpace(header))
            {
                return;
            }

            header = header.Trim();
            int ddStartIndex;

            if (header.StartsWith("dd=", StringComparison.Ordinal))
            {
                ddStartIndex = 0;
            }
            else
            {
                // if "dd=" is not at start of header, make sure we find the one preceded by comma
                // in case there is something like "key1=valuedd=whatisthis,dd=..."
                //                                                          ^ take this one
                //                                            ^ ignore this one
                ddStartIndex = header.IndexOf(",dd=", StringComparison.Ordinal);

                if (ddStartIndex >= 0)
                {
                    // skip the ',' (but don't add 1 if ddStartIndex == -1)
                    ddStartIndex++;
                }
            }

            if (ddStartIndex < 0)
            {
                // "dd=" was not found in header
                additionalValues = header;
                return;
            }

            var ddEndIndex = header.IndexOf(',', ddStartIndex + 3);

            if (ddEndIndex < 0)
            {
                // comma not found, "dd=" reaches the end of header
                ddEndIndex = header.Length;
            }

            ddValues = header.Substring(ddStartIndex, ddEndIndex - ddStartIndex);

            if (ddStartIndex == 0 && ddEndIndex == header.Length)
            {
                // "dd" was the only key, no additional values
                additionalValues = string.Empty;
            }
            else if (ddStartIndex == 0)
            {
                // "dd" first, additional values later
                additionalValues = header.Substring(ddEndIndex + 1, header.Length - ddEndIndex - 1);
            }
            else if (ddEndIndex == header.Length)
            {
                // additional values first, "dd" later
                additionalValues = header.Substring(0, ddStartIndex - 1);
            }
            else
            {
                // additional values on both sides, "dd" in the middle
                var otherValuesLeft = header.Substring(0, ddStartIndex - 1);
                var otherValuesRight = header.Substring(ddEndIndex + 1, header.Length - ddEndIndex - 1);
                additionalValues = string.Join(",", otherValuesLeft, otherValuesRight);
            }

            additionalValues = additionalValues.Trim();
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

#if NETCOREAPP
        private static int? SamplingPriorityToInt32(ReadOnlySpan<char> samplingPriority)
        {
            return samplingPriority switch
                   {
                       "2" => 2,
                       "1" => 1,
                       "0" => 0,
                       "-1" => -1,
                       "" => null,
                       _ => int.TryParse(samplingPriority, out var result) ? result : null
                   };
        }
#else
        private static int? SamplingPriorityToInt32(string? samplingPriority)
        {
            return samplingPriority switch
                   {
                       "2" => 2,
                       "1" => 1,
                       "0" => 0,
                       "-1" => -1,
                       null or "" => null,
                       not null => int.TryParse(samplingPriority, out var result) ? result : null
                   };
        }
#endif

        public bool TryExtract<TCarrier, TCarrierGetter>(
            TCarrier carrier,
            TCarrierGetter carrierGetter,
            [NotNullWhen(true)] out SpanContext? spanContext)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            spanContext = null;

            // get the "traceparent" header
            var traceParentHeaders = carrierGetter.Get(carrier, TraceParentHeaderName);

            if (!TryGetSingle(traceParentHeaders, out var traceParentHeader) ||
                string.IsNullOrWhiteSpace(traceParentHeader) ||
                !TryParseTraceParent(traceParentHeader, out var traceParent))
            {
                // a single "traceparent" header is required
                return false;
            }

            // get the "tracestate" header
            var traceStateHeaders = carrierGetter.Get(carrier, TraceStateHeaderName);
            var traceStateHeader = TrimAndJoinStrings(traceStateHeaders);
            var traceState = ParseTraceState(traceStateHeader);

            // if we can't get the more specific sampling priority from "tracestate",
            // then fallback to the boolean in "traceparent"
            var samplingPriority = traceState.SamplingPriority ??
                                   (traceParent.Sampled ?
                                        SamplingPriorityValues.AutoKeep :
                                        SamplingPriorityValues.AutoReject);

            spanContext = new SpanContext(
                traceId: traceParent.TraceId,
                spanId: traceParent.ParentId,
                samplingPriority: samplingPriority,
                serviceName: null,
                origin: traceState.Origin,
                rawTraceId: traceParent.RawTraceId,
                rawSpanId: traceParent.RawParentId);

            spanContext.PropagatedTags = traceState.PropagatedTags;
            spanContext.AdditionalW3CTraceState = traceState.AdditionalValues;
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

        private static bool TryGetSingle(IEnumerable<string?> values, out string value)
        {
            if (values is IReadOnlyList<string?> list)
            {
                if (list.Count == 1)
                {
                    value = list[0] ?? string.Empty;
                    return true;
                }

                value = string.Empty;
                return false;
            }

            return TryGetSingleRare(values, out value);
        }

        private static bool TryGetSingleRare(IEnumerable<string?> values, out string value)
        {
            value = string.Empty;
            var hasValue = false;

            foreach (var s in values)
            {
                if (!hasValue)
                {
                    // save first item
                    value = s ?? string.Empty;
                    hasValue = true;
                }
                else
                {
                    // we already saved the first item and there is a second one
                    return false;
                }
            }

            // there were no items
            return false;
        }

        private static string TrimAndJoinStrings(IEnumerable<string?> values)
        {
            switch (values)
            {
                case IReadOnlyList<string?> { Count: 1 } list:
                    // fast path for single values
                    return list[0]?.Trim() ?? string.Empty;
                case IReadOnlyCollection<string?> { Count: 0 }:
                case null:
                    // fast path for null or empty collections
                    return string.Empty;
                default:
                    return TrimAndJoinStringsRare(values);
            }
        }

        private static string TrimAndJoinStringsRare(IEnumerable<string?> values)
        {
            static void AppendIfNullOrWhiteSpace(StringBuilder sb, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sb.Append(value!.Trim()).Append(TraceStateHeaderValuesSeparator);
                }
            }

            var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

            switch (values)
            {
                case string?[] array:
                    // converts into a `for` loop
                    foreach (var value in array)
                    {
                        AppendIfNullOrWhiteSpace(sb, value);
                    }

                    break;

                case List<string?> list:
                    // uses List<T>'s struct enumerator
                    foreach (var value in list)
                    {
                        AppendIfNullOrWhiteSpace(sb, value);
                    }

                    break;

                default:
                    foreach (var value in values)
                    {
                        AppendIfNullOrWhiteSpace(sb, value);
                    }

                    break;
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

#if NETCOREAPP
        public static bool NeedsCharacterReplacement(ReadOnlySpan<char> value, char lowerBound, char upperBound, KeyValuePair<char, char>[] replacements)
#else
        public static bool NeedsCharacterReplacement(string value, char lowerBound, char upperBound, KeyValuePair<char, char>[] replacements)
#endif
        {
            foreach (var c in value)
            {
                if (c < lowerBound || c > upperBound)
                {
                    return true;
                }

                foreach (var pair in replacements)
                {
                    if (c == pair.Key)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

#if NETCOREAPP
        public static ReadOnlySpan<char> ReplaceCharacters(ReadOnlySpan<char> value, char lowerBound, char upperBound, char outOfBoundsReplacement, KeyValuePair<char, char>[] replacements)
#else
        public static string ReplaceCharacters(string value, char lowerBound, char upperBound, char outOfBoundsReplacement, KeyValuePair<char, char>[] replacements)
#endif
        {
            if (!NeedsCharacterReplacement(value, lowerBound, upperBound, replacements))
            {
                // common case, no replacements
                return value;
            }

            var sb = StringBuilderCache.Acquire(value.Length);
            sb.Append(value);

            for (var i = 0; i < sb.Length; i++)
            {
                var c = value[i];

                if (c < lowerBound || c > upperBound)
                {
                    sb[i] = outOfBoundsReplacement;
                }
            }

            foreach (var replacement in replacements)
            {
                sb.Replace(replacement.Key, replacement.Value);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}
