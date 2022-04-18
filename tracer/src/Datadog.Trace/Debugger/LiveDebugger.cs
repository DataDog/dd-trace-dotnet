// <copyright file="LiveDebugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger
{
    internal class LiveDebugger
    {
        private static readonly Lazy<LiveDebugger> LazyInstance = new Lazy<LiveDebugger>(Create, isThreadSafe: true);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LiveDebugger));

        private readonly ImmutableDebuggerSettings _settings;
        private readonly IDiscoveryService _discoveryService;
        private readonly IConfigurationPoller _configurationPoller;
        private readonly IDebuggerSink _debuggerSink;
        private readonly ILineProbeResolver _lineProbeResolver;
        private readonly List<ProbeDefinition> _unboundProbes;
        private readonly object _locker = new();

        private LiveDebugger(ImmutableDebuggerSettings settings, IDiscoveryService discoveryService, IConfigurationPoller configurationPoller, ILineProbeResolver lineProbeResolver, IDebuggerSink debuggerSink)
        {
            _settings = settings;
            _discoveryService = discoveryService;
            _configurationPoller = configurationPoller;
            _lineProbeResolver = lineProbeResolver;
            _debuggerSink = debuggerSink;
            _unboundProbes = new List<ProbeDefinition>();
        }

        public static LiveDebugger Instance => LazyInstance.Value;

        private static LiveDebugger Create()
        {
            var source = GlobalSettings.CreateDefaultConfigurationSource();
            var settings = ImmutableDebuggerSettings.Create(DebuggerSettings.FromSource(source));
            if (!settings.Enabled)
            {
                return Create(settings, null, null, null, null);
            }

            var apiFactory = DebuggerTransportStrategy.Get();
            IDiscoveryService discoveryService = DiscoveryService.Create(source, apiFactory);

            var probeConfigurationApi = ProbeConfigurationApiFactory.Create(settings, apiFactory, discoveryService);
            var updater = ConfigurationUpdater.Create(settings);
            IConfigurationPoller configurationPoller = ConfigurationPoller.Create(probeConfigurationApi, updater, settings);

            var snapshotStatusSink = SnapshotSink.Create(settings);
            var probeStatusSink = ProbeStatusSink.Create(settings);

            var batchApi = BatchUploadApiFactory.Create(settings, apiFactory, discoveryService);
            var batchUploader = BatchUploader.Create(batchApi);
            var debuggerSink = DebuggerSink.Create(snapshotStatusSink, probeStatusSink, settings, batchUploader);

            var lineProbeResolver = LineProbeResolver.Create();

            return Create(settings, discoveryService, configurationPoller, lineProbeResolver, debuggerSink);
        }

        // for tests
        internal static LiveDebugger Create(ImmutableDebuggerSettings settings, IDiscoveryService discoveryService, IConfigurationPoller configurationPoller, ILineProbeResolver lineProbeResolver, IDebuggerSink debuggerSink)
        {
            var debugger = new LiveDebugger(settings, discoveryService, configurationPoller, lineProbeResolver, debuggerSink);
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
            AppDomain.CurrentDomain.DomainUnload += (sender, args) => _lineProbeResolver.OnDomainUnloaded();
        }

        private async Task InitializeAsync()
        {
            try
            {
                if (_settings.ProbeMode == ProbeMode.Backend)
                {
                    await StartAsync().ConfigureAwait(false);
                    return;
                }

                var isDiscoverySuccessful = await _discoveryService.DiscoverAsync().ConfigureAwait(false);
                var isProbeConfigurationSupported = isDiscoverySuccessful && !string.IsNullOrWhiteSpace(_discoveryService.ProbeConfigurationEndpoint);
                if (_settings.ProbeMode == ProbeMode.Agent && !isProbeConfigurationSupported)
                {
                    Log.Warning("You must upgrade datadog-agent in order to leverage the Live Debugger. All debugging features will be disabled.");
                    return;
                }

                await StartAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Error(e, "Initializing Live Debugger failed.");
            }

            Task StartAsync()
            {
                LifetimeManager.Instance.AddShutdownTask(() => _configurationPoller.Dispose());
                LifetimeManager.Instance.AddShutdownTask(() => _debuggerSink.Dispose());

                return Task.WhenAll(_configurationPoller.StartPollingAsync(), _debuggerSink.StartFlushingAsync());
            }
        }

        internal void UpdateProbeInstrumentations(IReadOnlyList<ProbeDefinition> addedProbes, IReadOnlyList<ProbeDefinition> removedProbes)
        {
            lock (_locker)
            {
                if (addedProbes.Count == 0 && removedProbes.Count == 0)
                {
                    return;
                }

                Log.Information($"Live Debugger.InstrumentProbes: Request to instrument {addedProbes.Count} probes definitions and remove {removedProbes.Count} definitions");

                var methodProbes = new List<NativeMethodProbeDefinition>();
                var lineProbes = new List<NativeLineProbeDefinition>();
                foreach (var probe in addedProbes)
                {
                    switch (GetProbeLocationType(probe))
                    {
                        case ProbeLocationType.Line:
                            var (status, message) = _lineProbeResolver.TryResolveLineProbe(probe, out var location);
                            switch (status)
                            {
                                case LiveProbeResolveStatus.Bound:
                                    lineProbes.Add(new NativeLineProbeDefinition(location.ProbeDefinition.Id, location.MVID, location.MethodToken, (int)(location.BytecodeOffset), location.LineNumber, location.ProbeDefinition.Where.SourceFile));
                                    break;
                                case LiveProbeResolveStatus.Unbound:
                                    Log.Information(message);
                                    _unboundProbes.Add(probe);
                                    break;
                                case LiveProbeResolveStatus.Error:
                                    Log.Error(message);
                                    AddErrorProbeStatus(probe.Id, errorMessage: message);
                                    break;
                            }

                            break;
                        case ProbeLocationType.Method:
                            var nativeDefinition = new NativeMethodProbeDefinition(probe.Id, probe.Where.TypeName, probe.Where.MethodName, probe.Where.Signature?.Split(separator: ','));
                            methodProbes.Add(nativeDefinition);
                            break;
                        case ProbeLocationType.Unrecognized:
                            break;
                    }
                }

                var revertProbes = removedProbes.Select(probe => new NativeRemoveProbeRequest(probe.Id));
                RemoveUnboundProbes(removedProbes);
                using var disposable = new DisposableEnumerable<NativeMethodProbeDefinition>(methodProbes);
                DebuggerNativeMethods.InstrumentProbes(methodProbes.ToArray(), lineProbes.ToArray(), revertProbes.ToArray());
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

        private void RemoveUnboundProbes(IReadOnlyList<ProbeDefinition> removedDefinitions)
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
                    if (result.Status == LiveProbeResolveStatus.Bound)
                    {
                        // TODO: Install the line probe.
                    }
                }
            }
        }

        internal void AddSnapshot(string snapshot)
        {
            _debuggerSink.AddSnapshot(snapshot);
        }

        internal void AddReceivedProbeStatus(string probeId)
        {
            _debuggerSink.AddReceivedProbeStatus(probeId);
        }

        internal void AddInstalledProbeStatus(string probeId)
        {
            _debuggerSink.AddInstalledProbeStatus(probeId);
        }

        internal void AddBlockedProbeStatus(string probeId)
        {
            _debuggerSink.AddBlockedProbeStatus(probeId);
        }

        internal void AddErrorProbeStatus(string probeId, Exception exception = null, string errorMessage = null)
        {
            _debuggerSink.AddErrorProbeStatus(probeId, exception, errorMessage);
        }
    }
}

internal record BoundLineProbeLocation(ProbeDefinition ProbeDefinition, Guid MVID, int MethodToken, int? BytecodeOffset, int LineNumber);
