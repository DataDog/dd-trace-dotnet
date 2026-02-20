// <copyright file="ConfigurationTelemetry.Collector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration.Telemetry
{
    // This is the "collector" implementation
    internal partial class ConfigurationTelemetry
    {
        private readonly List<List<ConfigurationKeyValue>> _allData = new();
        private ConcurrentQueue<ConfigurationTelemetryEntry> _backBuffer = new();
        private int _reportedCount = 0;

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
            lock (_allData)
            {
                var latestData = GetLatestData();
                if (latestData != null)
                {
                    _allData.Add(latestData);
                }

                if (_reportedCount == _allData.Count)
                {
                    // we've reported everything, nothing more to do
                    return null;
                }

                if (_reportedCount == _allData.Count - 1)
                {
                    // we just need to report the latest results
                    _reportedCount++;
                    return latestData;
                }

                // we have multiple collections to report
                _reportedCount = _allData.Count;
                return new ListOfListOfConfigurationKeyValue(_allData, _reportedCount);
            }
        }

        public ICollection<ConfigurationKeyValue>? GetFullData()
        {
            lock (_allData)
            {
                // We have to make sure we include all the data that isn't
                // yet reported through other means
                var latestData = GetLatestData();
                if (latestData != null)
                {
                    _allData.Add(latestData);
                }

                // Take a copy of the full collection, don't skip anything
                return new ListOfListOfConfigurationKeyValue(_allData, skipCount: 0);
            }
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

        private List<ConfigurationKeyValue>? GetLatestData()
        {
            if (_entries.IsEmpty && _backBuffer.IsEmpty)
            {
                return null;
            }

            // Getting the count is relatively expensive, but we use it here regardless to try
            // to avoid list array re-allocations. The arbitrary additional 8 is because if more
            // configs flow in while this method is running, the allocation would be expensive,
            // but an array 8 larger isn't a big deal!
            var data = new List<ConfigurationKeyValue>(_backBuffer.Count + _entries.Count + 8);

            // There's a small race condition in the telemetry collector, which means that
            // the _backBuffer MAY contain "left over" config from the previous flush.
            // Grabbing it here ensures that we don't lose them completely
            GetData(_backBuffer, data);

            Debug.Assert(_backBuffer.IsEmpty, "The back buffer should be empty because nothing should be writing to it");

            var config = Interlocked.Exchange(ref _entries, _backBuffer);
            _backBuffer = config;

            if (!config.IsEmpty)
            {
                GetData(config, data);
            }

            return data;
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

            // clears any saved data
            _allData.Clear();
        }

        private sealed class ListOfListOfConfigurationKeyValue : ICollection<ConfigurationKeyValue>
        {
            private readonly List<List<ConfigurationKeyValue>> _list;

            public ListOfListOfConfigurationKeyValue(List<List<ConfigurationKeyValue>> listOfLists, int skipCount)
            {
                var itemCount = 0;
                var listCount = listOfLists.Count;
                var finalList = new List<List<ConfigurationKeyValue>>(listCount - skipCount);
                for (var i = skipCount; i < listCount; i++)
                {
                    var list = listOfLists[i];
                    finalList.Add(list);
                    itemCount += list.Count;
                }

                _list = finalList;
                Count = itemCount;
            }

            public int Count { get; }

            public bool IsReadOnly => true;

            public Enumerator GetEnumerator() => new Enumerator(_list);

            IEnumerator<ConfigurationKeyValue> IEnumerable<ConfigurationKeyValue>.GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            // We don't need to implement these for now as they're never called
            public void Add(ConfigurationKeyValue item) => throw new System.NotImplementedException();

            public void Clear() => throw new System.NotImplementedException();

            public bool Contains(ConfigurationKeyValue item) => throw new System.NotImplementedException();

            public void CopyTo(ConfigurationKeyValue[] array, int arrayIndex) => throw new System.NotImplementedException();

            public bool Remove(ConfigurationKeyValue item) => throw new System.NotImplementedException();

            internal struct Enumerator : IEnumerator<ConfigurationKeyValue>
            {
                private readonly List<List<ConfigurationKeyValue>> _outer;
                private int _outerIndex;
                private int _innerIndex;
                private ConfigurationKeyValue _current;

                internal Enumerator(List<List<ConfigurationKeyValue>> outer)
                {
                    _outer = outer;
                    _outerIndex = 0;
                    _innerIndex = 0;
                    _current = default;
                }

                public ConfigurationKeyValue Current => _current;

                object IEnumerator.Current => _current;

                public bool MoveNext()
                {
                    while (_outerIndex < _outer.Count)
                    {
                        var inner = _outer[_outerIndex];

                        if (_innerIndex < inner.Count)
                        {
                            _current = inner[_innerIndex];
                            _innerIndex++;
                            return true;
                        }

                        _outerIndex++;
                        _innerIndex = 0;
                    }

                    return false;
                }

                public void Reset()
                {
                    _outerIndex = 0;
                    _innerIndex = 0;
                    _current = default;
                }

                public void Dispose()
                {
                    // nothing to dispose
                }
            }
        }
    }
}
