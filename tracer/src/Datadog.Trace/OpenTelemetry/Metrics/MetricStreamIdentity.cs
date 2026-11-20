// <copyright file="MetricStreamIdentity.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
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
        public MetricStreamIdentity(Instrument instrument, InstrumentType instrumentType)
        {
            InstrumentName = instrument.Name;
            MeterName = instrument.Meter.Name;
            MeterVersion = instrument.Meter.Version ?? string.Empty;

            // Duck typing works at runtime - checks if Tags property exists (.NET 8+)
            var meterDuck = instrument.Meter.DuckAs<IMeterDuck>();
            MeterTags = meterDuck?.Tags?.ToArray() ?? [];
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
            MetricStreamName = $"{MeterName}.{MeterVersion}.{OtlpMetricsSerializer.JoinMeterTags(MeterTags)}.{MeterSchemaUrl}.{InstrumentName}.{InstrumentType}.{Unit}.{Description}";
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
                !TagsEqual(MeterTags, other.MeterTags) ||
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

        public override bool Equals(object? obj)
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
                hashCode.Add(MeterTags[i].Value);
            }

            return hashCode.ToHashCode();
        }

        // Compared by value, as OTel .NET does (MetricStreamIdentity and Tags): meters are identical when all
        // their parameters are equal (OTel metrics API spec, "Meter"), and each identity copies the tags into a new array.
        private static bool TagsEqual(KeyValuePair<string, object?>[] left, KeyValuePair<string, object?>[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (!string.Equals(left[i].Key, right[i].Key, StringComparison.Ordinal) || !Equals(left[i].Value, right[i].Value))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
