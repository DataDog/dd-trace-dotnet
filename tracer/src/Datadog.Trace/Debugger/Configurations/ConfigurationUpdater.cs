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
        private readonly HashSet<string> _removedRcmProbeIds = new(StringComparer.Ordinal);

        private ProbeConfiguration _currentConfiguration;
        private ProbeConfiguration? _fileConfiguration;
        private ProbeConfiguration _rcmConfiguration;

        private ConfigurationUpdater(string? env, string? version, int maxProbesPerType)
        {
            _env = env;
            _version = version;
            _maxProbesPerType = maxProbesPerType;
            _currentConfiguration = new ProbeConfiguration();
            _rcmConfiguration = new ProbeConfiguration();
        }

        public static ConfigurationUpdater Create(string? environment, string? serviceVersion, int maxProbesPerType)
        {
            return new ConfigurationUpdater(environment, serviceVersion, maxProbesPerType);
        }

        public List<UpdateResult> AcceptAdded(ProbeConfiguration configuration)
        {
            foreach (var probeId in ProbeConfigurationUtils.GetProbeIds(configuration))
            {
                _removedRcmProbeIds.Remove(probeId);
            }

            _rcmConfiguration = ProbeConfigurationUtils.Merge(_rcmConfiguration, configuration);
            return ApplyEffectiveConfiguration();
        }

        public List<UpdateResult> AcceptFile(ProbeConfiguration configuration)
        {
            _fileConfiguration = configuration;
            return ApplyEffectiveConfiguration();
        }

        public void AcceptRemoved(List<RemoteConfigurationPath> paths)
        {
            try
            {
                var removedProbeIds = paths.Where(ProbeConfigurationUtils.IsProbePath).Select(ProbeConfigurationUtils.GetProbeIdFromPath).ToArray();
                var isServiceConfigurationRemoved = paths.Any(path => path.Id.StartsWith(DefinitionPaths.ServiceConfiguration, StringComparison.Ordinal));
                foreach (var probeId in removedProbeIds)
                {
                    _removedRcmProbeIds.Add(probeId);
                }

                _rcmConfiguration = ProbeConfigurationUtils.RemoveItems(_rcmConfiguration, removedProbeIds, isServiceConfigurationRemoved);
                HandleRemovedProbesChanges(removedProbeIds);
                _ = ApplyEffectiveConfiguration();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove configurations");
            }
        }

        private List<UpdateResult> ApplyEffectiveConfiguration()
        {
            var result = new List<UpdateResult>();
            var filteredConfiguration = ApplyConfigurationFilters(GetEffectiveConfiguration());
            var comparer = new ProbeConfigurationComparer(_currentConfiguration, filteredConfiguration);

            if (comparer.HasProbeRelatedChanges)
            {
                result = HandleAddedProbesChanges(comparer);
            }

            if (comparer.HasRateLimitChanged)
            {
                HandleRateLimitChanged(comparer);
            }

            _currentConfiguration = filteredConfiguration;

            return result;
        }

        private ProbeConfiguration GetEffectiveConfiguration()
        {
            var fileConfiguration = _fileConfiguration;
            if (fileConfiguration != null && _removedRcmProbeIds.Count != 0)
            {
                fileConfiguration = ProbeConfigurationUtils.RemoveItems(fileConfiguration, _removedRcmProbeIds, removeServiceConfiguration: false);
            }

            return ProbeConfigurationUtils.Merge(fileConfiguration, _rcmConfiguration);
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

        private void HandleRemovedProbesChanges(string[] probeIds)
        {
            DebuggerManager.Instance.DynamicInstrumentation?.UpdateRemovedProbeInstrumentations(probeIds);
        }

        private void HandleRateLimitChanged(ProbeConfigurationComparer comparer)
        {
            // todo handle rate limited changes
        }

        internal sealed record UpdateResult(string Id, string? Error);
    }
}
