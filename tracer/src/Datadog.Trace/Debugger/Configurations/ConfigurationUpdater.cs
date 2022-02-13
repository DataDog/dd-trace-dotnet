// <copyright file="ConfigurationUpdater.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Debugger.Configurations;

internal class ConfigurationUpdater : IConfigurationUpdater
{
    private const int MaxAllowedSnapshotProbes = 100;
    private const int MaxAllowedMetricProbes = 100;
    private readonly ImmutableDebuggerSettings _settings;
    private ProbeConfiguration _currentConfiguration;

    public ConfigurationUpdater(ImmutableDebuggerSettings settings)
    {
        _settings = settings;
        _currentConfiguration = new ProbeConfiguration();
    }

    public bool Accept(ProbeConfiguration configuration)
    {
        try
        {
            var filteredConfiguration = ApplyConfigurationFilters(configuration);
            var comparer = new ProbeConfigurationComparer(_currentConfiguration, filteredConfiguration);

            if (comparer.HasProbeRelatedChanges)
            {
                HandleProbesChanges(comparer);
            }

            if (comparer.HasRateLimitChanged)
            {
                HandleRateLimitChanged(comparer);
            }

            _currentConfiguration = configuration;

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to accept configurations");
            return false;
        }
    }

    private ProbeConfiguration ApplyConfigurationFilters(ProbeConfiguration configuration)
    {
        return new ProbeConfiguration()
        {
            Id = configuration.Id,
            AllowList = configuration.AllowList,
            DenyList = configuration.DenyList,
            OpsConfiguration = configuration.OpsConfiguration,
            Probes = Filter(configuration.Probes, MaxAllowedSnapshotProbes),
            MetricProbes = Filter(configuration.MetricProbes, MaxAllowedMetricProbes)
        };

        T[] Filter<T>(T[] probes, int maxAllowedProbes)
            where T : ProbeDefinition
        {
            return
                probes
                   .Where(probe => probe.Active)
                   .Where(probe => probe.Language == "dotnet")
                   .Where(IsEnvAndVersionMatch)
                   .Take(maxAllowedProbes)
                   .ToArray();

            bool IsEnvAndVersionMatch(ProbeDefinition probe)
            {
                if (probe.Tags == null || probe.Tags.Length == 0)
                {
                    return true;
                }

                var probeEnv = probe.Tags.FirstOrDefault(tag => tag.Key == "env");
                var probeVersion = probe.Tags.FirstOrDefault(tag => tag.Key == "version");

                var envMatch = probeEnv == null || probeEnv.Value == _settings.Environment;
                var versionMatch = probeVersion == null || probeVersion.Value == _settings.Version;

                return envMatch && versionMatch;
            }
        }
    }

    private void HandleProbesChanges(ProbeConfigurationComparer comparer)
    {
        // todo handle probe changes
    }

    private void HandleRateLimitChanged(ProbeConfigurationComparer comparer)
    {
        // todo handle rate limited changes
    }
}
