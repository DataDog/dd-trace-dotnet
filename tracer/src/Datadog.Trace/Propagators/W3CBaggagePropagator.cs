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

        GetSettings(context, out var maximumItems, out var maximumBytes);
        var headerValue = CreateHeader(baggage, maximumItems, maximumBytes);

        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            carrierSetter.Set(carrier, BaggageHeaderName, headerValue);
            TelemetryFactory.Metrics.RecordCountContextHeaderStyleInjected(MetricTags.ContextHeaderStyle.Baggage);
        }
    }

    private static void GetSettings(PropagationContext context, out int maximumItems, out int maximumBytes)
    {
        if (context.SpanContext?.TraceContext?.Tracer.Settings is { } settings)
        {
            maximumItems = settings.BaggageMaximumItems;
            maximumBytes = settings.BaggageMaximumBytes;
        }
        else
        {
            maximumItems = DefaultMaximumBaggageItems;
            maximumBytes = DefaultMaximumBaggageBytes;
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

    internal static string Encode(string value, HashSet<char> charsToEncode)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        var sb = StringBuilderCache.Acquire();

        foreach (var b in bytes)
        {
            if (b < 0x20 || b > 0x7E || char.IsWhiteSpace((char)b) || charsToEncode.Contains((char)b))
            {
                // encode byte as '%XX'
                sb.Append($"%{b:X2}");
            }
            else
            {
                sb.Append((char)b);
            }
        }

        return StringBuilderCache.GetStringAndRelease(sb);
    }

    internal static string Decode(string value)
    {
        return Decode(value.AsSpan());
    }

    internal static string Decode(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        return Uri.UnescapeDataString(value.ToString());
    }

    internal static string CreateHeader(Baggage baggage, int maxBaggageItems, int maxBaggageLength)
    {
        var sb = StringBuilderCache.Acquire();

        try
        {
            var headerBuilder = new W3CBaggageHeaderBuilder(maxBaggageItems, maxBaggageLength, sb);
            baggage.ForEach(headerBuilder);

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

    private struct W3CBaggageHeaderBuilder : ICancellableObserver<KeyValuePair<string, string>>
    {
        private readonly int _maxBaggageItems;
        private readonly int _maxBaggageLength;
        private readonly StringBuilder _sb;

        private int _itemCount;
        private int _totalLength;

        public W3CBaggageHeaderBuilder(int maxBaggageItems, int maxBaggageLength, StringBuilder sb)
        {
            _maxBaggageItems = maxBaggageItems;
            _maxBaggageLength = maxBaggageLength;
            _sb = sb;
        }

        public bool CancellationRequested { get; private set; }

        public void OnNext(KeyValuePair<string, string> item)
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
                return;
            }

            var key = Encode(item.Key, KeyCharsToEncode);
            var value = Encode(item.Value, ValueCharsToEncode);

            var keyValuePairString = _sb.Length > 0 ?
                $"{PairSeparator}{key}{KeyAndValueSeparator}{value}" :
                $"{key}{KeyAndValueSeparator}{value}";

            _totalLength += Encoding.UTF8.GetByteCount(keyValuePairString);

            if (_totalLength > _maxBaggageLength)
            {
                // reached the byte count limit, stop adding items
                CancellationRequested = true;
                return;
            }

            _sb.Append(keyValuePairString);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
