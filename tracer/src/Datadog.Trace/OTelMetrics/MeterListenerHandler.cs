// <copyright file="MeterListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OTelMetrics
{
    internal static class MeterListenerHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MeterListenerHandler));

        private static readonly ConcurrentDictionary<string, MetricData> CapturedMetrics = new();

        public static void OnInstrumentPublished(Instrument instrument, System.Diagnostics.Metrics.MeterListener listener)
        {
            bool shouldEnable;
            var meterName = instrument.Meter.Name;
            var enabledMeterNames = Tracer.Instance.Settings.OpenTelemetryMeterNames;

            if (enabledMeterNames.Length > 0)
            {
                shouldEnable = enabledMeterNames.Contains(meterName);
            }
            else
            {
                shouldEnable = !meterName.StartsWith("System.", StringComparison.Ordinal);
            }

            if (shouldEnable)
            {
                Log.Debug("Enabling measurement events for instrument: {InstrumentName} from meter: {MeterName}", instrument.Name, meterName);
                listener.EnableMeasurementEvents(instrument, state: null);
            }
            else
            {
                Log.Debug("Skipping instrument: {InstrumentName} from meter: {MeterName} (filtered out)", instrument.Name, meterName);
            }
        }

        public static void OnMeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            var instrumentType = instrument.GetType().FullName;
            if (instrumentType is null)
            {
                Log.Warning("Unable to get the full name of the instrument type for: {InstrumentName}", instrument.Name);
                return;
            }

            // Only handle Counter<long> for now
            if (!instrumentType.StartsWith("System.Diagnostics.Metrics.Counter`1"))
            {
                Log.Debug("Skipping non-counter instrument: {InstrumentName} of type: {InstrumentType}", instrument.Name, instrumentType);
                return;
            }

            var tagsDict = new Dictionary<string, object?>(tags.Length);
            var tagsArray = new string[tags.Length];
            for (int i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                tagsDict[tag.Key] = tag.Value;
                tagsArray[i] = tag.Key + "=" + tag.Value;
            }

            // Create metric data
            var metricData = new MetricData
            {
                InstrumentName = instrument.Name,
                MeterName = instrument.Meter.Name ?? "unknown",
                InstrumentType = "Counter",
                Value = value,
                Tags = tagsDict,
                Timestamp = DateTimeOffset.UtcNow
            };

            var key = $"{metricData.MeterName}.{metricData.InstrumentName}";
            CapturedMetrics.AddOrUpdate(key, metricData, (k, existing) => metricData);

            Log.Debug("Captured counter measurement: {InstrumentName} = {Value}", instrument.Name, value);

            var tagsString = string.Join(",", tagsArray);
            Console.WriteLine($"[METRICS_CAPTURE] {key}|{metricData.InstrumentType}|{value}|{tagsString}");
        }
    }
}
#endif
