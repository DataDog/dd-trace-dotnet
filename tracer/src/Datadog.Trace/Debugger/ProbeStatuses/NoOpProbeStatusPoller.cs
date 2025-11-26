// <copyright file="NoOpProbeStatusPoller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Debugger.ProbeStatuses
{
    internal class NoOpProbeStatusPoller : IProbeStatusPoller
    {
        public static readonly NoOpProbeStatusPoller Instance = new();

        public void Dispose()
        {
        }

        public void StartPolling()
        {
        }

        public void AddProbes(FetchProbeStatus[] newProbes)
        {
        }

        public void RemoveProbes(string[] removeProbes)
        {
        }

        public void UpdateProbes(string[] probeIds, FetchProbeStatus[] newProbeStatuses)
        {
        }

        public void UpdateProbe(string probeId, FetchProbeStatus newProbeStatus)
        {
        }

        public string[] GetBoundedProbes()
        {
            throw new System.NotImplementedException();
        }

        public string[] GetBoundedProbes(string[] candidateProbeIds)
        {
            return [];
        }
    }
}
