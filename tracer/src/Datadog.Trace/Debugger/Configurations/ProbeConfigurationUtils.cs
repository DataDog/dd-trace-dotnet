// <copyright file="ProbeConfigurationUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Debugger.Configurations;

internal static class ProbeConfigurationUtils
{
    public static ProbeConfiguration Merge(ProbeConfiguration? lowerPriority, ProbeConfiguration? higherPriority)
    {
        return new ProbeConfiguration
        {
            ServiceConfiguration = higherPriority?.ServiceConfiguration ?? lowerPriority?.ServiceConfiguration,
            LogProbes = MergeProbes(lowerPriority?.LogProbes, higherPriority?.LogProbes ?? []),
            MetricProbes = MergeProbes(lowerPriority?.MetricProbes, higherPriority?.MetricProbes ?? []),
            SpanProbes = MergeProbes(lowerPriority?.SpanProbes, higherPriority?.SpanProbes ?? []),
            SpanDecorationProbes = MergeProbes(lowerPriority?.SpanDecorationProbes, higherPriority?.SpanDecorationProbes ?? [])
        };
    }

    public static ProbeConfiguration RemoveItems(ProbeConfiguration configuration, ICollection<string> removedProbeIds, bool removeServiceConfiguration)
    {
        if (removedProbeIds.Count == 0 && !removeServiceConfiguration)
        {
            return configuration;
        }

        return new ProbeConfiguration
        {
            ServiceConfiguration = removeServiceConfiguration ? null : configuration.ServiceConfiguration,
            LogProbes = configuration.LogProbes.Where(probe => !removedProbeIds.Contains(probe.Id)).ToArray(),
            MetricProbes = configuration.MetricProbes.Where(probe => !removedProbeIds.Contains(probe.Id)).ToArray(),
            SpanProbes = configuration.SpanProbes.Where(probe => !removedProbeIds.Contains(probe.Id)).ToArray(),
            SpanDecorationProbes = configuration.SpanDecorationProbes.Where(probe => !removedProbeIds.Contains(probe.Id)).ToArray()
        };
    }

    public static string GetProbeIdFromPath(RemoteConfigurationPath path)
    {
        var id = path.Id;
        if (id.StartsWith(DefinitionPaths.LogProbe, StringComparison.Ordinal))
        {
            return id.Substring(DefinitionPaths.LogProbe.Length);
        }

        if (id.StartsWith(DefinitionPaths.MetricProbe, StringComparison.Ordinal))
        {
            return id.Substring(DefinitionPaths.MetricProbe.Length);
        }

        if (id.StartsWith(DefinitionPaths.SpanProbe, StringComparison.Ordinal))
        {
            return id.Substring(DefinitionPaths.SpanProbe.Length);
        }

        if (id.StartsWith(DefinitionPaths.SpanDecorationProbe, StringComparison.Ordinal))
        {
            return id.Substring(DefinitionPaths.SpanDecorationProbe.Length);
        }

        return id;
    }

    public static bool IsProbePath(RemoteConfigurationPath path)
    {
        var id = path.Id;
        return id.StartsWith(DefinitionPaths.LogProbe, StringComparison.Ordinal)
            || id.StartsWith(DefinitionPaths.MetricProbe, StringComparison.Ordinal)
            || id.StartsWith(DefinitionPaths.SpanProbe, StringComparison.Ordinal)
            || id.StartsWith(DefinitionPaths.SpanDecorationProbe, StringComparison.Ordinal);
    }

    public static IEnumerable<string> GetProbeIds(ProbeConfiguration configuration)
    {
        foreach (var probe in configuration.LogProbes)
        {
            yield return probe.Id;
        }

        foreach (var probe in configuration.MetricProbes)
        {
            yield return probe.Id;
        }

        foreach (var probe in configuration.SpanProbes)
        {
            yield return probe.Id;
        }

        foreach (var probe in configuration.SpanDecorationProbes)
        {
            yield return probe.Id;
        }
    }

    private static T[] MergeProbes<T>(T[]? lowerPriority, T[] higherPriority)
        where T : ProbeDefinition
    {
        if (lowerPriority == null || lowerPriority.Length == 0)
        {
            return higherPriority;
        }

        if (higherPriority.Length == 0)
        {
            return lowerPriority;
        }

        var mergedProbes = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var probe in lowerPriority)
        {
            mergedProbes[probe.Id] = probe;
        }

        foreach (var probe in higherPriority)
        {
            mergedProbes[probe.Id] = probe;
        }

        return mergedProbes.Values.ToArray();
    }
}
