// <copyright file="LiveDebugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Debugger
{
    internal class LiveDebugger
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LiveDebugger));
        private static readonly object GlobalLock = new();

        private readonly DebuggerSettings _settings;
        private readonly IDiscoveryService _discoveryService;
        private readonly IRemoteConfigurationManager _remoteConfigurationManager;
        private readonly IDebuggerSink _debuggerSink;
        private readonly ILineProbeResolver _lineProbeResolver;
        private readonly List<ProbeDefinition> _unboundProbes;
        private readonly IProbeStatusPoller _probeStatusPoller;
        private readonly ConfigurationUpdater _configurationUpdater;
        private readonly object _instanceLock = new();
        private bool _isInitialized;
        private bool _isRcmAvailable;

        private LiveDebugger(
            DebuggerSettings settings,
            string serviceName,
            IDiscoveryService discoveryService,
            IRemoteConfigurationManager remoteConfigurationManager,
            ILineProbeResolver lineProbeResolver,
            IDebuggerSink debuggerSink,
            IProbeStatusPoller probeStatusPoller,
            ConfigurationUpdater configurationUpdater)
        {
            _settings = settings;
            _discoveryService = discoveryService;
            _lineProbeResolver = lineProbeResolver;
            _debuggerSink = debuggerSink;
            _probeStatusPoller = probeStatusPoller;
            _remoteConfigurationManager = remoteConfigurationManager;
            _configurationUpdater = configurationUpdater;
            _unboundProbes = new List<ProbeDefinition>();
            Product = new LiveDebuggerProduct();
            ServiceName = serviceName;
            discoveryService?.SubscribeToChanges(DiscoveryCallback);
        }

        public static LiveDebugger Instance { get; private set; }

        public LiveDebuggerProduct Product { get; }

        public string ServiceName { get; }

        public static LiveDebugger Create(
            DebuggerSettings settings,
            string serviceName,
            IDiscoveryService discoveryService,
            IRemoteConfigurationManager remoteConfigurationManager,
            ILineProbeResolver lineProbeResolver,
            IDebuggerSink debuggerSink,
            IProbeStatusPoller probeStatusPoller,
            ConfigurationUpdater configurationUpdater)
        {
            lock (GlobalLock)
            {
                return Instance ??= new LiveDebugger(settings, serviceName, discoveryService, remoteConfigurationManager, lineProbeResolver, debuggerSink, probeStatusPoller, configurationUpdater);
            }
        }

        public async Task InitializeAsync()
        {
            lock (GlobalLock)
            {
                if (!CanInitialize())
                {
                    return;
                }

                _isInitialized = true;
            }

            try
            {
                Log.Information("Live Debugger initialization started");

                _remoteConfigurationManager.RegisterProduct(Product);

                DebuggerSnapshotSerializer.SetConfig(_settings);
                Product.ConfigChanged += (sender, args) => AcceptAddedConfiguration(args);
                Product.ConfigRemoved += (sender, args) => AcceptRemovedConfiguration(args);
                AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => CheckUnboundProbes();

                await StartAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Error(e, "Initializing Live Debugger failed.");
            }

            bool CanInitialize()
            {
                if (_isInitialized)
                {
                    return false;
                }

                if (!_settings.Enabled)
                {
                    Log.Information("Live Debugger is disabled. To enable it, please set DD_DYNAMIC_INSTRUMENTATION_ENABLED environment variable to 'true'.");
                    return false;
                }

                if (!Volatile.Read(ref _isRcmAvailable))
                {
                    Log.Warning("Live Debugger could not be enabled because Remote Configuration Management is not available. Please ensure that you are using datadog-agent version 7.38.0 or higher, and that Remote Configuration Management is enabled in datadog-agent's yaml configuration file.");
                    return false;
                }

                return true;
            }

            Task StartAsync()
            {
                AddShutdownTask();

                _probeStatusPoller.StartPolling();
                return _debuggerSink.StartFlushingAsync();
            }

            void AddShutdownTask()
            {
                LifetimeManager.Instance.AddShutdownTask(() => _discoveryService.RemoveSubscription(DiscoveryCallback));
                LifetimeManager.Instance.AddShutdownTask(_debuggerSink.Dispose);
                LifetimeManager.Instance.AddShutdownTask(_probeStatusPoller.Dispose);
            }
        }

        internal void UpdateAddedProbeInstrumentations(IReadOnlyList<ProbeDefinition> addedProbes)
        {
            lock (_instanceLock)
            {
                if (addedProbes.Count == 0)
                {
                    return;
                }

                Log.Information($"Live Debugger.InstrumentProbes: Request to instrument {addedProbes.Count} probes definitions");

                var methodProbes = new List<NativeMethodProbeDefinition>();
                var lineProbes = new List<NativeLineProbeDefinition>();
                foreach (var probe in addedProbes)
                {
                    switch (GetProbeLocationType(probe))
                    {
                        case ProbeLocationType.Line:
                            var lineProbeResult = _lineProbeResolver.TryResolveLineProbe(probe, out var location);
                            var status = lineProbeResult.Status;
                            var message = lineProbeResult.Message;

                            Log.Information("Finished resolving line probe for ProbeID {ProbeID}. Result was '{Status}'. Message was: '{Message}'", probe.Id, status);
                            switch (status)
                            {
                                case LiveProbeResolveStatus.Bound:
                                    lineProbes.Add(new NativeLineProbeDefinition(location.ProbeDefinition.Id, location.MVID, location.MethodToken, (int)location.BytecodeOffset, location.LineNumber, location.ProbeDefinition.Where.SourceFile));
                                    break;
                                case LiveProbeResolveStatus.Unbound:
                                    _unboundProbes.Add(probe);
                                    break;
                                case LiveProbeResolveStatus.Error:
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

                using var disposable = new DisposableEnumerable<NativeMethodProbeDefinition>(methodProbes);
                DebuggerNativeMethods.InstrumentProbes(methodProbes.ToArray(), lineProbes.ToArray(), Array.Empty<NativeRemoveProbeRequest>());

                _probeStatusPoller.AddProbes(addedProbes.Select(probe => probe.Id).ToArray());

                foreach (var probe in addedProbes)
                {
                    if (probe is SnapshotProbe snapshotProbe && snapshotProbe.Sampling.HasValue)
                    {
                        ProbeRateLimiter.Instance.SetRate(probe.Id, (int)snapshotProbe.Sampling.Value.SnapshotsPerSecond);
                    }
                }

                // This log entry is being checked in integration test
                Log.Information("Live Debugger.InstrumentProbes: Request to instrument added probes definitions completed.");
            }
        }

        internal void UpdateRemovedProbeInstrumentations(string[] removedProbesIds)
        {
            lock (_instanceLock)
            {
                if (removedProbesIds.Length == 0)
                {
                    return;
                }

                Log.Information($"Live Debugger.InstrumentProbes: Request to remove {removedProbesIds.Length} probes.");

                RemoveUnboundProbes(removedProbesIds);

                var revertProbes = removedProbesIds
                   .Select(probeId => new NativeRemoveProbeRequest(probeId));

                DebuggerNativeMethods.InstrumentProbes(Array.Empty<NativeMethodProbeDefinition>(), Array.Empty<NativeLineProbeDefinition>(), revertProbes.ToArray());

                _probeStatusPoller.RemoveProbes(removedProbesIds);

                foreach (var id in removedProbesIds)
                {
                    ProbeRateLimiter.Instance.ResetRate(id);
                }

                // This log entry is being checked in integration test
                Log.Information("Live Debugger.InstrumentProbes: Request to de-instrument probes definitions completed.");
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

        private void RemoveUnboundProbes(IEnumerable<string> removedDefinitionIds)
        {
            lock (_instanceLock)
            {
                foreach (var id in removedDefinitionIds)
                {
                    _unboundProbes.RemoveAll(m => m.Id == id);
                }
            }
        }

        private void CheckUnboundProbes()
        {
            // A new assembly was loaded, so re-examine whether the probe can now be resolved.
            lock (_instanceLock)
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

        private void AcceptAddedConfiguration(ProductConfigChangedEventArgs args)
        {
            var snapshots = new List<SnapshotProbe>();
            var metrics = new List<MetricProbe>();
            ServiceConfiguration serviceConfig = null;

            foreach (var configContent in args.ConfigContents)
            {
                try
                {
                    switch (configContent.Path.Id)
                    {
                        case { } id when id.StartsWith(DefinitionPaths.SnapshotProbe):
                            snapshots.Add(configContent.Deserialize<SnapshotProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.MetricProbeProbe):
                            metrics.Add(configContent.Deserialize<MetricProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.ServiceConfiguration):
                            serviceConfig = configContent.Deserialize<ServiceConfiguration>().TypedFile;
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.LogProbe):
                            // not supported yet
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.SpanProbe):
                            // not supported yet
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception exception)
                {
                    Log.Warning($"Failed to deserialize configuration with path {configContent.Path.Path}", exception);
                }
            }

            var probeConfiguration = new ProbeConfiguration()
            {
                ServiceConfiguration = serviceConfig,
                MetricProbes = metrics.ToArray(),
                SnapshotProbes = snapshots.ToArray()
            };

            _configurationUpdater.AcceptAdded(probeConfiguration);
        }

        private void AcceptRemovedConfiguration(ProductConfigChangedEventArgs args)
        {
            var removedIds = args.ConfigContents
                   .Select(TrimProbeTypeFromPath)
                   .ToArray();

            _configurationUpdater.AcceptRemoved(removedIds);

            string TrimProbeTypeFromPath(NamedRawFile file)
            {
                return file.Path.Id.Split('_').Last();
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

        private void DiscoveryCallback(AgentConfiguration x)
            => _isRcmAvailable = !string.IsNullOrEmpty(x.ConfigurationEndpoint);
    }
}

internal record BoundLineProbeLocation
{
    public BoundLineProbeLocation(ProbeDefinition probe, Guid mvid, int methodToken, int bytecodeOffset, int lineNumber)
    {
        ProbeDefinition = probe;
        MVID = mvid;
        MethodToken = methodToken;
        BytecodeOffset = bytecodeOffset;
        LineNumber = lineNumber;
    }

    public ProbeDefinition ProbeDefinition { get; set; }

    public Guid MVID { get; set; }

    public int MethodToken { get; set; }

    public int BytecodeOffset { get; set; }

    public int LineNumber { get; set; }
}
