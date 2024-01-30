// <copyright file="ConfigurationTelemetry.Collector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry
{
    // This is the "collector" implementation
    internal partial class ConfigurationTelemetry
    {
        private static readonly IReadOnlyDictionary<string, string> ConfigurationKeysMapping = new Dictionary<string, string>
        {
            ["DD_TRACE_ENABLED"] = "trace_enabled",
            ["DD_PROFILING_ENABLED"] = "profiling_enabled",
            ["DD_APPSEC_ENABLED"] = "appsec_enabled",
            ["DD_DATA_STREAMS_ENABLED"] = "data_streams_enabled",
            ["DD_TAGS"] = "trace_tags",
            ["DD_TRACE_HEADER_TAGS"] = "trace_header_tags",
            ["DD_LOGS_INJECTION"] = "logs_injection_enabled",
            ["DD_TRACE_SAMPLE_RATE"] = "trace_sample_rate"
        };

        private ConcurrentQueue<ConfigurationTelemetryEntry> _backBuffer = new();

        public bool HasChanges() => !_entries.IsEmpty || !_backBuffer.IsEmpty;

        /// <inheritdoc />
        public void CopyTo(IConfigurationTelemetry destination)
        {
            if (destination is ConfigurationTelemetry telemetry)
            {
                // don't dequeue from the original
                foreach (var entry in _entries)
                {
                    // We're assuming that these entries "overwrite" any existing entries
                    // However, as we don't know in which order they were created, we need
                    // to update the SeqId to make sure they're given precedence
                    entry.SeqId = Interlocked.Increment(ref _seqId);
                    telemetry._entries.Enqueue(entry);
                }
            }
        }

        /// <summary>
        /// Get the latest data to send to the intake.
        /// </summary>
        /// <returns>Null if there are no changes, or the collector is not yet initialized</returns>
        public ICollection<ConfigurationKeyValue>? GetData()
        {
            if (_entries.IsEmpty && _backBuffer.IsEmpty)
            {
                return null;
            }

            var data = new List<ConfigurationKeyValue>();

            // There's a small race condition in the telemetry collector, which means that
            // the _backBuffer MAY contain "left over" config from the previous flush
            // this ensures that we don't lose it completely
            while (_backBuffer.TryDequeue(out var entry))
            {
                data.Add(GetConfigKeyValue(entry));
            }

            Debug.Assert(_backBuffer.IsEmpty, "The back buffer should be empty because nothing should be writing to it");

            var config = Interlocked.Exchange(ref _entries, _backBuffer);
            _backBuffer = config;

            if (config.IsEmpty)
            {
                return data;
            }

            while (config.TryDequeue(out var entry))
            {
                data.Add(GetConfigKeyValue(entry));
            }

            return data;

            static ConfigurationKeyValue GetConfigKeyValue(ConfigurationTelemetryEntry entry)
            {
                if (!ConfigurationKeysMapping.TryGetValue(entry.Key, out var key))
                {
                    key = entry.Key;
                }

                return new ConfigurationKeyValue(
                    name: key,
                    origin: entry.Origin.ToStringFast(),
                    seqId: entry.SeqId,
                    error: entry.Error,
                    value: GetValue(entry));
            }

            static object? GetValue(ConfigurationTelemetryEntry entry)
            {
                return entry.Type switch
                {
                    ConfigurationTelemetry.ConfigurationTelemetryEntryType.Bool => entry.BoolValue,
                    ConfigurationTelemetry.ConfigurationTelemetryEntryType.Double => entry.DoubleValue,
                    ConfigurationTelemetry.ConfigurationTelemetryEntryType.Int => entry.IntValue,
                    ConfigurationTelemetry.ConfigurationTelemetryEntryType.Redacted => "<redacted>",
                    _ => entry.StringValue
                };
            }
        }

        public void Clear()
        {
            // clears any data stored in the buffers
            while (_backBuffer.TryDequeue(out _))
            {
            }

            var config = Interlocked.Exchange(ref _entries, _backBuffer);
            while (config.TryDequeue(out _))
            {
            }
        }
    }
}
