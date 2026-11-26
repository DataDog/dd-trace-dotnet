// <copyright file="MetricStreamIdentity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Linq;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.OpenTelemetry.Metrics
{
    /// <summary>
    /// Represents the identity of a metric stream, used for uniqueness and deduplication
    /// </summary>
    internal readonly struct MetricStreamIdentity : IEquatable<MetricStreamIdentity>
    {
        // MeterTags values as exported (OtlpMetricsSerializer.FormatAttributeValue), computed once in the constructor.
        private readonly string[] _meterTagValues;

        public MetricStreamIdentity(Instrument instrument, InstrumentType instrumentType)
        {
            InstrumentName = instrument.Name;
            MeterName = instrument.Meter.Name;
            MeterVersion = instrument.Meter.Version ?? string.Empty;

            // Duck typing works at runtime - checks if Tags property exists (.NET 8+)
            var meterDuck = instrument.Meter.DuckAs<IMeterDuck>();
            MeterTags = meterDuck?.Tags?.ToArray() ?? [];
            _meterTagValues = new string[MeterTags.Length];
            for (var i = 0; i < MeterTags.Length; i++)
            {
                _meterTagValues[i] = OtlpMetricsSerializer.FormatAttributeValue(MeterTags[i].Value);
            }

            MeterSchemaUrl = instrument.Meter.DuckAs<IMeterSchemaUrlDuck>()?.TelemetrySchemaUrl ?? string.Empty;

            Unit = instrument.Unit ?? string.Empty;
            Description = instrument.Description ?? string.Empty;
            InstrumentType = instrumentType;
            IsHistogram = instrumentType == InstrumentType.Histogram;

            Type? instrumentGenericType = instrument.GetType().IsGenericType ? instrument.GetType().GetGenericArguments()[0] : null;
            IsLongType = instrumentGenericType == typeof(long) ||
                         instrumentGenericType == typeof(int) ||
                         instrumentGenericType == typeof(short) ||
                         instrumentGenericType == typeof(byte);

            // Includes the meter version, tags and schema URL, so streams from different meters don't collide.
            // Only the instrument name is case-insensitive (OTel spec, "Name conflict"), so only it is lowercased;
            // stream names are compared ordinally.
            MetricStreamName = $"{MeterName}.{MeterVersion}.{OtlpMetricsSerializer.JoinMeterTags(MeterTags)}.{MeterSchemaUrl}.{InstrumentName.ToLowerInvariant()}.{InstrumentType}.{Unit}.{Description}";
        }

        public string InstrumentName { get; }

        public string MeterName { get; }

        public string MeterVersion { get; }

        public KeyValuePair<string, object?>[] MeterTags { get; }

        public string MeterSchemaUrl { get; }

        public string Unit { get; }

        public string Description { get; }

        public InstrumentType InstrumentType { get; }

        public string MetricStreamName { get; }

        public bool IsHistogram { get; }

        public bool IsLongType { get; }

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
            if (!string.Equals(InstrumentName, other.InstrumentName, StringComparison.OrdinalIgnoreCase) ||
                MeterName != other.MeterName ||
                MeterVersion != other.MeterVersion ||
                !MeterTagsEqual(in other) ||
                MeterSchemaUrl != other.MeterSchemaUrl ||
                Unit != other.Unit ||
                Description != other.Description ||
                InstrumentType != other.InstrumentType ||
                IsLongType != other.IsLongType)
            {
                return false;
            }

            return true;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            return obj is MetricStreamIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(InstrumentName, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(MeterName);
            hashCode.Add(MeterVersion);
            hashCode.Add(MeterSchemaUrl);
            hashCode.Add(Unit);
            hashCode.Add(Description);
            hashCode.Add(InstrumentType);
            hashCode.Add(IsLongType);

            for (int i = 0; i < MeterTags.Length; i++)
            {
                hashCode.Add(MeterTags[i].Key);
                hashCode.Add(_meterTagValues[i]);
            }

            return hashCode.ToHashCode();
        }

        // Meters are identical when all their parameters are equal (OTel metrics API spec, "Meter"). Tag values are
        // compared by the text they are exported as (OtlpMetricsSerializer.FormatAttributeValue), not by the spec's
        // typed equality: this exporter writes every attribute as a string, so meters exported as the same scope are
        // made the same stream instead of two streams with overlapping points in one OTLP metric. This deliberately
        // also merges values the spec types apart, such as 1 and "1", or null and "".
        private bool MeterTagsEqual(in MetricStreamIdentity other)
        {
            if (MeterTags.Length != other.MeterTags.Length)
            {
                return false;
            }

            for (var i = 0; i < MeterTags.Length; i++)
            {
                if (!string.Equals(MeterTags[i].Key, other.MeterTags[i].Key, StringComparison.Ordinal)
                    || !string.Equals(_meterTagValues[i], other._meterTagValues[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
