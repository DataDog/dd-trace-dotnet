// <copyright file="MetricStreamIdentity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Diagnostics.Metrics;

namespace Datadog.Trace.OTelMetrics
{
    /// <summary>
    /// Represents the identity of a metric stream, used for uniqueness and deduplication
    /// </summary>
    internal readonly struct MetricStreamIdentity : IEquatable<MetricStreamIdentity>
    {
        public MetricStreamIdentity(Instrument instrument, InstrumentType instrumentType)
        {
            InstrumentName = instrument.Name;
            MeterName = instrument.Meter.Name;
            Unit = instrument.Unit ?? string.Empty;
            Description = instrument.Description ?? string.Empty;
            InstrumentType = instrumentType;
            IsHistogram = instrumentType == InstrumentType.Histogram;

            // Create a unique stream name that includes all identifying characteristics
            MetricStreamName = $"{MeterName}.{InstrumentName}.{InstrumentType}.{Unit}.{Description}";
        }

        public string InstrumentName { get; }

        public string MeterName { get; }

        public string Unit { get; }

        public string Description { get; }

        public InstrumentType InstrumentType { get; }

        public string MetricStreamName { get; }

        public bool IsHistogram { get; }

        public static bool operator ==(MetricStreamIdentity left, MetricStreamIdentity right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MetricStreamIdentity left, MetricStreamIdentity right)
        {
            return !left.Equals(right);
        }

        public bool Equals(MetricStreamIdentity other)
        {
            return string.Equals(InstrumentName, other.InstrumentName, StringComparison.OrdinalIgnoreCase) &&
                   MeterName == other.MeterName &&
                   Unit == other.Unit &&
                   Description == other.Description &&
                   InstrumentType == other.InstrumentType;
        }

        public override bool Equals(object? obj)
        {
            return obj is MetricStreamIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(InstrumentName),
                MeterName,
                Unit,
                Description,
                InstrumentType);
        }
    }
}
#endif
