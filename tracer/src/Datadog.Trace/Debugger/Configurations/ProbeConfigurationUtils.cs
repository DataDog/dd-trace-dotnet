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
            LogProbes = RemoveProbes(configuration.LogProbes, removedProbeIds),
            MetricProbes = RemoveProbes(configuration.MetricProbes, removedProbeIds),
            SpanProbes = RemoveProbes(configuration.SpanProbes, removedProbeIds),
            SpanDecorationProbes = RemoveProbes(configuration.SpanDecorationProbes, removedProbeIds)
        };
    }

    public static string GetProbeIdFromPath(RemoteConfigurationPath path)
    {
        var id = path.Id;
        var prefixLength = GetProbeIdPrefixLength(id);
        return prefixLength == 0 ? id : id.Substring(prefixLength);
    }

    public static bool IsProbeId(RemoteConfigurationPath path, string probeId)
    {
        var id = path.Id;
        var prefixLength = GetProbeIdPrefixLength(id);
        if (prefixLength == 0)
        {
            return id == probeId;
        }

        return id.Length == prefixLength + probeId.Length
            && string.Compare(id, prefixLength, probeId, 0, probeId.Length, StringComparison.Ordinal) == 0;
    }

    public static bool IsProbePath(RemoteConfigurationPath path)
    {
        return GetProbeIdPrefixLength(path.Id) != 0;
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

        var mergedProbes = new Dictionary<string, T>();
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

    private static T[] RemoveProbes<T>(T[] probes, ICollection<string> removedProbeIds)
        where T : ProbeDefinition
    {
        if (removedProbeIds.Count == 0 || probes.Length == 0)
        {
            return probes;
        }

        var removedCount = 0;
        for (var i = 0; i < probes.Length; i++)
        {
            if (removedProbeIds.Contains(probes[i].Id))
            {
                removedCount++;
            }
        }

        if (removedCount == 0)
        {
            return probes;
        }

        if (removedCount == probes.Length)
        {
            return [];
        }

        var filteredProbes = new T[probes.Length - removedCount];
        var index = 0;
        for (var i = 0; i < probes.Length; i++)
        {
            var probe = probes[i];
            if (!removedProbeIds.Contains(probe.Id))
            {
                filteredProbes[index++] = probe;
            }
        }

        return filteredProbes;
    }

    private static int GetProbeIdPrefixLength(string id)
    {
        if (id.StartsWith(DefinitionPaths.LogProbe, StringComparison.Ordinal))
        {
            return DefinitionPaths.LogProbe.Length;
        }

        if (id.StartsWith(DefinitionPaths.MetricProbe, StringComparison.Ordinal))
        {
            return DefinitionPaths.MetricProbe.Length;
        }

        if (id.StartsWith(DefinitionPaths.SpanProbe, StringComparison.Ordinal))
        {
            return DefinitionPaths.SpanProbe.Length;
        }

        if (id.StartsWith(DefinitionPaths.SpanDecorationProbe, StringComparison.Ordinal))
        {
            return DefinitionPaths.SpanDecorationProbe.Length;
        }

        return 0;
    }
}
