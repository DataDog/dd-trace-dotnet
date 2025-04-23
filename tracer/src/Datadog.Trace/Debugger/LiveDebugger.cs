// <copyright file="LiveDebugger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // FileMayOnlyContainASingleType - StyleCop did not enforce this for records initially
#nullable disable

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
using ProbeInfo = Datadog.Trace.Debugger.Expressions.ProbeInfo;

namespace Datadog.Trace.Debugger
{
    internal class LiveDebugger
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LiveDebugger));
        private static readonly object GlobalLock = new();

        private readonly IDiscoveryService _discoveryService;
        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly ISubscription _subscription;
        private readonly ISnapshotUploader _snapshotUploader;
        private readonly IDebuggerUploader _diagnosticsUploader;
        private readonly IDebuggerUploader _symbolsUploader;
        private readonly ILineProbeResolver _lineProbeResolver;
        private readonly List<ProbeDefinition> _unboundProbes;
        private readonly IProbeStatusPoller _probeStatusPoller;
        private readonly ConfigurationUpdater _configurationUpdater;
        private readonly IDogStatsd _dogStats;
        private readonly object _instanceLock = new();
        private bool _isRcmAvailable;

        private LiveDebugger(
            DebuggerSettings settings,
            string serviceName,
            IDiscoveryService discoveryService,
            IRcmSubscriptionManager remoteConfigurationManager,
            ILineProbeResolver lineProbeResolver,
            ISnapshotUploader snapshotUploader,
            IDebuggerUploader diagnosticsUploader,
            IDebuggerUploader symbolsUploader,
            IProbeStatusPoller probeStatusPoller,
            ConfigurationUpdater configurationUpdater,
            IDogStatsd dogStats)
        {
            Settings = settings;
            _discoveryService = discoveryService;
            _lineProbeResolver = lineProbeResolver;
            _snapshotUploader = snapshotUploader;
            _diagnosticsUploader = diagnosticsUploader;
            _symbolsUploader = symbolsUploader;
            _probeStatusPoller = probeStatusPoller;
            _subscriptionManager = remoteConfigurationManager;
            _configurationUpdater = configurationUpdater;
            _dogStats = dogStats;
            _unboundProbes = new List<ProbeDefinition>();
            ServiceName = serviceName;
            _subscription = new Subscription(
                (updates, removals) =>
                {
                    var addedResult = AcceptAddedConfiguration(updates.Values.SelectMany(u => u).ToList());
                    var removedResult = AcceptRemovedConfiguration(removals.Values.SelectMany(u => u).ToList());
                    return addedResult.Concat(removedResult).ToArray();
                },
                RcmProducts.LiveDebugging);
            discoveryService?.SubscribeToChanges(DiscoveryCallback);
        }

        public static LiveDebugger Instance { get; private set; }

        public bool IsInitialized { get; private set; }

        public string ServiceName { get; }

        internal DebuggerSettings Settings { get; }

        public static LiveDebugger Create(
            DebuggerSettings settings,
            string serviceName,
            IDiscoveryService discoveryService,
            IRcmSubscriptionManager remoteConfigurationManager,
            ILineProbeResolver lineProbeResolver,
            ISnapshotUploader snapshotUploader,
            IDebuggerUploader diagnosticsUploader,
            IDebuggerUploader symbolsUploader,
            IProbeStatusPoller probeStatusPoller,
            ConfigurationUpdater configurationUpdater,
            IDogStatsd dogStats)
        {
            lock (GlobalLock)
            {
                return Instance ??=
                           new LiveDebugger(
                               settings: settings,
                               serviceName: serviceName,
                               discoveryService: discoveryService,
                               remoteConfigurationManager: remoteConfigurationManager,
                               lineProbeResolver: lineProbeResolver,
                               snapshotUploader: snapshotUploader,
                               diagnosticsUploader: diagnosticsUploader,
                               symbolsUploader: symbolsUploader,
                               probeStatusPoller: probeStatusPoller,
                               configurationUpdater: configurationUpdater,
                               dogStats: dogStats);
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

                IsInitialized = true;
            }

            try
            {
                Log.Information("Live Debugger initialization started");
                _subscriptionManager.SubscribeToChanges(_subscription);

                DebuggerSnapshotSerializer.SetConfig(Settings);
                Redaction.Instance.SetConfig(Settings.RedactedIdentifiers, Settings.RedactedExcludedIdentifiers, Settings.RedactedTypes);
                AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => CheckUnboundProbes();

                await StartAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log.Error(e, "Initializing Live Debugger failed.");
            }

            bool CanInitialize()
            {
                if (IsInitialized)
                {
                    return false;
                }

                if (!Settings.Enabled)
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
                LifetimeManager.Instance.AddShutdownTask(ShutdownTask);

                _probeStatusPoller.StartPolling();
                _symbolsUploader.StartFlushingAsync();
                _diagnosticsUploader.StartFlushingAsync();
                return _snapshotUploader.StartFlushingAsync();
            }

            void ShutdownTask(Exception ex)
            {
                _discoveryService.RemoveSubscription(DiscoveryCallback);
                _snapshotUploader.Dispose();
                _diagnosticsUploader.Dispose();
                _symbolsUploader.Dispose();
                _probeStatusPoller.Dispose();
                _subscriptionManager.Unsubscribe(_subscription);
                _dogStats.Dispose();
            }
        }

        internal List<UpdateResult> UpdateAddedProbeInstrumentations(IReadOnlyList<ProbeDefinition> addedProbes)
        {
            if (addedProbes.Count == 0)
            {
                return [];
            }

            Log.Information<int>("Live Debugger.InstrumentProbes: Request to instrument {Count} probes definitions", addedProbes.Count);

            var result = new List<UpdateResult>(addedProbes.Count);

            lock (_instanceLock)
            {
                var methodProbes = new List<NativeMethodProbeDefinition>();
                var lineProbes = new List<NativeLineProbeDefinition>();
                var spanProbes = new List<NativeSpanProbeDefinition>();

                var fetchProbeStatus = new List<FetchProbeStatus>();

                foreach (var addedProbe in addedProbes)
                {
                    try
                    {
                        switch (GetProbeLocationType(addedProbe))
                        {
                            case ProbeLocationType.Line:
                                {
                                    var lineProbeResult = _lineProbeResolver.TryResolveLineProbe(addedProbe, out var location);
                                    var status = lineProbeResult.Status;
                                    var message = lineProbeResult.Message;

                                    Log.Information("Finished resolving line probe for ProbeID {ProbeID}. Result was '{Status}'. Message was: '{Message}'", addedProbe.Id, status, message);
                                    switch (status)
                                    {
                                        case LiveProbeResolveStatus.Bound:
                                            lineProbes.Add(new NativeLineProbeDefinition(location.ProbeDefinition.Id, location.MVID, location.MethodToken, (int)location.BytecodeOffset, location.LineNumber, location.ProbeDefinition.Where.SourceFile));
                                            fetchProbeStatus.Add(new FetchProbeStatus(addedProbe.Id, addedProbe.Version ?? 0));
                                            ProbeExpressionsProcessor.Instance.AddProbeProcessor(addedProbe);
                                            SetRateLimit(addedProbe);
                                            break;
                                        case LiveProbeResolveStatus.Unbound:
                                            Log.Information("ProbeID {ProbeID} is unbound.", addedProbe.Id);
                                            _unboundProbes.Add(addedProbe);
                                            fetchProbeStatus.Add(new FetchProbeStatus(addedProbe.Id, addedProbe.Version ?? 0, new ProbeStatus(addedProbe.Id, Sink.Models.Status.RECEIVED, errorMessage: null)));
                                            break;
                                        case LiveProbeResolveStatus.Error:
                                            fetchProbeStatus.Add(new FetchProbeStatus(addedProbe.Id, addedProbe.Version ?? 0, new ProbeStatus(addedProbe.Id, Sink.Models.Status.ERROR, errorMessage: message)));
                                            break;
                                    }

                                    break;
                                }

                            case ProbeLocationType.Method:
                                {
                                    SignatureParser.TryParse(addedProbe.Where.Signature, out var signature);

                                    fetchProbeStatus.Add(new FetchProbeStatus(addedProbe.Id, addedProbe.Version ?? 0));
                                    if (addedProbe is SpanProbe)
                                    {
                                        var spanDefinition = new NativeSpanProbeDefinition(addedProbe.Id, addedProbe.Where.TypeName, addedProbe.Where.MethodName, signature);
                                        spanProbes.Add(spanDefinition);
                                    }
                                    else
                                    {
                                        var nativeDefinition = new NativeMethodProbeDefinition(addedProbe.Id, addedProbe.Where.TypeName, addedProbe.Where.MethodName, signature);
                                        methodProbes.Add(nativeDefinition);
                                        ProbeExpressionsProcessor.Instance.AddProbeProcessor(addedProbe);
                                        SetRateLimit(addedProbe);
                                    }

                                    break;
                                }

                            case ProbeLocationType.Unrecognized:
                                fetchProbeStatus.Add(new FetchProbeStatus(addedProbe.Id, addedProbe.Version ?? 0, new ProbeStatus(addedProbe.Id, Sink.Models.Status.ERROR, errorMessage: "Unknown probe type")));
                                result.Add(new UpdateResult(addedProbe.Id, "Unknown probe type"));
                                break;
                        }

                        result.Add(new UpdateResult(addedProbe.Id, null));
                    }
                    catch (Exception e)
                    {
                        result.Add(new UpdateResult(addedProbe.Id, e.Message));
                    }
                }

                using var disposableMethodProbes = new DisposableEnumerable<NativeMethodProbeDefinition>(methodProbes);
                using var disposableSpanProbes = new DisposableEnumerable<NativeSpanProbeDefinition>(spanProbes);
                DebuggerNativeMethods.InstrumentProbes(methodProbes.ToArray(), lineProbes.ToArray(), spanProbes.ToArray(), []);

                var probeIds = fetchProbeStatus.Select(fp => fp.ProbeId).ToArray();
                _probeStatusPoller.UpdateProbes(probeIds, fetchProbeStatus.ToArray());

                // This log entry is being checked in integration test
                Log.Information("Live Debugger.InstrumentProbes: Request to instrument added probes definitions completed.");

                return result;
            }
        }

        private static void SetRateLimit(ProbeDefinition probe)
        {
            if (probe is not LogProbe logProbe)
            {
                return;
            }

            if (logProbe.Sampling is { } sampling)
            {
                ProbeRateLimiter.Instance.SetRate(probe.Id, (int)sampling.SnapshotsPerSecond);
            }
            else
            {
                ProbeRateLimiter.Instance.SetRate(probe.Id, logProbe.CaptureSnapshot ? 1 : 5000);
            }
        }

        internal ApplyDetails[] UpdateRemovedProbeInstrumentations(List<RemoteConfigurationPath> paths)
        {
            var removedProbesIds = paths
                                  .Select(TrimProbeTypeFromPath)
                                  .ToArray();
            string TrimProbeTypeFromPath(RemoteConfigurationPath path)
            {
                return path.Id.Split('_').Last();
            }

            if (removedProbesIds.Length == 0)
            {
                return [];
            }

            Log.Information<int>("Live Debugger.InstrumentProbes: Request to remove {Length} probes.", removedProbesIds.Length);
            var result = new ApplyDetails[paths.Count];

            lock (_instanceLock)
            {
                var boundedProbes = _probeStatusPoller.GetBoundedProbes();
                var probesToRemoveFromNative = new List<string>();
                for (var i = 0; i < removedProbesIds.Length; i++)
                {
                    var id = removedProbesIds[i];
                    try
                    {
                        _unboundProbes.RemoveAll(pd => pd.Id == id);

                        _probeStatusPoller.RemoveProbes(removedProbesIds);

                        ProbeRateLimiter.Instance.ResetRate(id);
                        ProbeExpressionsProcessor.Instance.Remove(id);

                        if (boundedProbes.Contains(id))
                        {
                            probesToRemoveFromNative.Add(id);
                            result[i] = ApplyDetails.FromOk(paths[i].Path);
                        }
                        else
                        {
                            result[i] = ApplyDetails.FromError(paths[i].Path, "Probe ID does not have a native representation so no request revert will call for him");
                        }
                    }
                    catch (Exception e)
                    {
                        result[i] = ApplyDetails.FromError(paths[i].Path, e.Message);
                        Log.Error(e, "Error remove probe {ID} instrumentation", id);
                    }
                }

                if (probesToRemoveFromNative.Any())
                {
                    var revertProbes = probesToRemoveFromNative
                       .Select(probeId => new NativeRemoveProbeRequest(probeId));

                    DebuggerNativeMethods.InstrumentProbes([], [], [], revertProbes.ToArray());
                }

                // This log entry is being checked in integration test
                Log.Information("Live Debugger.InstrumentProbes: Request to de-instrument probes definitions completed.");
            }

            return result;
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
                        ProbeExpressionsProcessor.Instance.AddProbeProcessor(boundProbe);
                        SetRateLimit(boundProbe);
                        _unboundProbes.Remove(boundProbe);
                    }

                    // Update probe statuses

                    var probeIds = noLongerUnboundProbes.Select(p => p.Id).ToArray();
                    var newProbeStatuses = noLongerUnboundProbes.Select(p => new FetchProbeStatus(p.Id, p.Version ?? 0)).ToArray();

                    _probeStatusPoller.UpdateProbes(probeIds, newProbeStatuses);
                }
            }
        }

        private ApplyDetails[] AcceptAddedConfiguration(List<RemoteConfiguration> configs)
        {
            var logs = new List<LogProbe>();
            var metrics = new List<MetricProbe>();
            var spanDecoration = new List<SpanDecorationProbe>();
            var spans = new List<SpanProbe>();
            ServiceConfiguration serviceConfig = null;

            var result = new List<ApplyDetails>(configs.Count);

            for (int i = 0; i < configs.Count; i++)
            {
                var config = configs[i];
                var namedRawFile = new NamedRawFile(config.Path, config.Contents);
                try
                {
                    switch (namedRawFile.Path.Id)
                    {
                        case { } id when id.StartsWith(DefinitionPaths.LogProbe):
                            logs.Add(namedRawFile.Deserialize<LogProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.MetricProbe):
                            metrics.Add(namedRawFile.Deserialize<MetricProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.SpanDecorationProbe):
                            spanDecoration.Add(namedRawFile.Deserialize<SpanDecorationProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.SpanProbe):
                            spans.Add(namedRawFile.Deserialize<SpanProbe>().TypedFile);
                            break;
                        case { } id when id.StartsWith(DefinitionPaths.ServiceConfiguration):
                            serviceConfig = namedRawFile.Deserialize<ServiceConfiguration>().TypedFile;
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Failed to deserialize configuration with path {Path}", namedRawFile.Path.Path);
                    result.Add(ApplyDetails.FromError(namedRawFile.Path.Path, exception.Message));
                }
            }

            var probeConfiguration = new ProbeConfiguration()
            {
                ServiceConfiguration = serviceConfig,
                MetricProbes = metrics.ToArray(),
                SpanDecorationProbes = spanDecoration.ToArray(),
                LogProbes = logs.ToArray(),
                SpanProbes = spans.ToArray()
            };

            try
            {
                var updateResults = _configurationUpdater.AcceptAdded(probeConfiguration);
                foreach (var updateResult in updateResults)
                {
                    var config = configs.FirstOrDefault(c => c.Path.Id == updateResult.Id);
                    if (config != null)
                    {
                        result.Add(updateResult.Error == null 
                                       ? ApplyDetails.FromOk(config.Path.Path) 
                                       : ApplyDetails.FromError(config.Path.Path, updateResult.Error));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to add configurations");
            }

            return result.ToArray();
        }

        private ApplyDetails[] AcceptRemovedConfiguration(List<RemoteConfigurationPath> paths)
        {
            return _configurationUpdater.AcceptRemoved(paths);
        }

        internal void AddSnapshot(ProbeInfo probe, string snapshot)
        {
            _snapshotUploader.Add(probe.ProbeId, snapshot);
            SetProbeStatusToEmitting(probe);
        }

        internal void SetProbeStatusToEmitting(ProbeInfo probe)
        {
            if (!probe.IsEmitted)
            {
                var probeStatus = new ProbeStatus(probe.ProbeId, Sink.Models.Status.EMITTING);
                var fetchProbeStatus = new FetchProbeStatus(probe.ProbeId, probe.ProbeVersion, probeStatus);
                _probeStatusPoller.UpdateProbe(probe.ProbeId, fetchProbeStatus);
                probe.IsEmitted = true;
            }
        }

        internal void SendMetrics(ProbeInfo probe, MetricKind metricKind, string metricName, double value, string probeId)
        {
            if (_dogStats is NoOpStatsd)
            {
                Log.Warning($"{nameof(SendMetrics)}: Metrics are not enabled");
            }

            switch (metricKind)
            {
                case MetricKind.COUNT:
                    _dogStats.Counter(statName: metricName, value: value, tags: new[] { $"probe-id:{probeId}" });
                    break;
                case MetricKind.GAUGE:
                    _dogStats.Gauge(statName: metricName, value: value, tags: new[] { $"probe-id:{probeId}" });
                    break;
                case MetricKind.HISTOGRAM:
                    _dogStats.Histogram(statName: metricName, value: value, tags: new[] { $"probe-id:{probeId}" });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(metricKind),
                        $"{metricKind} is not a valid value");
            }

            SetProbeStatusToEmitting(probe);
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
