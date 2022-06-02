// <copyright file="ClientStatsPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;

namespace Datadog.Trace.Agent
{
    internal class ClientStatsPayload
    {
        private long _sequence;

        public string HostName { get; set; }

        public string Environment { get; set; }

        public string Version { get; set; }

        public long GetSequenceNumber() => Interlocked.Increment(ref _sequence);
    }
}
