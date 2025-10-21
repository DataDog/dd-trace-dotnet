// <copyright file="ConfigurationUpdater.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;

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

        public List<UpdateResult> AcceptAdded(ProbeConfiguration configuration)
        {
            var result = new List<UpdateResult>();
            var filteredConfiguration = ApplyConfigurationFilters(configuration);
            var comparer = new ProbeConfigurationComparer(_currentConfiguration, filteredConfiguration);

            if (comparer.HasProbeRelatedChanges)
            {
                result = HandleAddedProbesChanges(comparer);
            }

            if (comparer.HasRateLimitChanged)
            {
                HandleRateLimitChanged(comparer);
            }

            _currentConfiguration = configuration;

            return result;
        }

        public void AcceptRemoved(List<RemoteConfigurationPath> paths)
        {
            try
            {
                HandleRemovedProbesChanges(paths);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove configurations");
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

        private List<UpdateResult> HandleAddedProbesChanges(ProbeConfigurationComparer comparer)
        {
            return DebuggerManager.Instance.DynamicInstrumentation?.UpdateAddedProbeInstrumentations(comparer.AddedDefinitions) ?? [];
        }

        private void HandleRemovedProbesChanges(List<RemoteConfigurationPath> paths)
        {
            DebuggerManager.Instance.DynamicInstrumentation?.UpdateRemovedProbeInstrumentations(paths);
        }

        private void HandleRateLimitChanged(ProbeConfigurationComparer comparer)
        {
            // todo handle rate limited changes
        }

        internal record UpdateResult(string Id, string? Error);
    }
}
