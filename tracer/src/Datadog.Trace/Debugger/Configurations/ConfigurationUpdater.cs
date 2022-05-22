// <copyright file="ConfigurationUpdater.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Debugger.Configurations
{
    internal class ConfigurationUpdater
    {
        private const int MaxAllowedSnapshotProbes = 100;
        private const int MaxAllowedMetricProbes = 100;
        private readonly ImmutableDebuggerSettings _settings;
        private ProbeConfiguration _currentConfiguration;

        private ConfigurationUpdater(ImmutableDebuggerSettings settings)
        {
            _settings = settings;
            _currentConfiguration = new ProbeConfiguration();
        }

        public static ConfigurationUpdater Create(ImmutableDebuggerSettings settings)
        {
            return new ConfigurationUpdater(settings);
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
                SnapshotProbes = Filter(configuration.SnapshotProbes, MaxAllowedSnapshotProbes),
                MetricProbes = Filter(configuration.MetricProbes, MaxAllowedMetricProbes)
            };

            T[] Filter<T>(T[] probes, int maxAllowedProbes)
                where T : ProbeDefinition
            {
                return
                    probes
                       .Where(probe => probe.Active)
                       .Where(probe => probe.Language == TracerConstants.Language)
                       .Where(IsEnvAndVersionMatch)
                       .Take(maxAllowedProbes)
                       .ToArray();

                bool IsEnvAndVersionMatch(ProbeDefinition probe)
                {
                    if (probe.Tags == null || probe.Tags.Length == 0)
                    {
                        return true;
                    }

                    var tagMap =
                            probe.Tags
                                 .Distinct()
                                 .Select(Tag.FromString)
                                 .ToDictionary(tag => tag.Key, tag => tag.Value)
                        ;

                    var envNotExistsOrMatch = !tagMap.TryGetValue("env", out var probeEnv) || probeEnv == _settings.Environment;
                    var versionNotExistsOrMatch = !tagMap.TryGetValue("version", out var probeVersion) || probeVersion == _settings.ServiceVersion;

                    return envNotExistsOrMatch && versionNotExistsOrMatch;
                }
            }
        }

        private void HandleProbesChanges(ProbeConfigurationComparer comparer)
        {
            LiveDebugger.Instance.UpdateProbeInstrumentations(comparer.AddedDefinitions, comparer.RemovedDefinitions);
        }

        private void HandleRateLimitChanged(ProbeConfigurationComparer comparer)
        {
            // todo handle rate limited changes
        }
    }
}
