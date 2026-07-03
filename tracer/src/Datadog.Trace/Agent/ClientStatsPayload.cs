// <copyright file="ClientStatsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.MessagePack;

namespace Datadog.Trace.Agent
{
    internal sealed class ClientStatsPayload(MutableSettings settings)
    {
        private AppSettings _settings = CreateSettings(settings);
        private long _sequence;

        public string? HostName { get; init; }

        public AppSettings Details => _settings;

        public long GetSequenceNumber() => Interlocked.Increment(ref _sequence);

        public void UpdateDetails(MutableSettings settings)
            => Interlocked.Exchange(ref _settings, CreateSettings(settings));

        private static AppSettings CreateSettings(MutableSettings settings)
            => new(settings.Environment, settings.ServiceVersion, settings.DefaultServiceName, settings.ProcessTags, settings.GitCommitSha, BuildDdTags(settings.GlobalTags));

        private static byte[][] BuildDdTags(ReadOnlyDictionary<string, string> globalTags)
        {
            if (globalTags.Count == 0)
            {
                return [];
            }

            var tags = new byte[globalTags.Count][];
            var i = 0;
            foreach (var kvp in globalTags)
            {
                var keyCount = StringEncoding.UTF8.GetByteCount(kvp.Key);
                var valueCount = StringEncoding.UTF8.GetByteCount(kvp.Value);
                var buffer = new byte[keyCount + 1 + valueCount];

                StringEncoding.UTF8.GetBytes(kvp.Key, 0, kvp.Key.Length, buffer, 0);
                buffer[keyCount] = (byte)':';
                StringEncoding.UTF8.GetBytes(kvp.Value, 0, kvp.Value.Length, buffer, keyCount + 1);

                tags[i] = buffer;
                i++;
            }

            return tags;
        }

        internal sealed record AppSettings(string? Environment, string? Version, string DefaultServiceName, ProcessTags? ProcessTags, string? GitCommitSha, byte[][] DdTags);
    }
}
