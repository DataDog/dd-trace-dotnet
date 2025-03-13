// <copyright file="MeterListenerHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.OTelMetrics.DuckTypes;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.OTelMetrics
{
    internal static class MeterListenerHandler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MeterListenerHandler));

        private static readonly HashSet<string> EnabledMeters = new();

        private static readonly IDogStatsd _dogstatsd = TracerManagerFactory.CreateDogStatsdClient(Tracer.Instance.Settings, TracerManager.Instance.DefaultServiceName, constantTags: null);

        static MeterListenerHandler()
        {
            foreach (string meterName in Tracer.Instance.Settings.EnabledMeters)
            {
                EnabledMeters.Add(meterName);
            }
        }

        public static void OnInstrumentPublishedProxy<TInstrument, TMeterListener>(TInstrument instrument, TMeterListener listener)
            where TInstrument : IInstrument
            where TMeterListener : IMeterListener
        {
            var meterName = instrument.Meter.Name;
            if (meterName is not null && EnabledMeters.Contains(meterName))
            {
                listener.EnableMeasurementEvents(instrument, state: null);
            }
            else
            {
                Log.Warning("MeterListenerHandler: The instrument will not be handled by the InstrumentPublished event. [Meter={MeterName}] [Instrument={InstrumentName}]", meterName, instrument.Name);
            }
        }

        public static void OnInstrumentPublished(System.Diagnostics.Metrics.Instrument instrument, System.Diagnostics.Metrics.MeterListener listener)
        {
            var meterName = instrument.Meter.Name;
            if (meterName is not null && EnabledMeters.Contains(meterName))
            {
                listener.EnableMeasurementEvents(instrument, state: null);
            }
            else
            {
                Log.Warning("MeterListenerHandler: The instrument will not be handled by the InstrumentPublished event. [Meter={MeterName}] [Instrument={InstrumentName}]", meterName, instrument.Name);
            }
        }

        public static void OnMeasurementRecordedDouble(System.Diagnostics.Metrics.Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            Console.WriteLine($"{instrument.Name} recorded measurement {value} with tags {string.Join(",", tags.ToArray())}");
        }

        public static void OnMeasurementRecordedLong(System.Diagnostics.Metrics.Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            var tagsArray = new string[tags.Length];
            for (int i = 0; i < tags.Length; i++)
            {
                tagsArray[i] = tags[i].Key + ":" + tags[i].Value;
            }

            if (instrument is System.Diagnostics.Metrics.Counter<byte>
                || instrument is System.Diagnostics.Metrics.Counter<short>
                || instrument is System.Diagnostics.Metrics.Counter<int>
                || instrument is System.Diagnostics.Metrics.Counter<long>)
            {
                _dogstatsd.Counter(statName: instrument.Name + ".dd", value: value, tags: tagsArray);
            }
            else
            {
                Log.Warning("MeterListenerHandler: The instrument will not be handled by the MeasurementRecorded event. [Instrument={InstrumentName}]", instrument.Name);
            }

            // else if (instrument is System.Diagnostics.Metrics.Gauge<byte>
            //          || instrument is System.Diagnostics.Metrics.Measure<short>)
            // {
            //     _dogstatsd.Gauge(statName: instrument.Name, value: value, tags: tagsArray);
            // }

            Console.WriteLine($"{instrument.Name} recorded measurement {value} with tags {string.Join(",", tags.ToArray())}");
        }
    }
}
#endif
