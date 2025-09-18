// <copyright file="MetricState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.Logging;

namespace Datadog.Trace.OTelMetrics
{
    /// <summary>
    /// Represents the state for a metric instrument, used to avoid ConcurrentDictionary contention
    /// </summary>
    internal class MetricState(MetricStreamIdentity identity, MetricPoint metricPoint)
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MetricState));
        private readonly MetricStreamIdentity _identity = identity;
        private readonly MetricPoint _metricPoint = metricPoint;
        private readonly object _lock = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordMeasurementLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            // RFC requirement: Validate negative values for Counter and Histogram
            if ((_identity.InstrumentType == InstrumentType.Counter || _identity.InstrumentType == InstrumentType.Histogram) && value < 0)
            {
                Log.Warning("Ignoring negative value {Value} for {InstrumentType} instrument: {InstrumentName}. API usage is incorrect.", value, _identity.InstrumentType, _identity.InstrumentName);
                return;
            }

            lock (_lock)
            {
                // Capture tags from the first measurement (for testing purposes)
                if (_metricPoint.Tags.Count == 0 && tags.Length > 0)
                {
                    foreach (var tag in tags)
                    {
                        _metricPoint.Tags[tag.Key] = tag.Value;
                    }
                }

                switch (_identity.InstrumentType)
                {
                    case InstrumentType.Counter:
                    case InstrumentType.ObservableCounter:
                    case InstrumentType.UpDownCounter:
                    case InstrumentType.ObservableUpDownCounter:
                        _metricPoint.UpdateCounter(value);
                        break;
                    case InstrumentType.Gauge:
                    case InstrumentType.ObservableGauge:
                        _metricPoint.UpdateGauge(value);
                        break;
                    case InstrumentType.Histogram:
                        _metricPoint.UpdateHistogram(value);
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordMeasurementDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            // RFC requirement: Validate negative values for Counter and Histogram
            if ((_identity.InstrumentType == InstrumentType.Counter || _identity.InstrumentType == InstrumentType.Histogram) && value < 0)
            {
                Log.Warning("Ignoring negative value {Value} for {InstrumentType} instrument: {InstrumentName}. API usage is incorrect.", value, _identity.InstrumentType, _identity.InstrumentName);
                return;
            }

            lock (_lock)
            {
                // Capture tags from the first measurement (for testing purposes)
                if (_metricPoint.Tags.Count == 0 && tags.Length > 0)
                {
                    foreach (var tag in tags)
                    {
                        _metricPoint.Tags[tag.Key] = tag.Value;
                    }
                }

                switch (_identity.InstrumentType)
                {
                    case InstrumentType.Counter:
                    case InstrumentType.ObservableCounter:
                    case InstrumentType.UpDownCounter:
                    case InstrumentType.ObservableUpDownCounter:
                        _metricPoint.UpdateCounter(value);
                        break;
                    case InstrumentType.Gauge:
                    case InstrumentType.ObservableGauge:
                        _metricPoint.UpdateGauge(value);
                        break;
                    case InstrumentType.Histogram:
                        _metricPoint.UpdateHistogram(value);
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordMeasurementGaugeLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            lock (_lock)
            {
                _metricPoint.UpdateGauge(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordMeasurementGaugeDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            lock (_lock)
            {
                _metricPoint.UpdateGauge(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordMeasurementHistogramLong(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            lock (_lock)
            {
                _metricPoint.UpdateHistogram(value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RecordMeasurementHistogramDouble(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            lock (_lock)
            {
                _metricPoint.UpdateHistogram(value);
            }
        }

        public MetricPoint GetMetricPoint()
        {
            lock (_lock)
            {
                return _metricPoint;
            }
        }

        public MetricStreamIdentity GetIdentity() => _identity;
    }
}
#endif
