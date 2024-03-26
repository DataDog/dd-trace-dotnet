// <copyright file="ConfigurationTelemetry.Collector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry
{
    // This is the "collector" implementation
    internal partial class ConfigurationTelemetry
    {
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
            GetData(_backBuffer, data);

            Debug.Assert(_backBuffer.IsEmpty, "The back buffer should be empty because nothing should be writing to it");

            var config = Interlocked.Exchange(ref _entries, _backBuffer);
            _backBuffer = config;

            if (config.IsEmpty)
            {
                return data;
            }

            GetData(config, data);

            return data;
        }

        private static void GetData(ConcurrentQueue<ConfigurationTelemetryEntry> buffer, List<ConfigurationKeyValue> destination)
        {
            while (buffer.TryDequeue(out var entry))
            {
                destination.Add(GetConfigKeyValue(entry));
            }
        }

        private static object? GetValue(ConfigurationTelemetryEntry entry)
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

        private static ConfigurationKeyValue GetConfigKeyValue(ConfigurationTelemetryEntry entry)
        {
            return new ConfigurationKeyValue(
                name: entry.Key,
                origin: entry.Origin.ToStringFast(),
                seqId: entry.SeqId,
                error: entry.Error,
                value: GetValue(entry));
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
