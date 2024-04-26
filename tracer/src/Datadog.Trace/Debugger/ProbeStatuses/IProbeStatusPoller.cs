// <copyright file="IProbeStatusPoller.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Debugger.ProbeStatuses
{
    internal interface IProbeStatusPoller : IDisposable
    {
        void StartPolling();

        void AddProbes(FetchProbeStatus[] newProbes);

        void RemoveProbes(string[] removeProbes);

        void UpdateProbes(string[] probeIds, FetchProbeStatus[] newProbeStatuses);

        void UpdateProbe(string probeId, FetchProbeStatus newProbeStatus);

        string[] GetBoundedProbes(string[] candidateProbeIds);
    }
}
