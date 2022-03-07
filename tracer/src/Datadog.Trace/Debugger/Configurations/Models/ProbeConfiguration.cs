// <copyright file="ProbeConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Debugger.Configurations.Models;

internal class ProbeConfiguration : IJsonApiObject
{
    public string Id { get; set; }

    public SnapshotProbe[] SnapshotProbes { get; set; } = Array.Empty<SnapshotProbe>();

    public MetricProbe[] MetricProbes { get; set; } = Array.Empty<MetricProbe>();

    public FilterList AllowList { get; set; }

    public FilterList DenyList { get; set; }

    public Sampling? Sampling { get; set; }

    public OpsConfiguration OpsConfiguration { get; set; }

    public IEnumerable<ProbeDefinition> GetProbeDefinitions()
    {
        return SnapshotProbes.Cast<ProbeDefinition>().Concat(MetricProbes);
    }
}
