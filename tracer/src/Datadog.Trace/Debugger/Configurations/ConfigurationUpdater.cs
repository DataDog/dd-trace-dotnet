// <copyright file="ConfigurationUpdater.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Debugger.Configurations
{
    internal sealed class ConfigurationUpdater
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ConfigurationUpdater>();

        private readonly Func<ProbeTarget> _getProbeTarget;
        private readonly int _maxProbesPerType;
        private readonly IDebuggerGlobalRateLimiter _globalRateLimiter;
        private readonly HashSet<string> _removedRcmProbeIds = new();
        private Func<IReadOnlyList<ProbeDefinition>, List<UpdateResult>>? _handleAddedProbesChanges;
        private Action<string[]>? _handleRemovedProbesChanges;

        private ProbeConfiguration _currentConfiguration;
        private ProbeConfiguration? _fileConfiguration;
        private ProbeConfiguration _rcmConfiguration;
        private ProbeTarget _activeProbeTarget;

        private ConfigurationUpdater(Func<ProbeTarget> getProbeTarget, int maxProbesPerType, IDebuggerGlobalRateLimiter? globalRateLimiter)
        {
            _getProbeTarget = getProbeTarget;
            _maxProbesPerType = maxProbesPerType;
            _globalRateLimiter = globalRateLimiter ?? DebuggerGlobalRateLimiter.Instance;
            _currentConfiguration = new ProbeConfiguration();
            _rcmConfiguration = new ProbeConfiguration();
        }

        public static ConfigurationUpdater Create(Func<ProbeTarget> getProbeTarget, int maxProbesPerType, IDebuggerGlobalRateLimiter? globalRateLimiter = null)
        {
            return new ConfigurationUpdater(getProbeTarget, maxProbesPerType, globalRateLimiter);
        }

        public void SetProbeInstrumentationHandlers(Func<IReadOnlyList<ProbeDefinition>, List<UpdateResult>> handleAddedProbesChanges, Action<string[]> handleRemovedProbesChanges)
        {
            _handleAddedProbesChanges = handleAddedProbesChanges;
            _handleRemovedProbesChanges = handleRemovedProbesChanges;
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

        public List<UpdateResult> AcceptFile(ProbeConfiguration configuration, Action? beforeAddingProbes = null)
        {
            _fileConfiguration = configuration;
            return ApplyEffectiveConfiguration(beforeAddingProbes: beforeAddingProbes);
        }

        public void ReconcileProbeTarget() => _ = ApplyEffectiveConfiguration();

        public void AcceptRemoved(List<RemoteConfigurationPath> paths)
        {
            try
            {
                var removedProbeIds = paths.Where(ProbeConfigurationUtils.IsProbePath).Select(ProbeConfigurationUtils.GetProbeIdFromPath).ToArray();
                var isServiceConfigurationRemoved = paths.Exists(path => path.Id.StartsWith(DefinitionPaths.ServiceConfiguration, StringComparison.Ordinal));
                foreach (var probeId in removedProbeIds)
                {
                    _removedRcmProbeIds.Add(probeId);
                }

                _rcmConfiguration = ProbeConfigurationUtils.RemoveItems(_rcmConfiguration, removedProbeIds, isServiceConfigurationRemoved);
                HandleRemovedProbesChanges(removedProbeIds);
                _ = ApplyEffectiveConfiguration(removedProbeIds);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to remove configurations");
            }
        }

        private static Func<ProbeDefinition, bool> CreateProbeFilter(ProbeTarget target)
        {
            return probe => probe.Language == TracerConstants.Language && IsEnvAndVersionMatch(probe, target.Environment, target.ServiceVersion);
        }

        private static bool IsEnvAndVersionMatch(ProbeDefinition probe, string? environment, string? serviceVersion)
        {
            if (probe.Tags == null || probe.Tags.Length == 0)
            {
                return true;
            }

            var tagMap = probe.Tags.Distinct().Select(Tag.FromString).ToDictionary(tag => tag.Key, tag => tag.Value);
            var envNotExistsOrMatch = !tagMap.TryGetValue("env", out var probeEnv) || probeEnv == environment;
            var versionNotExistsOrMatch = !tagMap.TryGetValue("version", out var probeVersion) || probeVersion == serviceVersion;
            return envNotExistsOrMatch && versionNotExistsOrMatch;
        }

        private List<UpdateResult> ApplyEffectiveConfiguration(string[]? alreadyRemoved = null, Action? beforeAddingProbes = null)
        {
            var result = new List<UpdateResult>();
            var target = _getProbeTarget();
            var filteredConfiguration = ApplyConfigurationFilters(GetEffectiveConfiguration(), CreateProbeFilter(target));
            var comparer = new ProbeConfigurationComparer(_currentConfiguration, filteredConfiguration);

            if (_activeProbeTarget != target)
            {
                var obsolete = ProbeConfigurationUtils.GetProbeIds(_currentConfiguration)
                                                      .Except(ProbeConfigurationUtils.GetProbeIds(filteredConfiguration));
                if (alreadyRemoved is { Length: > 0 })
                {
                    obsolete = obsolete.Except(alreadyRemoved);
                }

                var obsoleteIds = obsolete.ToArray();
                if (obsoleteIds.Length > 0)
                {
                    HandleRemovedProbesChanges(obsoleteIds);
                }
            }

            if (comparer.AddedDefinitions.Count > 0)
            {
                beforeAddingProbes?.Invoke();
            }

            // Apply the global limiter before making new probes live.
            if (comparer.HasRateLimitChanged)
            {
                HandleRateLimitChanged(filteredConfiguration);
            }

            if (comparer.HasProbeRelatedChanges)
            {
                result = HandleAddedProbesChanges(comparer);
            }

            _currentConfiguration = filteredConfiguration;
            _activeProbeTarget = target;

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

        private ProbeConfiguration ApplyConfigurationFilters(ProbeConfiguration configuration, Func<ProbeDefinition, bool> include)
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
                var filtered = probes.Where<T>(include);

                if (_maxProbesPerType > 0)
                {
                    filtered = filtered.Take(_maxProbesPerType);
                }

                return filtered.ToArray();
            }
        }

        private List<UpdateResult> HandleAddedProbesChanges(ProbeConfigurationComparer comparer)
        {
            return _handleAddedProbesChanges?.Invoke(comparer.AddedDefinitions) ?? [];
        }

        private void HandleRemovedProbesChanges(string[] probeIds)
        {
            _handleRemovedProbesChanges?.Invoke(probeIds);
        }

        private void HandleRateLimitChanged(ProbeConfiguration configuration)
        {
            _globalRateLimiter.SetRate(configuration.ServiceConfiguration?.Sampling?.SnapshotsPerSecond);
        }

        internal readonly record struct ProbeTarget(string? Environment, string? ServiceVersion);

        internal sealed record UpdateResult(string Id, string? Error);
    }
}
