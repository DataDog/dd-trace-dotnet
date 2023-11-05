// <copyright file="LiveDebugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // FileMayOnlyContainASingleType - StyleCop did not enforce this for records initially

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Models;
using Datadog.Trace.Debugger.PInvoke;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Debugger
{
    internal class LiveDebugger
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LiveDebugger));
        private static readonly object GlobalLock = new();

        private readonly DebuggerSettings _settings;
        private readonly IDiscoveryService _discoveryService;
        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly ISubscription _subscription;
        private readonly IDebuggerSink _debuggerSink;
        private readonly ILineProbeResolver _lineProbeResolver;
        private readonly List<ProbeDefinition> _unboundProbes;
        private readonly IProbeStatusPoller _probeStatusPoller;
        private readonly ConfigurationUpdater _configurationUpdater;
        private readonly IDogStatsd _dogStats;
        private readonly object _instanceLock = new();
        private bool _isInitialized;
        private bool _isRcmAvailable;

        private LiveDebugger(
            DebuggerSettings settings,
            string serviceName,
            IDiscoveryService discoveryService,
            IRcmSubscriptionManager remoteConfigurationManager,
            ILineProbeResolver lineProbeResolver,
            IDebuggerSink debuggerSink,
            IProbeStatusPoller probeStatusPoller,
            ConfigurationUpdater configurationUpdater,
            IDogStatsd dogStats)
        {
            _settings = settings;
            _discoveryService = discoveryService;
            _lineProbeResolver = lineProbeResolver;
            _debuggerSink = debuggerSink;
            _probeStatusPoller = probeStatusPoller;
            _subscriptionManager = remoteConfigurationManager;
            _configurationUpdater = configurationUpdater;
            _dogStats = dogStats;
            _unboundProbes = new List<ProbeDefinition>();
            ServiceName = serviceName;
            _subscription = new Subscription(
                (updates, removals) =>
                {
                    AcceptAddedConfiguration(updates.Values.SelectMany(u => u).Select(i => new NamedRawFile(i.Path, i.Contents)));
                    AcceptRemovedConfiguration(removals.Values.SelectMany(u => u));
                    return Enumerable.Empty<ApplyDetails>();
                },
                RcmProducts.LiveDebugging);
            discoveryService?.SubscribeToChanges(DiscoveryCallback);
        }

        public static LiveDebugger Instance { get; private set; }

        public string ServiceName { get; }

        public static LiveDebugger Create(
            DebuggerSettings settings,
            string serviceName,
            IDiscoveryService discoveryService,
            IRcmSubscriptionManager remoteConfigurationManager,
            ILineProbeResolver lineProbeResolver,
            IDebuggerSink debuggerSink,
            IProbeStatusPoller probeStatusPoller,
            ConfigurationUpdater configurationUpdater,
            IDogStatsd dogStats)
        {
            lock (GlobalLock)
            {
                return Instance ??= new LiveDebugger(settings, serviceName, discoveryService, remoteConfigurationManager, lineProbeResolver, debuggerSink, probeStatusPoller, configurationUpdater, dogStats);
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
                _subscriptionManager.SubscribeToChanges(_subscription);

                DebuggerSnapshotSerializer.SetConfig(_settings);
                Redaction.SetConfig(_settings);
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
                    Log.Warning("Live Debugger could not be enabled because Remote Configuration Management is not available. Please ensure that you are using datadog-agent version 7.41.1 or higher, and that Remote Configuration Management is enabled in datadog-agent's yaml configuration file.");
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
                LifetimeManager.Instance.AddShutdownTask(_dogStats.Dispose);
                LifetimeManager.Instance.AddShutdownTask(() => _subscriptionManager.Unsubscribe(_subscription));
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

                Log.Information<int>("Live Debugger.InstrumentProbes: Request to instrument {Count} probes definitions", addedProbes.Count);

                var methodProbes = new List<NativeMethodProbeDefinition>();
                var lineProbes = new List<NativeLineProbeDefinition>();
                var spanProbes = new List<NativeSpanProbeDefinition>();

                var fetchProbeStatus = new List<FetchProbeStatus>();

                foreach (var probe in addedProbes)
                {
                    switch (GetProbeLocationType(probe))
                    {
                        case ProbeLocationType.Line:
                            var lineProbeResult = _lineProbeResolver.TryResolveLineProbe(probe, out var location);
                            var status = lineProbeResult.Status;
                            var message = lineProbeResult.Message;

                            Log.Information("Finished resolving line probe for ProbeID {ProbeID}. Result was '{Status}'. Message was: '{Message}'", probe.Id, status, message);
                            switch (status)
                            {
                                case LiveProbeResolveStatus.Bound:
                                    lineProbes.Add(new NativeLineProbeDefinition(location.ProbeDefinition.Id, location.MVID, location.MethodToken, (int)location.BytecodeOffset, location.LineNumber, location.ProbeDefinition.Where.SourceFile));
                                    fetchProbeStatus.Add(new FetchProbeStatus(probe.Id));
                                    break;
                                case LiveProbeResolveStatus.Unbound:
                                    Log.Information("ProbeID {ProbeID} is unbound.", probe.Id);
                                    _unboundProbes.Add(probe);

                                    fetchProbeStatus.Add(new FetchProbeStatus(probe.Id, new ProbeStatus(probe.Id, Sink.Models.Status.RECEIVED, errorMessage: null)));
                                    break;
                                case LiveProbeResolveStatus.Error:
                                    fetchProbeStatus.Add(new FetchProbeStatus(probe.Id, new ProbeStatus(probe.Id, Sink.Models.Status.ERROR, errorMessage: message)));
                                    break;
                            }

                            break;
                        case ProbeLocationType.Method:
                            fetchProbeStatus.Add(new FetchProbeStatus(probe.Id));
                            if (probe is SpanProbe)
                            {
                                var spanDefinition = new NativeSpanProbeDefinition(probe.Id, probe.Where.TypeName, probe.Where.MethodName, probe.Where.Signature?.Split(separator: ','));
                                spanProbes.Add(spanDefinition);
                            }
                            else
                            {
                                var nativeDefinition = new NativeMethodProbeDefinition(probe.Id, probe.Where.TypeName, probe.Where.MethodName, probe.Where.Signature?.Split(separator: ','));
                                methodProbes.Add(nativeDefinition);
                            }

                            break;
                        case ProbeLocationType.Unrecognized:
                            fetchProbeStatus.Add(new FetchProbeStatus(probe.Id, new ProbeStatus(probe.Id, Sink.Models.Status.ERROR, errorMessage: "Unknown probe type")));
                            break;
                    }
                }

                using var disposableMethodProbes = new DisposableEnumerable<NativeMethodProbeDefinition>(methodProbes);
                using var disposableSpanProbes = new DisposableEnumerable<NativeSpanProbeDefinition>(spanProbes);
                DebuggerNativeMethods.InstrumentProbes(methodProbes.ToArray(), lineProbes.ToArray(), spanProbes.ToArray(), Array.Empty<NativeRemoveProbeRequest>());

                _probeStatusPoller.AddProbes(fetchProbeStatus.ToArray());

                foreach (var probe in addedProbes.Where(probe => probe is not SpanProbe))
                {
                    ProbeExpressionsProcessor.Instance.AddProbeProcessor(probe);
                    if (probe is LogProbe logProbe)
                    {
                        if (logProbe.Sampling is { } sampling)
                        {
                            ProbeRateLimiter.Instance.SetRate(probe.Id, (int)sampling.SnapshotsPerSecond);
                        }
                        else
                        {
                            ProbeRateLimiter.Instance.SetRate(probe.Id, logProbe.CaptureSnapshot ? 1 : 5000);
                        }
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

                Log.Information<int>("Live Debugger.InstrumentProbes: Request to remove {Length} probes.", removedProbesIds.Length);

                RemoveUnboundProbes(removedProbesIds);

                var probesToRemoveFromNative = _probeStatusPoller.GetFetchedProbes(removedProbesIds);
                _probeStatusPoller.RemoveProbes(removedProbesIds);

                if (probesToRemoveFromNative.Any())
                {
                    var revertProbes = probesToRemoveFromNative
                       .Select(probeId => new NativeRemoveProbeRequest(probeId));

                    DebuggerNativeMethods.InstrumentProbes(Array.Empty<NativeMethodProbeDefinition>(), Array.Empty<NativeLineProbeDefinition>(), Array.Empty<NativeSpanProbeDefinition>(), revertProbes.ToArray());
                }

                foreach (var id in removedProbesIds)
                {
                    ProbeRateLimiter.Instance.ResetRate(id);
                    ProbeExpressionsProcessor.Instance.Remove(id);
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

                // Initialize these lists only when there is at least one unbound probe that becomes bound, to reduce unnecessary allocations.
                List<NativeLineProbeDefinition> lineProbes = null;
                List<ProbeDefinition> noLongerUnboundProbes = null;

                foreach (var unboundProbe in _unboundProbes)
                {
                    var result = _lineProbeResolver.TryResolveLineProbe(unboundProbe, out var location);
                    if (result.Status == LiveProbeResolveStatus.Bound)
                    {
                        lineProbes ??= new List<NativeLineProbeDefinition>();
                        noLongerUnboundProbes ??= new List<ProbeDefinition>();

                        noLongerUnboundProbes.Add(unboundProbe);
                        lineProbes.Add(new NativeLineProbeDefinition(location.ProbeDefinition.Id, location.MVID, location.MethodToken, (int)location.BytecodeOffset, location.LineNumber, location.ProbeDefinition.Where.SourceFile));
                    }
                }

                if (lineProbes?.Any() == true)
                {
                    Log.Information<int>("LiveDebugger.CheckUnboundProbes: {Count} unbound probes became bound.", noLongerUnboundProbes.Count);
                    DebuggerNativeMethods.InstrumentProbes(Array.Empty<NativeMethodProbeDefinition>(), lineProbes.ToArray(), Array.Empty<NativeSpanProbeDefinition>(), Array.Empty<NativeRemoveProbeRequest>());

                    foreach (var boundProbe in noLongerUnboundProbes)
                    {
                        _unboundProbes.Remove(boundProbe);
                    }

                    // Update probe statuses

                    var probeIds = noLongerUnboundProbes.Select(p => p.Id).ToArray();
                    var newProbeStatuses = noLongerUnboundProbes.Select(p => new FetchProbeStatus(p.Id)).ToArray();

                    _probeStatusPoller.UpdateProbes(probeIds, newProbeStatuses);
                }
            }
        }

        private void AcceptAddedConfiguration(IEnumerable<NamedRawFile> configContents)
        {
            var logs = new List<LogProbe>();
            var metrics = new List<MetricProbe>();
            var spanDecoration = new List<SpanDecorationProbe>();
            var spans = new List<SpanProbe>();
            ServiceConfiguration serviceConfig = null;

            foreach (var configContent in configContents)
            {
                try
                {
                    switch (configContent.Path.Id)
                    {
                        case { } id when id.StartsWith(DefinitionPaths.LogProbe):
                            logs.Add(configContent.Deserialize<LogProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.MetricProbe):
                            metrics.Add(configContent.Deserialize<MetricProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.SpanDecorationProbe):
                            spanDecoration.Add(configContent.Deserialize<SpanDecorationProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.SpanProbe):
                            spans.Add(configContent.Deserialize<SpanProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.ServiceConfiguration):
                            serviceConfig = configContent.Deserialize<ServiceConfiguration>().TypedFile;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception exception)
                {
                    Log.Warning(exception, "Failed to deserialize configuration with path {Path}", configContent.Path.Path);
                }
            }

            var probeConfiguration = new ProbeConfiguration() { ServiceConfiguration = serviceConfig, MetricProbes = metrics.ToArray(), SpanDecorationProbes = spanDecoration.ToArray(), LogProbes = logs.ToArray(), SpanProbes = spans.ToArray() };

            _configurationUpdater.AcceptAdded(probeConfiguration);
        }

        private void AcceptRemovedConfiguration(IEnumerable<RemoteConfigurationPath> paths)
        {
            var removedIds = paths
                            .Select(TrimProbeTypeFromPath)
                            .ToArray();

            _configurationUpdater.AcceptRemoved(removedIds);

            string TrimProbeTypeFromPath(RemoteConfigurationPath path)
            {
                return path.Id.Split('_').Last();
            }
        }

        internal void AddSnapshot(string probeId, string snapshot)
        {
            _debuggerSink.AddSnapshot(probeId, snapshot);
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

        internal void SendMetrics(MetricKind metricKind, string metricName, double value)
        {
            if (_dogStats is NoOpStatsd)
            {
                Log.Warning($"{nameof(SendMetrics)}: Metrics are not enabled");
            }

            switch (metricKind)
            {
                case MetricKind.COUNT:
                    _dogStats.Counter(statName: metricName, value: value);
                    break;
                case MetricKind.GAUGE:
                    _dogStats.Gauge(statName: metricName, value: value);
                    break;
                case MetricKind.HISTOGRAM:
                    _dogStats.Histogram(statName: metricName, value: value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(metricKind),
                        $"{metricKind} is not a valid value");
            }
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

#pragma warning restore SA1402 // FileMayOnlyContainASingleType - StyleCop did not enforce this for records initially
