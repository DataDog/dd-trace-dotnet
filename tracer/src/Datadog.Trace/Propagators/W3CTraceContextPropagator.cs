// <copyright file="W3CTraceContextPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    // https://www.w3.org/TR/trace-context/
    internal class W3CTraceContextPropagator : IContextInjector, IContextExtractor
    {
        // the standard W3C separator between top-level key/value pairs
        // "key1=value1,key2=value2"
        //             ^
        private const char TraceStateHeaderValuesSeparator = ',';

        // the separator used between key/value pairs embedded inside the "dd" value
        // "key1=value1,dd=s:1;o:rum,key2=value2"
        //                    ^
        private const char TraceStateDatadogPairsSeparator = ';';

        // the separator used between the key and value in the pairs embedded inside the "dd" value
        // "key1=value1,dd=s:1;o:rum,key2=value2"
        //                  ^   ^
        private const char TraceStateDatadogKeyValueSeparator = ':';

        // the key used for the sampling priority in the key/value pairs embedded inside the "dd" value
        // "key1=value1,dd=s:1;o:rum,key2=value2"
        //                 ^
        private const string TraceStateSamplingPriorityKey = "s";

        // the key used for the origin in the key/value pairs embedded inside the "dd" value
        // "key1=value1,dd=s:1;o:rum,key2=value2"
        //                     ^
        private const string TraceStateOriginKey = "o";

        // the key used for the last seen parent Datadog span ID in the key/value pairs embedded inside the "dd" value
        // "key1=value1,dd=s:1;o:rum;p:0123456789abcdef,key2=value2"
        //                           ^
        private const string TraceStateLastParentKey = "p";

        // character bounds validation
        private const char LowerBound = '\u0020'; // decimal: 32, ' ' (space)
        private const char UpperBound = '\u007e'; // decimal: 126, '~' (tilde)
        private const char OutOfBoundsReplacement = '_';

        // zero value (16 zeroes) for when there isn't a last parent (`p`)
        // this value indicates that the backend can make this span as the root span if necessary of a trace
        internal const string ZeroLastParent = "0000000000000000";

        private static readonly KeyValuePair<char, char>[] InjectOriginReplacements =
        {
            new(',', '_'),
            new(';', '_'),
            new('~', '_'),
            new('=', '~'), // note '=' is encoded as '~' when injecting
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

        private const string PropagatedTagPrefix = "t.";

        public static readonly W3CTraceContextPropagator Instance = new();

        private W3CTraceContextPropagator()
        {
        }

        [Flags]
        [EnumExtensions]
        internal enum TraceFlags : byte
        {
            None = 0,
            Sampled = 1,
        }

        public void Inject<TCarrier, TCarrierSetter>(SpanContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            TelemetryFactory.Metrics.RecordCountContextHeaderStyleInjected(MetricTags.ContextHeaderStyle.TraceContext);

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
            var samplingPriority = context.GetOrMakeSamplingDecision() ?? SamplingPriorityValues.Default;
            var sampled = SamplingPriorityValues.IsKeep(samplingPriority) ? "01" : "00";

#if NET6_0_OR_GREATER
            return string.Create(null, stackalloc char[128], $"00-{context.RawTraceId}-{context.RawSpanId}-{sampled}");
#else
            return $"00-{context.RawTraceId}-{context.RawSpanId}-{sampled}";
#endif
        }

        internal static string CreateTraceStateHeader(SpanContext context)
        {
            var sb = StringBuilderCache.Acquire(100);

            try
            {
                sb.Append("dd=");

                // sampling priority ("s:<value>")
                if (context.GetOrMakeSamplingDecision() is { } samplingPriority)
                {
                    sb.Append("s:").Append(SamplingPriorityValues.ToString(samplingPriority)).Append(TraceStateDatadogPairsSeparator);
                }

                // origin ("o:<value>")
                var origin = context.Origin;

                if (!string.IsNullOrWhiteSpace(origin))
                {
                    var replacedOrigin = ReplaceCharacters(origin!, LowerBound, UpperBound, OutOfBoundsReplacement, InjectOriginReplacements);
                    sb.Append("o:").Append(replacedOrigin).Append(TraceStateDatadogPairsSeparator);
                }

                // last parent ("p:<value>")
                var lastParent = HexString.ToHexString(context.SpanId, lowerCase: true);
                sb.Append("p:").Append(lastParent).Append(TraceStateDatadogPairsSeparator);

                // propagated tags ("t.<key>:<value>")
                var propagatedTags = context.PrepareTagsForPropagation();

                if (propagatedTags?.Count > 0)
                {
                    var traceTagAppender = new TraceTagAppender(sb);
                    propagatedTags.Enumerate(ref traceTagAppender);
                }

                if (sb.Length == 3)
                {
                    // remove "dd=" since we never appended anything
                    sb.Clear();
                }
                else if (sb[sb.Length - 1] == TraceStateDatadogPairsSeparator)
                {
                    // remove trailing ";"
                    sb.Length--;
                }

                var additionalState = context.AdditionalW3CTraceState;

                if (!string.IsNullOrWhiteSpace(additionalState))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(TraceStateHeaderValuesSeparator);
                    }

                    sb.Append(additionalState);
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

            TraceId traceId;
            ulong parentId;
            string rawTraceId;
            string rawSpanId;

#if NETCOREAPP
            var w3cTraceId = header.AsSpan(start: 3, length: 32);
            var w3cSpanId = header.AsSpan(start: 36, length: 16);

            if (!HexString.TryParseTraceId(w3cTraceId, out traceId) || traceId == TraceId.Zero)
            {
                return false;
            }

            if (!HexString.TryParseUInt64(w3cSpanId, out parentId) || parentId == 0)
            {
                return false;
            }

            rawTraceId = w3cTraceId.ToString();
            rawSpanId = w3cSpanId.ToString();
            bool sampled;

            if (HexString.TryParseByte(header.AsSpan(53, 2), out var traceFlags))
            {
                sampled = ((TraceFlags)traceFlags).HasFlagFast(TraceFlags.Sampled);
            }
            else
            {
                return false;
            }
#else
            rawTraceId = header.Substring(startIndex: 3, length: 32);
            rawSpanId = header.Substring(startIndex: 36, length: 16);

            if (!HexString.TryParseTraceId(rawTraceId, out traceId) || traceId == TraceId.Zero)
            {
                return false;
            }

            if (!HexString.TryParseUInt64(rawSpanId, out parentId) || parentId == 0)
            {
                return false;
            }

            bool sampled;

            if (HexString.TryParseByte(header.Substring(53, 2), out var traceFlags))
            {
                sampled = ((TraceFlags)traceFlags).HasFlagFast(TraceFlags.Sampled);
            }
            else
            {
                return false;
            }
#endif

            traceParent = new W3CTraceParent(
                traceId: traceId,
                parentId: parentId,
                sampled: sampled,
                rawTraceId: rawTraceId,
                rawParentId: rawSpanId);

            TelemetryFactory.Metrics.RecordCountContextHeaderStyleExtracted(MetricTags.ContextHeaderStyle.TraceContext);
            return true;
        }

        internal static W3CTraceState ParseTraceState(string? header)
        {
            // header format: "[*,]dd=s:1;o:rum;t.dm:-4;t.usr.id:12345[,*]"
            if (string.IsNullOrWhiteSpace(header))
            {
                return new W3CTraceState(samplingPriority: null, origin: null, lastParent: ZeroLastParent, propagatedTags: null, additionalValues: null);
            }

            SplitTraceStateValues(header!, out var ddValues, out var additionalValues);

            if (ddValues is null or { Length: < 6 })
            {
                // "dd" section not found or it is too short
                // shortest valid length is 6 as in "dd=a:b"
                // note for this case the p will be viewed as 0 if added as a span tag
                return new W3CTraceState(samplingPriority: null, origin: null, lastParent: ZeroLastParent, propagatedTags: null, additionalValues);
            }

            int? samplingPriority = null;
            string? origin = null;
            string? lastParent = null;
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
                    var endIndex = ddValues.IndexOf(TraceStateDatadogPairsSeparator, startIndex);

                    if (endIndex < 0)
                    {
                        // no more semicolons left,
                        // this key/value pair goes on to the end of ddValues
                        endIndex = ddValues.Length;
                    }

                    var colonIndex = ddValues.IndexOf(TraceStateDatadogKeyValueSeparator, startIndex, endIndex - startIndex);

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

                    if (name.Equals(TraceStateSamplingPriorityKey, StringComparison.Ordinal))
                    {
                        // SamplingPriorityToInt32(ReadOnlySpan<char>)
                        samplingPriority = SamplingPriorityToInt32(value);
                    }
                    else if (name.Equals(TraceStateOriginKey, StringComparison.Ordinal))
                    {
                        origin = value.ToString();
                    }
                    else if (name.Equals(TraceStateLastParentKey, StringComparison.Ordinal))
                    {
                        lastParent = value.ToString();
                    }
                    else if (name.StartsWith(PropagatedTagPrefix, StringComparison.Ordinal))
                    {
                        value = ReplaceCharacters(value, LowerBound, UpperBound, OutOfBoundsReplacement, ExtractPropagatedTagValueReplacements);

                        propagatedTagsBuilder.Append(TagPropagation.PropagatedTagPrefix)
                                             .Append(name[2..]) // tag name without "t." prefix
                                             .Append(TagPropagation.KeyValueSeparator)
                                             .Append(value)
                                             .Append(TagPropagation.TagPairSeparator);
                    }
#else
                    var name = ddValues.Substring(startIndex: startIndex, length: colonIndex - startIndex);
                    var value = ddValues.Substring(startIndex: colonIndex + 1, length: endIndex - colonIndex - 1);

                    if (name == TraceStateSamplingPriorityKey)
                    {
                        // SamplingPriorityToInt32(string)
                        samplingPriority = SamplingPriorityToInt32(value);
                    }
                    else if (name == TraceStateOriginKey)
                    {
                        origin = value;
                    }
                    else if (name == TraceStateLastParentKey)
                    {
                        lastParent = value;
                    }
                    else if (name.StartsWith(PropagatedTagPrefix, StringComparison.Ordinal))
                    {
                        value = ReplaceCharacters(value, LowerBound, UpperBound, OutOfBoundsReplacement, ExtractPropagatedTagValueReplacements);

                        propagatedTagsBuilder.Append(TagPropagation.PropagatedTagPrefix)
                                             .Append(name.Substring(2)) // tag name without "t." prefix
                                             .Append(TagPropagation.KeyValueSeparator)
                                             .Append(value)
                                             .Append(TagPropagation.TagPairSeparator);
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
                    if (propagatedTagsBuilder[propagatedTagsBuilder.Length - 1] == TagPropagation.TagPairSeparator)
                    {
                        propagatedTagsBuilder.Length--;
                    }

                    propagatedTags = propagatedTagsBuilder.ToString();
                }
                else
                {
                    propagatedTags = null;
                }

                lastParent ??= ZeroLastParent;

                return new W3CTraceState(samplingPriority, origin, lastParent, propagatedTags, additionalValues);
            }
            finally
            {
                StringBuilderCache.Release(propagatedTagsBuilder);
            }
        }

        internal static void SplitTraceStateValues(string header, out string? ddValues, out string? additionalValues)
        {
            // header format: "[*,]dd=s:1;o:rum;t.dm:-4;t.usr.id:12345[,*]"

            if (string.IsNullOrWhiteSpace(header))
            {
                ddValues = null;
                additionalValues = null;
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
                    // if ",dd=" was found, skip the ','
                    ddStartIndex++;
                }
            }

            if (ddStartIndex < 0)
            {
                // "dd=" was not found in header, the entire header is "additional values"
                // example tracestate: "foo=bar"
                //                      ^^^^^^^
                ddValues = null;
                additionalValues = header;
                return;
            }

            // search for end of "dd="
            var ddEndIndex = header.IndexOf(TraceStateHeaderValuesSeparator, ddStartIndex + 3);

            if (ddEndIndex < 0)
            {
                // "dd=" reaches the end of header
                ddEndIndex = header.Length;
            }

            ddValues = header.Substring(ddStartIndex, ddEndIndex - ddStartIndex);

            if (ddStartIndex == 0 && ddEndIndex == header.Length)
            {
                // "dd" was the only key, no additional values
                // example tracestate: "dd=s:1;o:rum"
                additionalValues = null;
            }
            else if (ddStartIndex == 0)
            {
                // "dd" first, additional values later
                // example tracestate: "dd=s:1;o:rum,foo=bar"
                //                                   ^^^^^^^
                additionalValues = header.Substring(ddEndIndex + 1, header.Length - ddEndIndex - 1);
            }
            else if (ddEndIndex == header.Length)
            {
                // additional values first, "dd" later
                // example tracestate: "foo=bar,dd=s:1;o:rum"
                //                      ^^^^^^^
                additionalValues = header.Substring(0, ddStartIndex - 1);
            }
            else
            {
                // additional values on both sides, "dd" in the middle
                // example tracestate: "foo1=bar1,dd=s:1;o:rum,foo2=bar2" => "foo1=bar1,foo2=bar2"
                //                      ^^^^^^^^^              ^^^^^^^^^
                var otherValuesLeft = header.Substring(0, ddStartIndex - 1);
                var otherValuesRight = header.Substring(ddEndIndex + 1, header.Length - ddEndIndex - 1);

                var sb = StringBuilderCache.Acquire(otherValuesLeft.Length + otherValuesRight.Length + 1);
                sb.Append(otherValuesLeft).Append(TraceStateHeaderValuesSeparator).Append(otherValuesRight);
                additionalValues = StringBuilderCache.GetStringAndRelease(sb);
            }
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

            // Consider both the traceparent sampled flag and the Datadog sampling priority value to determine the final sampling priority value.
            // If both values agree (both say sample or both say do not sample), use the Datadog sampling priority value
            // Otherwise, prefer the traceparent sampled flag. Set to 1 for sampled=true or 0 for sampled=false
            var samplingPriority = traceParent.Sampled switch
            {
                true when traceState.SamplingPriority is > 0 => traceState.SamplingPriority.Value,
                true => SamplingPriorityValues.AutoKeep,
                false when traceState.SamplingPriority is <= 0 => traceState.SamplingPriority.Value,
                false => SamplingPriorityValues.AutoReject,
            };

            var traceTags = TagPropagation.ParseHeader(traceState.PropagatedTags);

            if (traceParent.Sampled && traceState.SamplingPriority <= 0)
            {
                traceTags.SetTag(Tags.Propagated.DecisionMaker, "-0");
            }
            else if (!traceParent.Sampled && traceState.SamplingPriority > 0)
            {
                traceTags.RemoveTag(Tags.Propagated.DecisionMaker);
            }

            spanContext = new SpanContext(
                traceId: traceParent.TraceId,
                spanId: traceParent.ParentId,
                samplingPriority: samplingPriority,
                serviceName: null,
                origin: traceState.Origin,
                rawTraceId: traceParent.RawTraceId,
                rawSpanId: traceParent.RawParentId,
                isRemote: true);

            spanContext.PropagatedTags = traceTags;
            spanContext.AdditionalW3CTraceState = traceState.AdditionalValues;
            spanContext.LastParentId = traceState.LastParent;
            return true;
        }

        private static bool TryGetSingle(IEnumerable<string?> values, out string value)
        {
            // fast path for string[], List<string>, and others
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
            => values switch
               {
                   // fast path for single value
                   IReadOnlyList<string?> { Count: 1 } list => list[0]?.Trim() ?? string.Empty,
                   // fast path for null or empty collections
                   IReadOnlyCollection<string?> { Count: 0 } or null => string.Empty,
                   // fallback
                   _ => TrimAndJoinStringsRare(values),
               };

        private static string TrimAndJoinStringsRare(IEnumerable<string?> values)
        {
            static void AppendIfNotNullOrWhiteSpace(StringBuilder sb, string? value)
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
                        AppendIfNotNullOrWhiteSpace(sb, value);
                    }

                    break;

                case List<string?> list:
                    // uses List<T>'s struct enumerator
                    foreach (var value in list)
                    {
                        AppendIfNotNullOrWhiteSpace(sb, value);
                    }

                    break;

                default:
                    foreach (var value in values)
                    {
                        AppendIfNotNullOrWhiteSpace(sb, value);
                    }

                    break;
            }

            if (sb.Length == 0)
            {
                StringBuilderCache.GetStringAndRelease(sb);
                return string.Empty;
            }

            // remove trailing ","
            if (sb[sb.Length - 1] == TraceStateHeaderValuesSeparator)
            {
                sb.Length--;
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

        internal readonly struct TraceTagAppender : TraceTagCollection.ITagEnumerator
        {
            private readonly StringBuilder _sb;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal TraceTagAppender(StringBuilder sb)
            {
                _sb = sb;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Next(KeyValuePair<string, string> tag)
            {
                // do not propagate "t.tid" tag in W3C headers,
                // the full 128-bit trace id is propagated in the traceparent header
                if (tag.Key.StartsWith(TagPropagation.PropagatedTagPrefix, StringComparison.Ordinal) &&
                    !tag.Key.Equals(Tags.Propagated.TraceIdUpper, StringComparison.Ordinal))
                {
#if NETCOREAPP
                    var key = tag.Key.AsSpan(start: 6);
#else
                    var key = tag.Key.Substring(startIndex: 6);
#endif

                    var tagKey = ReplaceCharacters(key, LowerBound, UpperBound, OutOfBoundsReplacement, InjectPropagatedTagKeyReplacements);
                    var tagValue = ReplaceCharacters(tag.Value, LowerBound, UpperBound, OutOfBoundsReplacement, InjectPropagatedTagValueReplacements);
                    _sb.Append(PropagatedTagPrefix).Append(tagKey).Append(TraceStateDatadogKeyValueSeparator).Append(tagValue).Append(TraceStateDatadogPairsSeparator);
                }
            }
        }
    }
}
