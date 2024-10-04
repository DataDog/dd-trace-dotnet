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
    public const string BaggageHeaderName = "baggage";

    public const int DefaultMaximumBaggageItems = 64;

    public const int DefaultMaximumBaggageBytes = 8192;

    // the standard W3C separator between top-level key/value pairs
    // "key1=value1,key2=value2"
    //             ^
    private const char TraceStateHeaderValuesSeparator = ',';

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

        if (baggage?.Items is null || baggage.Items.IsEmpty)
        {
            // nothing to inject
            return;
        }

        TelemetryFactory.Metrics.RecordCountContextHeaderStyleInjected(MetricTags.ContextHeaderStyle.Baggage);

        var settings = context.SpanContext?.TraceContext?.Tracer.Settings;
        var baggageMaximumItems = settings?.BaggageMaximumItems ?? DefaultMaximumBaggageItems;
        var baggageMaximumBytes = settings?.BaggageMaximumBytes ?? DefaultMaximumBaggageBytes;

        var headerValue = CreateBaggageHeader(baggage, baggageMaximumItems, baggageMaximumBytes);

        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            carrierSetter.Set(carrier, BaggageHeaderName, headerValue);
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

        foreach (var pair in baggageHeaders!.SplitIntoSpans(','))
        {
            var separatorPosition = pair.Source.IndexOf('=');
            var key = pair.Source.Substring(0, separatorPosition);
            var value = pair.Source.Substring(separatorPosition + 1);

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
        if (baggage?.Items is null || baggage.Items.IsEmpty)
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

                if (itemCount >= maxBaggageItems)
                {
                    // reached the item count limit, stop adding items
                    break;
                }

                // TODO: percent encoding
                var itemString = sb.Length > 0 ? $",{item.Key}={item.Value}" : $"{item.Key}={item.Value}";
                totalLength += Encoding.UTF8.GetByteCount(itemString);

                if (totalLength > maxBaggageLength)
                {
                    // reached the byte count limit, stop adding items
                    break;
                }

                sb.Append(itemString);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }
        finally
        {
            StringBuilderCache.Release(sb);
        }
    }
}
