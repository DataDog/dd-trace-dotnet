// <copyright file="ProbeStatusPollerMock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.ProbeStatuses;

namespace Datadog.Trace.Debugger.Helpers
{
    internal class ProbeStatusPollerMock : IProbeStatusPoller
    {
        internal bool Called { get; private set; }

        public void StartPolling()
        {
            Called = true;
        }

        public void AddProbes(FetchProbeStatus[] newProbes)
        {
            Called = true;
        }

        public void RemoveProbes(string[] removeProbes)
        {
            Called = true;
        }

        public void UpdateProbes(string[] probeIds, FetchProbeStatus[] newProbeStatuses)
        {
            Called = true;
        }

        public void UpdateProbe(string probeId, FetchProbeStatus newProbeStatus)
        {
            Called = true;
        }

        public string[] GetBoundedProbes(string[] candidateProbeIds)
        {
            Called = true;
            return candidateProbeIds;
        }

        public void Dispose()
        {
        }
    }
}
