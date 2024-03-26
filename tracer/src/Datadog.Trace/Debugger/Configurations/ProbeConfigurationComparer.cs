// <copyright file="ProbeConfigurationComparer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;

namespace Datadog.Trace.Debugger.Configurations
{
    internal class ProbeConfigurationComparer
    {
        public ProbeConfigurationComparer(ProbeConfiguration currentConfiguration, ProbeConfiguration incomingConfiguration)
        {
            var addedLogs = incomingConfiguration.LogProbes.Where(ip => !currentConfiguration.LogProbes.Contains(ip));
            var addedMetrics = incomingConfiguration.MetricProbes.Where(ip => !currentConfiguration.MetricProbes.Contains(ip));
            var addedSpans = incomingConfiguration.SpanProbes.Where(ip => !currentConfiguration.SpanProbes.Contains(ip));
            var addedSpansDecoration = incomingConfiguration.SpanDecorationProbes.Where(ip => !currentConfiguration.SpanDecorationProbes.Contains(ip));

            AddedDefinitions =
                addedLogs
                   .Cast<ProbeDefinition>()
                   .Concat(addedMetrics)
                   .Concat(addedSpans)
                   .Concat(addedSpansDecoration)
                   .ToList();

            var isFilteredListChanged =
                (!currentConfiguration.ServiceConfiguration?.AllowList?.Equals(incomingConfiguration.ServiceConfiguration?.AllowList) ?? incomingConfiguration.ServiceConfiguration?.AllowList != null)
             || (!currentConfiguration.ServiceConfiguration?.DenyList?.Equals(incomingConfiguration.ServiceConfiguration?.DenyList) ?? incomingConfiguration.ServiceConfiguration?.DenyList != null);

            HasProbeRelatedChanges = AddedDefinitions.Any() || isFilteredListChanged;
            HasRateLimitChanged =
                (!currentConfiguration.ServiceConfiguration?.Sampling?.Equals(incomingConfiguration.ServiceConfiguration?.Sampling) ?? incomingConfiguration.ServiceConfiguration?.Sampling != null)
             || HasProbeRelatedChanges;
        }

        public IReadOnlyList<ProbeDefinition> AddedDefinitions { get; }

        public bool HasProbeRelatedChanges { get; }

        public bool HasRateLimitChanged { get; }
    }
}
