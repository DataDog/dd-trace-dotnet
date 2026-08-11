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
    internal sealed class W3CTraceContextPropagator : IContextInjector, IContextExtractor
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

        public PropagatorType PropagatorType => PropagatorType.TraceContext;

        public string DisplayName => "tracecontext";

        public void Inject<TCarrier, TCarrierSetter>(PropagationContext context, TCarrier carrier, TCarrierSetter carrierSetter)
            where TCarrierSetter : struct, ICarrierSetter<TCarrier>
        {
            if (context.SpanContext is not { } spanContext)
            {
                // nothing to inject
                return;
            }

            TelemetryFactory.Metrics.RecordCountContextHeaderStyleInjected(MetricTags.ContextHeaderStyle.TraceContext);

            var traceparent = CreateTraceParentHeader(spanContext);
            carrierSetter.Set(carrier, TraceParentHeaderName, traceparent);

            var tracestate = CreateTraceStateHeader(spanContext);

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
            var sb = StringBuilderCache.Acquire();

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

                // OTel consistent-probability-sampling sub-keys ("ot=rv:...;th:..."), placed
                // immediately after "dd=" so both survive right-side truncation of a crowded
                // tracestate (W3C permits dropping members past 32).
                var otelTraceState = context.OtelTraceState;

                if (!string.IsNullOrWhiteSpace(otelTraceState))
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(TraceStateHeaderValuesSeparator);
                    }

                    sb.Append("ot=").Append(otelTraceState);
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

                return StringBuilderCache.GetStringAndRelease(sb);
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
                return new W3CTraceState(samplingPriority: null, origin: null, lastParent: ZeroLastParent, propagatedTags: null, additionalValues: null, otTraceState: null);
            }

            SplitTraceStateValues(
                header!.AsSpan().Trim(),
                out var ddValues,
                out _,
                out var otTraceState,
                out var hasOtTraceState,
                out var firstAdditionalMembers,
                out var secondAdditionalMembers,
                out var thirdAdditionalMembers);
            var additionalValues = GetAdditionalValues(firstAdditionalMembers, secondAdditionalMembers, thirdAdditionalMembers);

            return ParseDdMember(ddValues, additionalValues, hasOtTraceState ? otTraceState.ToString() : null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static W3CTraceState ParseDdMember(ReadOnlySpan<char> ddValues, string? additionalValues, string? otTraceState)
        {
            if (ddValues.Length < 3)
            {
                // "dd" section not found or it is too short
                // shortest valid length is 3 as in "a:b" ("dd=" prefix already stripped)
                // note for this case the p will be viewed as 0 if added as a span tag
                return new W3CTraceState(samplingPriority: null, origin: null, lastParent: ZeroLastParent, propagatedTags: null, additionalValues, otTraceState);
            }

            int? samplingPriority = null;
            ReadOnlySpan<char> origin = default;
            ReadOnlySpan<char> lastParent = default;
            var propagatedTagsBuilder = StringBuilderCache.Acquire();

            try
            {
                foreach (var member in new SpanCharSplitter(ddValues, TraceStateDatadogPairsSeparator, count: int.MaxValue))
                {
                    if (!ExtractKeyValue(member.AsSpan(), out var name, out var value))
                    {
                        continue;
                    }

                    if (name.Equals(TraceStateSamplingPriorityKey.AsSpan(), StringComparison.Ordinal))
                    {
                        samplingPriority = SamplingPriorityToInt32(value);
                    }
                    else if (name.Equals(TraceStateOriginKey.AsSpan(), StringComparison.Ordinal))
                    {
                        origin = value;
                    }
                    else if (name.Equals(TraceStateLastParentKey.AsSpan(), StringComparison.Ordinal))
                    {
                        lastParent = value;
                    }
                    else if (name.StartsWith(PropagatedTagPrefix.AsSpan(), StringComparison.Ordinal))
                    {
                        value = ReplaceCharacters(value, LowerBound, UpperBound, OutOfBoundsReplacement, ExtractPropagatedTagValueReplacements);

                        propagatedTagsBuilder.Append(TagPropagation.PropagatedTagPrefix)
                                             .Append(name.Slice(2)) // tag name without "t." prefix
                                             .Append(TagPropagation.KeyValueSeparator)
                                             .Append(value)
                                             .Append(TagPropagation.TagPairSeparator);
                    }
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

                return new W3CTraceState(samplingPriority, origin.IsEmpty ? null : origin.ToString(), lastParent.IsEmpty ? ZeroLastParent : lastParent.ToString(), propagatedTags, additionalValues, otTraceState);
            }
            finally
            {
                StringBuilderCache.Release(propagatedTagsBuilder);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ExtractKeyValue(ReadOnlySpan<char> source, out ReadOnlySpan<char> name, out ReadOnlySpan<char> value)
        {
            var colonIndex = source.IndexOf(TraceStateDatadogKeyValueSeparator);

            if (colonIndex <= 0 || colonIndex >= source.Length - 1)
            {
                name = default;
                value = default;
                return false;
            }

            name = source.Slice(0, colonIndex);
            value = source.Slice(colonIndex + 1);
            return true;
        }

        private static void SplitTraceStateValues(
            ReadOnlySpan<char> header,
            out ReadOnlySpan<char> ddValues,
            out bool hasDdValues,
            out ReadOnlySpan<char> otValues,
            out bool hasOtValues,
            out ReadOnlySpan<char> firstAdditionalMembers,
            out ReadOnlySpan<char> secondAdditionalMembers,
            out ReadOnlySpan<char> thirdAdditionalMembers)
        {
            ExtractMember(header, "dd=", out ddValues, out var precedingMembers, out var succeedingMembers, out hasDdValues);
            ExtractMember(precedingMembers, "ot=", out otValues, out firstAdditionalMembers, out secondAdditionalMembers, out hasOtValues);

            if (hasOtValues)
            {
                thirdAdditionalMembers = succeedingMembers;
                return;
            }

            ExtractMember(succeedingMembers, "ot=", out otValues, out secondAdditionalMembers, out thirdAdditionalMembers, out hasOtValues);
            firstAdditionalMembers = precedingMembers;
        }

        private static void ExtractMember(
            ReadOnlySpan<char> header,
            string prefix,
            out ReadOnlySpan<char> value,
            out ReadOnlySpan<char> precedingMembers,
            out ReadOnlySpan<char> succeedingMembers,
            out bool found)
        {
            var startIndex = 0;

            while (startIndex < header.Length && !header.Slice(startIndex).StartsWith(prefix.AsSpan(), StringComparison.Ordinal))
            {
                var separatorIndex = header.Slice(startIndex).IndexOf(TraceStateHeaderValuesSeparator);

                if (separatorIndex < 0)
                {
                    value = default;
                    precedingMembers = header;
                    succeedingMembers = default;
                    found = false;
                    return;
                }

                startIndex += separatorIndex + 1;
            }

            if (startIndex == header.Length)
            {
                value = default;
                precedingMembers = header;
                succeedingMembers = default;
                found = false;
                return;
            }

            var endIndex = header.Slice(startIndex + prefix.Length).IndexOf(TraceStateHeaderValuesSeparator);
            endIndex = endIndex < 0 ? -1 : startIndex + prefix.Length + endIndex;
            endIndex = endIndex < 0 ? header.Length : endIndex;

            value = header.Slice(startIndex + prefix.Length, endIndex - startIndex - prefix.Length);
            found = true;

            precedingMembers = startIndex == 0 ? default : header.Slice(0, startIndex - 1);
            succeedingMembers = endIndex == header.Length ? default : header.Slice(endIndex + 1);
        }

        private static string? GetAdditionalValues(
            ReadOnlySpan<char> firstMembers,
            ReadOnlySpan<char> secondMembers,
            ReadOnlySpan<char> thirdMembers)
        {
            if (firstMembers.IsEmpty)
            {
                if (secondMembers.IsEmpty)
                {
                    return thirdMembers.IsEmpty ? null : thirdMembers.ToString();
                }

                return thirdMembers.IsEmpty ? secondMembers.ToString() : CombineMembers(secondMembers, thirdMembers);
            }

            if (secondMembers.IsEmpty)
            {
                return thirdMembers.IsEmpty ? firstMembers.ToString() : CombineMembers(firstMembers, thirdMembers);
            }

            if (thirdMembers.IsEmpty)
            {
                return CombineMembers(firstMembers, secondMembers);
            }

            var sb = StringBuilderCache.Acquire(firstMembers.Length + secondMembers.Length + thirdMembers.Length + 2);
            sb.Append(firstMembers)
              .Append(TraceStateHeaderValuesSeparator)
              .Append(secondMembers)
              .Append(TraceStateHeaderValuesSeparator)
              .Append(thirdMembers);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static string CombineMembers(ReadOnlySpan<char> firstMembers, ReadOnlySpan<char> secondMembers)
        {
            var sb = StringBuilderCache.Acquire(firstMembers.Length + secondMembers.Length + 1);
            sb.Append(firstMembers)
              .Append(TraceStateHeaderValuesSeparator)
              .Append(secondMembers);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static int? SamplingPriorityToInt32(ReadOnlySpan<char> samplingPriority)
        {
            return samplingPriority.Length switch
                   {
                       0 => null,
                       1 when samplingPriority[0] == '2' => 2,
                       1 when samplingPriority[0] == '1' => 1,
                       1 when samplingPriority[0] == '0' => 0,
                       2 when samplingPriority[0] == '-' && samplingPriority[1] == '1' => -1,
#if NETCOREAPP
                       _ => int.TryParse(samplingPriority, out var result) ? result : null,
#else
                       _ => int.TryParse(samplingPriority.ToString(), out var result) ? result : null,
#endif
                   };
        }

        public bool TryExtract<TCarrier, TCarrierGetter>(
            TCarrier carrier,
            TCarrierGetter carrierGetter,
            out PropagationContext context)
            where TCarrierGetter : struct, ICarrierGetter<TCarrier>
        {
            context = default;

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

            var spanContext = new SpanContext(
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
            spanContext.OtelTraceState = traceState.OtTraceState;
            spanContext.LastParentId = traceState.LastParent;

            context = new PropagationContext(spanContext, baggage: null);

            TelemetryFactory.Metrics.RecordCountContextHeaderStyleExtracted(MetricTags.ContextHeaderStyle.TraceContext);
            return true;
        }

        [TestingAndPrivateOnly]
        internal static bool TryGetSingle(IEnumerable<string?> values, out string value)
        {
            // null values is handled in TryGetSingleRare
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

        [TestingAndPrivateOnly]
        internal static bool TryGetSingleRare(IEnumerable<string?> values, out string value)
        {
            if (values is null)
            {
                value = string.Empty;
                return false;
            }

            using var enumerator = values.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                // there were no items
                value = string.Empty;
                return false;
            }

            // store first value
            value = enumerator.Current ?? string.Empty;

            // is there a second value?
            if (enumerator.MoveNext())
            {
                value = string.Empty;
                return false; // more than one value
            }

            return true;
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

            var sb = StringBuilderCache.Acquire();

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

        public static bool NeedsCharacterReplacement(string value, char lowerBound, char upperBound, KeyValuePair<char, char>[] replacements)
            => NeedsCharacterReplacement(value.AsSpan(), lowerBound, upperBound, replacements);

        public static string ReplaceCharacters(string value, char lowerBound, char upperBound, char outOfBoundsReplacement, KeyValuePair<char, char>[] replacements)
            => ReplaceCharacters(value.AsSpan(), lowerBound, upperBound, outOfBoundsReplacement, replacements).ToString();

        private static bool NeedsCharacterReplacement(ReadOnlySpan<char> value, char lowerBound, char upperBound, KeyValuePair<char, char>[] replacements)
        {
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];

                if (character < lowerBound || character > upperBound)
                {
                    return true;
                }

                foreach (var pair in replacements)
                {
                    if (character == pair.Key)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static ReadOnlySpan<char> ReplaceCharacters(ReadOnlySpan<char> value, char lowerBound, char upperBound, char outOfBoundsReplacement, KeyValuePair<char, char>[] replacements)
        {
            if (!NeedsCharacterReplacement(value, lowerBound, upperBound, replacements))
            {
                return value;
            }

            var sb = StringBuilderCache.Acquire(value.Length);
            sb.Append(value);

            for (var index = 0; index < sb.Length; index++)
            {
                if (value[index] < lowerBound || value[index] > upperBound)
                {
                    sb[index] = outOfBoundsReplacement;
                }
            }

            foreach (var replacement in replacements)
            {
                sb.Replace(replacement.Key, replacement.Value);
            }

            return StringBuilderCache.GetStringAndRelease(sb).AsSpan();
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
                    var key = tag.Key.AsSpan(6);
                    var tagKey = ReplaceCharacters(key, LowerBound, UpperBound, OutOfBoundsReplacement, InjectPropagatedTagKeyReplacements);
                    var tagValue = ReplaceCharacters(tag.Value.AsSpan(), LowerBound, UpperBound, OutOfBoundsReplacement, InjectPropagatedTagValueReplacements);
                    _sb.Append(PropagatedTagPrefix)
                       .Append(tagKey)
                       .Append(TraceStateDatadogKeyValueSeparator)
                       .Append(tagValue)
                       .Append(TraceStateDatadogPairsSeparator);
                }
            }
        }
    }
}
