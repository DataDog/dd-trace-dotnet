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
    internal sealed class ConfigurationUpdater
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationUpdater>();

        private readonly string? _env;
        private readonly string? _version;
        private readonly int _maxProbesPerType;

        private ProbeConfiguration _currentConfiguration;

        private ConfigurationUpdater(string? env, string? version, int maxProbesPerType)
        {
            _env = env;
            _version = version;
            _maxProbesPerType = maxProbesPerType;
            _currentConfiguration = new ProbeConfiguration();
        }

        public static ConfigurationUpdater Create(string? environment, string? serviceVersion, int maxProbesPerType)
        {
            return new ConfigurationUpdater(environment, serviceVersion, maxProbesPerType);
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
                LogProbes = Filter(configuration.LogProbes),
                MetricProbes = Filter(configuration.MetricProbes),
                SpanProbes = Filter(configuration.SpanProbes),
                SpanDecorationProbes = Filter(configuration.SpanDecorationProbes)
            };

            T[] Filter<T>(T[] probes)
                where T : ProbeDefinition
            {
                var filtered =
                    probes
                       .Where(probe => probe.Language == TracerConstants.Language)
                       .Where(IsEnvAndVersionMatch);

                if (_maxProbesPerType > 0)
                {
                    filtered = filtered.Take(_maxProbesPerType);
                }

                return filtered.ToArray();

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

        internal sealed record UpdateResult(string Id, string? Error);
    }
}
