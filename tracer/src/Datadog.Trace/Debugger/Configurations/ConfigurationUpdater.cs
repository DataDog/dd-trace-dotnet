// <copyright file="ConfigurationUpdater.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Configurations
{
    internal class ConfigurationUpdater
    {
        private const int MaxAllowedLogProbes = 100;
        private const int MaxAllowedMetricProbes = 100;
        private const int MaxAllowedSpanProbes = 100;
        private const int MaxAllowedSpanDecorationProbes = 100;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationUpdater>();

        private readonly string? _env;
        private readonly string? _version;

        private ProbeConfiguration _currentConfiguration;

        private ConfigurationUpdater(string? env, string? version)
        {
            _env = env;
            _version = version;
            _currentConfiguration = new ProbeConfiguration();
        }

        public static ConfigurationUpdater Create(string? environment, string? serviceVersion)
        {
            return new ConfigurationUpdater(environment, serviceVersion);
        }

        public bool AcceptAdded(ProbeConfiguration configuration)
        {
            try
            {
                var filteredConfiguration = ApplyConfigurationFilters(configuration);
                var comparer = new ProbeConfigurationComparer(_currentConfiguration, filteredConfiguration);

                if (comparer.HasProbeRelatedChanges)
                {
                    HandleAddedProbesChanges(comparer);
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
                Log.Error(ex, "Failed to add configurations");
                return false;
            }
        }

        public bool AcceptRemoved(string[] removedProbesIds)
        {
            try
            {
                HandleRemovedProbesChanges(removedProbesIds);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove configurations");
                return false;
            }
        }

        private ProbeConfiguration ApplyConfigurationFilters(ProbeConfiguration configuration)
        {
            return new ProbeConfiguration()
            {
                ServiceConfiguration = configuration.ServiceConfiguration,
                LogProbes = Filter(configuration.LogProbes, MaxAllowedLogProbes),
                MetricProbes = Filter(configuration.MetricProbes, MaxAllowedMetricProbes),
                SpanProbes = Filter(configuration.SpanProbes, MaxAllowedSpanProbes),
                SpanDecorationProbes = Filter(configuration.SpanDecorationProbes, MaxAllowedSpanDecorationProbes)
            };

            T[] Filter<T>(T[] probes, int maxAllowedProbes)
                where T : ProbeDefinition
            {
                return
                    probes
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

                    var envNotExistsOrMatch = !tagMap.TryGetValue("env", out var probeEnv) || probeEnv == _env;
                    var versionNotExistsOrMatch = !tagMap.TryGetValue("version", out var probeVersion) || probeVersion == _version;

                    return envNotExistsOrMatch && versionNotExistsOrMatch;
                }
            }
        }

        private void HandleAddedProbesChanges(ProbeConfigurationComparer comparer)
        {
            LiveDebugger.Instance.UpdateAddedProbeInstrumentations(comparer.AddedDefinitions);
        }

        private void HandleRemovedProbesChanges(string[] removedProbesIds)
        {
            LiveDebugger.Instance.UpdateRemovedProbeInstrumentations(removedProbesIds);
        }

        private void HandleRateLimitChanged(ProbeConfigurationComparer comparer)
        {
            // todo handle rate limited changes
        }
    }
}
