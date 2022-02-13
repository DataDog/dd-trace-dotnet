// <copyright file="ProbeConfigurationComparer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;

namespace Datadog.Trace.Debugger.Configurations;

internal class ProbeConfigurationComparer
{
    public ProbeConfigurationComparer(
        ProbeConfiguration currentConfiguration,
        ProbeConfiguration incomingConfiguration)
    {
        var originalDefinitions = currentConfiguration.GetProbeDefinitions();
        var incomingDefinitions = incomingConfiguration.GetProbeDefinitions();

        AddedDefinitions = incomingDefinitions.Except(originalDefinitions).ToList();
        RemovedDefinitions = originalDefinitions.Except(incomingDefinitions).ToList();

        var isFilteredListChanged =
            !currentConfiguration.AllowList.Equals(incomingConfiguration.AllowList) ||
            !currentConfiguration.DenyList.Equals(incomingConfiguration.DenyList);

        HasProbeRelatedChanges = AddedDefinitions.Any() || RemovedDefinitions.Any() || isFilteredListChanged;
        HasRateLimitChanged = !currentConfiguration.Sampling.Equals(incomingConfiguration.Sampling) || HasProbeRelatedChanges;
    }

    public IReadOnlyList<ProbeDefinition> AddedDefinitions { get; }

    public IReadOnlyList<ProbeDefinition> RemovedDefinitions { get; }

    public bool HasProbeRelatedChanges { get; }

    public bool HasRateLimitChanged { get; }
}
