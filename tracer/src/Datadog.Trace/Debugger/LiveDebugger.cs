// <copyright file="LiveDebugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger
{
    internal class LiveDebugger
    {
        private static readonly Lazy<LiveDebugger> LazyInstance = new Lazy<LiveDebugger>(Create, isThreadSafe: true);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LiveDebugger));

        private readonly ImmutableDebuggerSettings _settings;
        private readonly DiscoveryService _discoveryService;
        private readonly ConfigurationPoller _configurationPoller;
        private readonly SnapshotUploader _snapshotUploader;
        private readonly LineProbeResolver _lineProbeResolver;
        private readonly List<ProbeDefinition> _unboundProbes = new();
        private readonly object _locker = new();

        private LiveDebugger()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();

            var apiFactory = DebuggerTransportStrategy.Get();
            _discoveryService = DiscoveryService.Create(source, apiFactory);

            _settings = ImmutableDebuggerSettings.Create(DebuggerSettings.FromSource(source));
            var probeConfigurationApi = ProbeConfigurationFactory.Create(_settings, apiFactory, _discoveryService);
            var updater = ConfigurationUpdater.Create(_settings);
            _configurationPoller = ConfigurationPoller.Create(probeConfigurationApi, updater, _settings);

            var snapshotApi = SnapshotApi.Create(_settings, apiFactory, _discoveryService);
            _snapshotUploader = SnapshotUploader.Create(snapshotApi);
            _lineProbeResolver = new LineProbeResolver();
            _lineProbeResolver.Start();
        }

        public static LiveDebugger Instance => LazyInstance.Value;

        private static LiveDebugger Create()
        {
            var debugger = new LiveDebugger();
            debugger.Initialize();

            return debugger;
        }

        private void Initialize()
        {
            if (!_settings.Enabled)
            {
                Log.Information("Live Debugger is disabled. To enable it, please set DD_DEBUGGER_ENABLED environment variable to 'true'.");
                return;
            }

            Log.Information("Initializing Live Debugger");
            Task.Run(async () => await InitializeAsync().ConfigureAwait(false));
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => CheckUnboundProbes();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var isDiscoverySuccessful = await _discoveryService.DiscoverAsync().ConfigureAwait(false);
                var isProbeConfigurationSupported = isDiscoverySuccessful && !string.IsNullOrWhiteSpace(_discoveryService.ProbeConfigurationEndpoint);
                if (_settings.ProbeMode == ProbeMode.Agent && !isProbeConfigurationSupported)
                {
                    Log.Warning("You must upgrade datadog-agent in order to leverage the Live Debugger. All debugging features will be disabled.");
                    return;
                }

                await StartPollingLoop().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Error(e, "Initializing Live Debugger failed.");
            }

            Task StartPollingLoop()
            {
                LifetimeManager.Instance.AddShutdownTask(() => _configurationPoller.Dispose());
                return _configurationPoller.StartPollingAsync();
            }
        }

        internal void InstallProbes(IReadOnlyList<ProbeDefinition> probeDefinitions)
        {
            lock (_locker)
            {
                if (probeDefinitions.Count == 0)
                {
                    return;
                }

                Log.Information($"Live Debugger.InstrumentProbes: Request to instrument {probeDefinitions.Count} probes definitions");

                var methodProbes = new List<NativeMethodProbeDefinition>();
                var lineProbes = new List<BoundLineProbeLocation>();
                foreach (var probe in probeDefinitions)
                {
                    switch (GetProbeLocationType(probe))
                    {
                        case ProbeLocationType.Line:
                            var result = _lineProbeResolver.TryResolveLineProbe(probe, out var location);
                            switch (result)
                            {
                                case ResolveResult.Bound:
                                    lineProbes.Add(location);
                                    break;
                                case ResolveResult.Unbound:
                                    _unboundProbes.Add(probe);
                                    break;
                            }

                            break;
                        case ProbeLocationType.Method:
                            var nativeDefinition = new NativeMethodProbeDefinition("Samples.Probes", probe.Where.TypeName, probe.Where.MethodName, probe.Where.Signature?.Split(separator: ','));
                            methodProbes.Add(nativeDefinition);
                            break;
                        case ProbeLocationType.Unrecognized:
                            break;
                    }
                }

                using var disposable = new DisposableEnumerable<NativeMethodProbeDefinition>(methodProbes);
                DebuggerNativeMethods.InstrumentProbes("1", methodProbes.ToArray());
            }
        }

        private ProbeLocationType GetProbeLocationType(ProbeDefinition probe)
        {
            if (!string.IsNullOrEmpty(probe.Where.MethodName))
            {
                return ProbeLocationType.Method;
            }

            if (!string.IsNullOrEmpty(probe.Where.SourceFile))
            {
                return ProbeLocationType.Line;
            }

            return ProbeLocationType.Unrecognized;
        }

        internal async Task UploadSnapshot(string snapshot)
        {
            try
            {
                Log.Information($"Live Debugger.UploadSnapshot: Request to upload snapshot size {snapshot.Length}");
                await _snapshotUploader.UploadSnapshot(snapshot).ConfigureAwait(continueOnCapturedContext: false);
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to upload snapshot");
            }
        }

        public void RemoveProbes(IReadOnlyList<ProbeDefinition> removedDefinitions)
        {
            lock (_locker)
            {
                foreach (var probeDefinition in removedDefinitions)
                {
                    _unboundProbes.RemoveAll(m => m.Id == probeDefinition.Id);
                }
            }
        }

        private void CheckUnboundProbes()
        {
            // A new assembly was loaded, so re-examine whether the probe can now be resolved.
            lock (_locker)
            {
                if (_unboundProbes.Count == 0)
                {
                    return;
                }

                foreach (var unboundProbe in _unboundProbes)
                {
                    var result = _lineProbeResolver.TryResolveLineProbe(unboundProbe, out var bytecodeLocation);
                    if (result == ResolveResult.Bound)
                    {
                        // TODO: Install the line probe.
                    }
                }
            }
        }
    }
}

internal record BoundLineProbeLocation(ProbeDefinition ProbeDefinition, Guid MVID, int MethodToken, int? BytecodeOffset);
