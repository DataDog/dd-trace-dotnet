// <copyright file="FetchProbeStatus.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.PInvoke;

namespace Datadog.Trace.Debugger.ProbeStatuses
{
    internal record FetchProbeStatus(string ProbeId, int ProbeVersion, ProbeStatus ProbeStatus)
    {
        public FetchProbeStatus(string probeId, int probeVersion)
         : this(probeId, probeVersion, ProbeStatus.Default)
        {
        }

        public string ProbeId { get; } = ProbeId;

        public int ProbeVersion { get; } = ProbeVersion;

        public ProbeStatus ProbeStatus { get; } = ProbeStatus;

        public bool ShouldFetch() => ProbeStatus.Equals(ProbeStatus.Default);

        public virtual bool Equals(FetchProbeStatus other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            return other.ProbeId == ProbeId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ProbeId);
        }
    }
}
