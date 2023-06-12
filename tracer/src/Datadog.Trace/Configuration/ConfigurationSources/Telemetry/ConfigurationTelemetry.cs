// <copyright file="ConfigurationTelemetry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry;

internal partial class ConfigurationTelemetry : IConfigurationTelemetry
{
    private static long _seqId;
    private ConcurrentQueue<ConfigurationTelemetryEntry> _entries = new();

    public enum ConfigurationTelemetryEntryType
    {
        String,
        Redacted,
        Bool,
        Int,
        Double
    }

    public void Record(string key, string? value, bool recordValue, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
        => _entries.Enqueue(
            recordValue
                ? ConfigurationTelemetryEntry.String(key, value, origin, error)
                : ConfigurationTelemetryEntry.Redacted(key, origin, error));

    public void Record(string key, bool value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
        => _entries.Enqueue(ConfigurationTelemetryEntry.Bool(key, value, origin, error));

    public void Record(string key, double value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
        => _entries.Enqueue(ConfigurationTelemetryEntry.Number(key, value, origin, error));

    public void Record(string key, int value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
        => _entries.Enqueue(ConfigurationTelemetryEntry.Number(key, value, origin, error));

    public void Record(string key, double? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
    {
        if (value is { } v)
        {
            _entries.Enqueue(ConfigurationTelemetryEntry.Number(key, v, origin, error));
        }
        else
        {
            _entries.Enqueue(ConfigurationTelemetryEntry.String(key, null, origin, error));
        }
    }

    public void Record(string key, int? value, ConfigurationOrigins origin, TelemetryErrorCode? error = null)
    {
        if (value is { } v)
        {
            _entries.Enqueue(ConfigurationTelemetryEntry.Number(key, v, origin, error));
        }
        else
        {
            _entries.Enqueue(ConfigurationTelemetryEntry.String(key, null, origin, error));
        }
    }

    /// <summary>
    /// Gets the currently enqueued values. Should only be used for testing
    /// </summary>
    internal ConcurrentQueue<ConfigurationTelemetryEntry> GetQueueForTesting() => _entries;

    public class ConfigurationTelemetryEntry
    {
        // internal for testing
        private ConfigurationTelemetryEntry(string key, ConfigurationOrigins origin, ConfigurationTelemetryEntryType type, TelemetryErrorCode? error, string? stringValue = null, bool? boolValue = null, int? intValue = null, double? doubleValue = null)
        {
            Key = key;
            Origin = origin;
            Error = error;
            StringValue = stringValue;
            BoolValue = boolValue;
            IntValue = intValue;
            DoubleValue = doubleValue;
            Type = type;
            SeqId = Interlocked.Increment(ref _seqId);
        }

        public string Key { get; }

        public ConfigurationOrigins Origin { get; }

        public TelemetryErrorCode? Error { get; }

        public long SeqId { get; set; }

        public ConfigurationTelemetryEntryType Type { get; }

        public string? StringValue { get; }

        public bool? BoolValue { get; }

        public int? IntValue { get; }

        public double? DoubleValue { get; }

        public static ConfigurationTelemetryEntry String(string key, string? value, ConfigurationOrigins origin, TelemetryErrorCode? error)
            => new(key, origin, ConfigurationTelemetryEntryType.String, error, stringValue: value);

        public static ConfigurationTelemetryEntry Redacted(string key, ConfigurationOrigins origin, TelemetryErrorCode? error)
            => new(key, origin, ConfigurationTelemetryEntryType.Redacted, error, stringValue: null);

        public static ConfigurationTelemetryEntry Bool(string key, bool value, ConfigurationOrigins origin, TelemetryErrorCode? error)
            => new(key, origin, ConfigurationTelemetryEntryType.Bool, error, boolValue: value);

        public static ConfigurationTelemetryEntry Number(string key, int value, ConfigurationOrigins origin, TelemetryErrorCode? error)
            => new(key, origin, ConfigurationTelemetryEntryType.Int, error, intValue: value);

        public static ConfigurationTelemetryEntry Number(string key, double value, ConfigurationOrigins origin, TelemetryErrorCode? error)
            => new(key, origin, ConfigurationTelemetryEntryType.Double, error, doubleValue: value);
    }
}
