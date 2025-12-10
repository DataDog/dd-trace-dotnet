// <copyright file="ClientStatsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Agent
{
    internal sealed class ClientStatsPayload(MutableSettings settings)
    {
        private AppSettings _settings = CreateSettings(settings);
        private long _sequence;

        public string HostName { get; init; }

        public AppSettings Details => _settings;

        public string ProcessTags { get; init; }

        public long GetSequenceNumber() => Interlocked.Increment(ref _sequence);

        public void UpdateDetails(MutableSettings settings)
            => Interlocked.Exchange(ref _settings, CreateSettings(settings));

        private static AppSettings CreateSettings(MutableSettings settings)
            => new(settings.Environment, settings.ServiceVersion);

        internal sealed record AppSettings(string Environment, string Version);
    }
}
