// <copyright file="W3CBaggagePropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Text;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace.Propagators;

// https://www.w3.org/TR/baggage/
internal class W3CBaggagePropagator : IContextInjector, IContextExtractor
{
    private const string BaggageHeaderName = "baggage";

    private const int DefaultMaximumBaggageItems = 64;

    private const int DefaultMaximumBaggageBytes = 8192;

    // the standard W3C separator between key/value pairs
    // "key1=value1,key2=value2"
    //             ^
    private const char KeyValuePairSeparator = ',';

    // the standard W3C separator between key and value in each pair
    // "key1=value1,key2=value2"
    //      ^           ^
    private const char KeyAndValueSeparator = ',';

    public static readonly W3CBaggagePropagator Instance = new();

    private W3CBaggagePropagator()
    {
    }

    public void Inject<TCarrier, TCarrierSetter>(
        PropagationContext context,
        TCarrier carrier,
        TCarrierSetter carrierSetter)
        where TCarrierSetter : struct, ICarrierSetter<TCarrier>
    {
        var baggage = context.Baggage;

        if (baggage?.Items is null or { IsEmpty: true })
        {
            // nothing to inject
            return;
        }

        var settings = context.SpanContext?.TraceContext?.Tracer.Settings;
        var baggageMaximumItems = settings?.BaggageMaximumItems ?? DefaultMaximumBaggageItems;
        var baggageMaximumBytes = settings?.BaggageMaximumBytes ?? DefaultMaximumBaggageBytes;

        var headerValue = CreateBaggageHeader(baggage, baggageMaximumItems, baggageMaximumBytes);

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
        var baggage = new Baggage();
        var baggageHeaders = ParseUtility.ParseString(carrier, carrierGetter, BaggageHeaderName);

        if (string.IsNullOrWhiteSpace(baggageHeaders))
        {
            return false;
        }

        foreach (var pair in baggageHeaders!.SplitIntoSpans(KeyValuePairSeparator))
        {
            var span = pair.AsSpan();

            // only the first `=` character is considered the separator.
            // if there are multiple `=` characters, the rest are part of the value.
            var separatorPosition = span.IndexOf(KeyAndValueSeparator);
            var key = span.Slice(0, separatorPosition).ToString();
            var value = span.Slice(separatorPosition + 1).ToString();

            baggage.Set(key, value);
        }

        context = new PropagationContext(spanContext: null, baggage);
        TelemetryFactory.Metrics.RecordCountContextHeaderStyleExtracted(MetricTags.ContextHeaderStyle.Baggage);
        return true;
    }

    internal static string CreateBaggageHeader(
        Baggage baggage,
        int maxBaggageItems,
        int maxBaggageLength)
    {
        if (baggage?.Items is null or { IsEmpty: true })
        {
            return string.Empty;
        }

        var sb = StringBuilderCache.Acquire();

        try
        {
            var itemCount = 0;
            var totalLength = 0;

            foreach (var item in baggage.Items)
            {
                itemCount++;

                if (itemCount > maxBaggageItems)
                {
                    // reached the item count limit, stop adding items
                    break;
                }

                // TODO: percent encoding
                var keyValuePairString = sb.Length > 0 ?
                    $"{KeyValuePairSeparator}{item.Key}{KeyAndValueSeparator}{item.Value}" :
                    $"{item.Key}{KeyAndValueSeparator}{item.Value}";

                totalLength += Encoding.UTF8.GetByteCount(keyValuePairString);

                if (totalLength > maxBaggageLength)
                {
                    // reached the byte count limit, stop adding items
                    break;
                }

                sb.Append(keyValuePairString);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }
        finally
        {
            StringBuilderCache.Release(sb);
        }
    }
}
