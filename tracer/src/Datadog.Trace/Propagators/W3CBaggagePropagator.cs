// <copyright file="W3CBaggagePropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;

#nullable enable

namespace Datadog.Trace.Propagators;

// https://www.w3.org/TR/baggage/
internal class W3CBaggagePropagator : IContextInjector, IContextExtractor
{
    public const string BaggageHeaderName = "baggage";
    public const int DefaultMaximumBaggageItems = 64;
    public const int DefaultMaximumBaggageBytes = 8192;

    // the standard W3C separator between key/value pairs
    // "key1=value1,key2=value2"
    //             ^
    private const char PairSeparator = ',';

    // the standard W3C separator between the key and value in each pair
    // "key1=value1,key2=value2"
    //      ^           ^
    private const char KeyAndValueSeparator = '=';

    public static readonly W3CBaggagePropagator Instance = new();

    private static readonly HashSet<char> KeyCharsToEncode;

    private static readonly HashSet<char> ValueCharsToEncode;

    static W3CBaggagePropagator()
    {
        // key may not contain whitespace or any of the following characters: " , ; \ ( ) / : < = > ? @ [ ] { }
        KeyCharsToEncode = ['"', ',', ';', '\\', '(', ')', '/', ':', '<', '=', '>', '?', '@', '[', ']', '{', '}'];

        // value may contain characters from the Basic Latin Unicode Block, except for the following:
        //   U+0020 space ( )
        //   U+0022 double quotation mark (")
        //   U+002C comma (,)
        //   U+003B semicolon (;)
        //   U+005C backslash (\)
        ValueCharsToEncode = [' ', '"', ',', ';', '\\'];
    }

    private W3CBaggagePropagator()
    {
    }

    public PropagatorType PropagatorType => PropagatorType.Baggage;

    public void Inject<TCarrier, TCarrierSetter>(
        PropagationContext context,
        TCarrier carrier,
        TCarrierSetter carrierSetter)
        where TCarrierSetter : struct, ICarrierSetter<TCarrier>
    {
        var baggage = context.Baggage;

        if (baggage is null or { Count: 0 })
        {
            // nothing to inject
            return;
        }

        var settings = Tracer.Instance.Settings;
        var headerValue = CreateHeader(baggage, settings.BaggageMaximumItems, settings.BaggageMaximumBytes);

        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            carrierSetter.Set(carrier, BaggageHeaderName, headerValue);
            TelemetryFactory.Metrics.RecordCountContextHeaderStyleInjected(MetricTags.ContextHeaderStyle.Baggage);
        }
    }

    public bool TryExtract<TCarrier, TCarrierGetter>(
        TCarrier carrier,
        TCarrierGetter carrierGetter,
        out PropagationContext context)
        where TCarrierGetter : struct, ICarrierGetter<TCarrier>
    {
        context = default;
        var header = ParseUtility.ParseString(carrier, carrierGetter, BaggageHeaderName);

        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var baggage = ParseHeader(header!);
        context = new PropagationContext(spanContext: null, baggage);
        TelemetryFactory.Metrics.RecordCountContextHeaderStyleExtracted(MetricTags.ContextHeaderStyle.Baggage);

        return baggage is { Count: > 0 };
    }

    internal static void EncodeStringAndAppend(StringBuilder sb, string source, HashSet<char> charsToEncode)
    {
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        if (!AnyCharRequiresEncoding(source, charsToEncode))
        {
            // no bytes require encoding, append the source string directly
            sb.Append(source);
            return;
        }

        // this is an upper bound and will almost always be more bytes than we need
        var maxByteCount = Encoding.UTF8.GetMaxByteCount(source.Length);

#if NETCOREAPP3_1_OR_GREATER
        if (maxByteCount < 256)
        {
            // allocate a buffer on the stack for the UTF-8 bytes
            Span<byte> stackBuffer = stackalloc byte[maxByteCount];
            var byteCount = Encoding.UTF8.GetBytes(source, stackBuffer);

            // slice the buffer down to the actual bytes written
            var stackBytes = stackBuffer[..byteCount];
            EncodeBytesAndAppend(sb, stackBytes, charsToEncode);
            return;
        }
#endif

        // rent a buffer for the UTF-8 bytes
        var buffer = ArrayPool<byte>.Shared.Rent(minimumLength: maxByteCount);

        try
        {
            var byteCount = Encoding.UTF8.GetBytes(source, 0, source.Length, buffer, 0);

            // slice the buffer down to the actual bytes written
            var bytes = buffer.AsSpan(0, byteCount);
            EncodeBytesAndAppend(sb, bytes, charsToEncode);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void EncodeBytesAndAppend(StringBuilder sb, Span<byte> bytes, HashSet<char> charsToEncode)
    {
        // allocate a buffer on the stack (or rent one) for hexadecimal strings
#if NETCOREAPP3_1_OR_GREATER
        Span<char> hexStringBuffer = stackalloc char[2];
#else
        var buffer = ArrayPool<char>.Shared.Rent(minimumLength: 2);
        var hexStringBuffer = buffer.AsSpan(start: 0, length: 2);
#endif

        for (var index = 0; index < bytes.Length; index++)
        {
            var b = bytes[index];

            if (b < 0x20 || b > 0x7E || char.IsWhiteSpace((char)b) || charsToEncode.Contains((char)b))
            {
                // encode byte as "%FF" (hexadecimal)
                var byteToEncode = bytes.Slice(index, 1);
                HexString.ToHexChars(byteToEncode, hexStringBuffer, lowerCase: false);

                sb.Append('%').Append(hexStringBuffer);
            }
            else
            {
                // append the byte as a character
                sb.Append((char)b);
            }
        }

#if !NETCOREAPP3_1_OR_GREATER
        ArrayPool<char>.Shared.Return(buffer);
#endif
    }

    private static bool AnyCharRequiresEncoding(string source, HashSet<char> charsToEncode)
    {
        foreach (var c in source)
        {
            if (c < 0x20 || c > 0x7E || char.IsWhiteSpace(c) || charsToEncode.Contains(c))
            {
                return true;
            }
        }

        return false;
    }

    internal static string Decode(string value)
    {
        return Decode(value.AsSpan());
    }

    private static string Decode(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        // returns the same string (no allocations) if there are no escaped/encoded characters
        return Uri.UnescapeDataString(value.ToString());
    }

    internal static string CreateHeader(Baggage baggage, int maxBaggageItems, int maxBaggageLength)
    {
        var sb = StringBuilderCache.Acquire();

        try
        {
            var headerBuilder = new W3CBaggageHeaderBuilder(maxBaggageItems, maxBaggageLength, sb);
            baggage.Enumerate(headerBuilder);

            if (sb.Length == 0)
            {
                // no valid items were added
                return string.Empty;
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }
        finally
        {
            StringBuilderCache.Release(sb);
        }
    }

    internal static Baggage? ParseHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        Baggage? baggage = null;

        foreach (var pair in header.SplitIntoSpans(PairSeparator))
        {
            var span = pair.AsSpan();

            // only the first `=` character is considered the separator.
            // if there are multiple `=` characters, the rest are part of the value.
            var separatorPosition = span.IndexOf(KeyAndValueSeparator);

            if (separatorPosition <= 0 || separatorPosition == span.Length - 1)
            {
                // -1: invalid, no '=' character found, e.g. "foo"
                // 0: invalid, key is empty, e.g. "=value" or "="
                // span.Length - 1: invalid, value is empty, e.g. "key=" or "="
                continue;
            }

            var key = Decode(span.Slice(0, separatorPosition).Trim());

            if (key.Length == 0)
            {
                continue;
            }

            var value = Decode(span.Slice(separatorPosition + 1).Trim());

            if (value.Length == 0)
            {
                continue;
            }

            baggage ??= new Baggage();
            baggage[key] = value;
        }

        return baggage;
    }

    private struct W3CBaggageHeaderBuilder : ICancellableObserver<KeyValuePair<string, string?>>
    {
        private readonly int _maxBaggageItems;
        private readonly int _maxBaggageLength;
        private readonly StringBuilder _sb;

        private int _itemCount;

        public W3CBaggageHeaderBuilder(int maxBaggageItems, int maxBaggageLength, StringBuilder sb)
        {
            _maxBaggageItems = maxBaggageItems;
            _maxBaggageLength = maxBaggageLength;
            _sb = sb;
        }

        public bool CancellationRequested { get; private set; }

        public void OnNext(KeyValuePair<string, string?> item)
        {
            if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrEmpty(item.Value))
            {
                // skip invalid item
                return;
            }

            _itemCount++;

            if (_itemCount > _maxBaggageItems)
            {
                // reached the item count limit, stop adding items
                CancellationRequested = true;

                TelemetryFactory.Metrics.RecordCountContextHeaderTruncated(MetricTags.ContextHeaderTruncationReason.BaggageItemCountExceeded);
                return;
            }

            var currentLength = _sb.Length;

            if (currentLength > 0)
            {
                _sb.Append(PairSeparator);
            }

            EncodeStringAndAppend(_sb, item.Key, KeyCharsToEncode);
            _sb.Append(KeyAndValueSeparator);
            EncodeStringAndAppend(_sb, item.Value!, ValueCharsToEncode);

            // it's all ASCII here after encoding, so we can use the string
            // length directly instead of using Encoding.UTF8.GetByteCount().
            if (_sb.Length > _maxBaggageLength)
            {
                // reached the byte count limit, remove the pair we just added
                // by restoring the previous string length and stop adding more items
                _sb.Length = currentLength;
                CancellationRequested = true;

                TelemetryFactory.Metrics.RecordCountContextHeaderTruncated(MetricTags.ContextHeaderTruncationReason.BaggageByteCountExceeded);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
