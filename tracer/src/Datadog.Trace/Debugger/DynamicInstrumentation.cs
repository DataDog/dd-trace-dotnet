// <copyright file="DynamicInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1402 // FileMayOnlyContainASingleType - StyleCop did not enforce this for records initially
#nullable enable

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
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.StatsdClient;
using ProbeInfo = Datadog.Trace.Debugger.Expressions.ProbeInfo;

namespace Datadog.Trace.Debugger
{
    internal sealed class DynamicInstrumentation : IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DynamicInstrumentation));

        private readonly TaskCompletionSource<bool> _processExit;
        private readonly IDiscoveryService _discoveryService;
        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly ISubscription _subscription;
        private readonly ISnapshotUploader _snapshotUploader;
        private readonly ISnapshotUploader _logUploader;
        private readonly IDebuggerUploader _diagnosticsUploader;
        private readonly ILineProbeResolver _lineProbeResolver;
        private readonly List<ProbeDefinition> _unboundProbes;
        private readonly IProbeStatusPoller _probeStatusPoller;
        private readonly ConfigurationUpdater _configurationUpdater;
        private readonly IDogStatsd _dogStats;
        private readonly DebuggerSettings _settings;
        private readonly object _instanceLock = new();
        private int _disposeState;

        internal DynamicInstrumentation(
            DebuggerSettings settings,
            IDiscoveryService discoveryService,
            IRcmSubscriptionManager remoteConfigurationManager,
            ILineProbeResolver lineProbeResolver,
            ISnapshotUploader snapshotUploader,
            ISnapshotUploader logUploader,
            IDebuggerUploader diagnosticsUploader,
            IProbeStatusPoller probeStatusPoller,
            ConfigurationUpdater configurationUpdater,
            IDogStatsd dogStats)
        {
            Log.Information("Initializing Dynamic Instrumentation");
            _settings = settings;
            _processExit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _discoveryService = discoveryService;
            _lineProbeResolver = lineProbeResolver;
            _snapshotUploader = snapshotUploader;
            _logUploader = logUploader;
            _diagnosticsUploader = diagnosticsUploader;
            _probeStatusPoller = probeStatusPoller;
            _subscriptionManager = remoteConfigurationManager;
            _configurationUpdater = configurationUpdater;
            _dogStats = dogStats;
            _unboundProbes = new List<ProbeDefinition>();
            _subscription = new Subscription(
                (updates, removals) =>
                {
                    var addedResult = AcceptAddedConfiguration(updates?.Values.SelectMany(u => u).ToList());
                    AcceptRemovedConfiguration(removals?.Values.SelectMany(u => u).ToList());
                    return addedResult;
                },
                RcmProducts.LiveDebugging);
        }

        public bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

        public bool IsInitialized { get; private set; }

        internal void Initialize()
        {
            if (!_settings.DynamicInstrumentationEnabled)
            {
                return;
            }

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var isRcmAvailable = await WaitForRcmAvailabilityAsync().ConfigureAwait(false);
                if (!isRcmAvailable)
                {
                    return;
                }

                _subscriptionManager.SubscribeToChanges(_subscription);
                AppDomain.CurrentDomain.AssemblyLoad += CheckUnboundProbes;
                StartBackgroundProcess();
                IsInitialized = true;
                Log.Information("Dynamic Instrumentation initialization completed successfully");
            }
            catch (OperationCanceledException e)
            {
                Log.Debug(e, "Dynamic Instrumentation stopped due task cancellation");
            }
            catch (Exception e)
            {
                Log.Error(e, "Dynamic Instrumentation initialization failed");
            }
        }

        private void StartBackgroundProcess()
        {
            _probeStatusPoller.StartPolling();

            _ = _diagnosticsUploader.StartFlushingAsync()
                                    .ContinueWith(
                                         t => Log.Error(t?.Exception, "Error in diagnostic uploader"),
                                         CancellationToken.None,
                                         TaskContinuationOptions.OnlyOnFaulted,
                                         TaskScheduler.Default);

            _ = _snapshotUploader.StartFlushingAsync()
                                 .ContinueWith(
                                      t => Log.Error(t?.Exception, "Error in snapshot uploader"),
                                      CancellationToken.None,
                                      TaskContinuationOptions.OnlyOnFaulted,
                                      TaskScheduler.Default);

            _ = _logUploader.StartFlushingAsync()
                            .ContinueWith(
                                 t => Log.Error(t?.Exception, "Error in log uploader"),
                                 CancellationToken.None,
                                 TaskContinuationOptions.OnlyOnFaulted,
                                 TaskScheduler.Default);
        }

        internal List<ConfigurationUpdater.UpdateResult> UpdateAddedProbeInstrumentations(IReadOnlyList<ProbeDefinition> addedProbes)
        {
            if (IsDisposed)
            {
                return [];
            }

            lock (_instanceLock)
            {
                if (addedProbes.Count == 0)
                {
                    return [];
                }

                Log.Information<int>("Dynamic Instrumentation.InstrumentProbes: Request to instrument {Count} probes definitions", addedProbes.Count);
                var result = new List<ConfigurationUpdater.UpdateResult>(addedProbes.Count);

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
                                                lineProbes.Add(new NativeLineProbeDefinition(location!.ProbeDefinition.Id, location.Mvid, location.MethodToken, (int)location.BytecodeOffset, location.LineNumber, location.ProbeDefinition.Where.SourceFile));
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
                                                Log.Warning("ProbeID {ProbeID} error resolving live. Error: {Error}", addedProbe.Id, message);
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
                                    result.Add(new ConfigurationUpdater.UpdateResult(addedProbe.Id, "Unknown probe type"));
                                    break;
                            }

                            result.Add(new ConfigurationUpdater.UpdateResult(addedProbe.Id, null));
                        }
                        catch (Exception e)
                        {
                            result.Add(new ConfigurationUpdater.UpdateResult(addedProbe.Id, e.Message));
                        }
                    }

                    using var disposableMethodProbes = new DisposableEnumerable<NativeMethodProbeDefinition>(methodProbes);
                    using var disposableSpanProbes = new DisposableEnumerable<NativeSpanProbeDefinition>(spanProbes);
                    DebuggerNativeMethods.InstrumentProbes(methodProbes.ToArray(), lineProbes.ToArray(), spanProbes.ToArray(), []);

                    var probeIds = fetchProbeStatus.Select(fp => fp.ProbeId).ToArray();
                    _probeStatusPoller.UpdateProbes(probeIds, fetchProbeStatus.ToArray());

                    // This log entry is being checked in integration test
                    Log.Information("Dynamic Instrumentation.InstrumentProbes: Request to instrument added probes definitions completed.");

                    return result;
                }
            }
        }

        private static void SetRateLimit(ProbeDefinition probe)
        {
            switch (probe)
            {
                case LogProbe { Sampling: { } sampling }:
                    ProbeRateLimiter.Instance.SetRate(probe.Id, (int)sampling.SnapshotsPerSecond);
                    break;
                case LogProbe logProbe:
                    ProbeRateLimiter.Instance.SetRate(probe.Id, logProbe.CaptureSnapshot ? 1 : 5000);
                    break;
                case SpanDecorationProbe or MetricProbe:
                    ProbeRateLimiter.Instance.TryAddSampler(probe.Id, NopAdaptiveSampler.Instance);
                    break;
            }
        }

        internal void UpdateRemovedProbeInstrumentations(List<RemoteConfigurationPath> paths)
        {
            if (IsDisposed)
            {
                return;
            }

            var removedProbesIds = paths
                                  .Select(TrimProbeTypeFromPath)
                                  .ToArray();
            string TrimProbeTypeFromPath(RemoteConfigurationPath path)
            {
                return path.Id.Split('_').Last();
            }

            if (removedProbesIds.Length == 0)
            {
                return;
            }

            Log.Information<int>("Dynamic Instrumentation.InstrumentProbes: Request to remove {Length} probes.", removedProbesIds.Length);

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
                        }
                    }
                    catch (Exception e)
                    {
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
                Log.Information("Dynamic Instrumentation.InstrumentProbes: Request to de-instrument probes definitions completed.");
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

        private void CheckUnboundProbes(object? sender, AssemblyLoadEventArgs args)
        {
            // A new assembly was loaded, so re-examine whether the probe can now be resolved.
            lock (_instanceLock)
            {
                if (_unboundProbes.Count == 0)
                {
                    return;
                }

                // Initialize these lists only when there is at least one unbound probe that becomes bound, to reduce unnecessary allocations.
                List<NativeLineProbeDefinition>? lineProbes = null;
                List<ProbeDefinition>? noLongerUnboundProbes = null;

                foreach (var unboundProbe in _unboundProbes)
                {
                    var result = _lineProbeResolver.TryResolveLineProbe(unboundProbe, out var location);
                    if (result.Status == LiveProbeResolveStatus.Bound)
                    {
                        lineProbes ??= new List<NativeLineProbeDefinition>();
                        noLongerUnboundProbes ??= new List<ProbeDefinition>();

                        noLongerUnboundProbes.Add(unboundProbe);
                        lineProbes.Add(new NativeLineProbeDefinition(location!.ProbeDefinition.Id, location.Mvid, location.MethodToken, (int)location.BytecodeOffset, location.LineNumber, location.ProbeDefinition.Where.SourceFile));
                    }
                }

                if (lineProbes?.Any() == true && noLongerUnboundProbes != null)
                {
                    Log.Information("Dynamic Instrumentation.CheckUnboundProbes: {Count} unbound probes became bound.", property: noLongerUnboundProbes.Count);

                    DebuggerNativeMethods.InstrumentProbes([], lineProbes.ToArray(), [], []);

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

        private ApplyDetails[] AcceptAddedConfiguration(List<RemoteConfiguration>? configs)
        {
            if (configs == null)
            {
                return [];
            }

            var logs = new List<LogProbe>();
            var metrics = new List<MetricProbe>();
            var spanDecoration = new List<SpanDecorationProbe>();
            var spans = new List<SpanProbe>();
            ServiceConfiguration? serviceConfig = null;

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
                            var logProbes = namedRawFile.Deserialize<LogProbe>().TypedFile;
                            if (logProbes != null)
                            {
                                logs.Add(logProbes);
                            }

                            break;
                        case { } id when id.StartsWith(DefinitionPaths.MetricProbe):
                            var metricProbes = namedRawFile.Deserialize<MetricProbe>().TypedFile;
                            if (metricProbes != null)
                            {
                                metrics.Add(metricProbes);
                            }

                            break;
                        case { } id when id.StartsWith(DefinitionPaths.SpanDecorationProbe):
                            var spanDecorationProbes = namedRawFile.Deserialize<SpanDecorationProbe>().TypedFile;
                            if (spanDecorationProbes != null)
                            {
                                spanDecoration.Add(spanDecorationProbes);
                            }

                            break;
                        case { } id when id.StartsWith(DefinitionPaths.SpanProbe):
                            var spanProbes = namedRawFile.Deserialize<SpanProbe>().TypedFile;
                            if (spanProbes != null)
                            {
                                spans.Add(spanProbes);
                            }

                            break;
                        case { } id when id.StartsWith(DefinitionPaths.ServiceConfiguration):
                            serviceConfig = namedRawFile.Deserialize<ServiceConfiguration>().TypedFile;
                            break;
                        default:
                            result.Add(ApplyDetails.FromError(namedRawFile.Path.Path, "Unknown config"));
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
                        result.Add(
                            updateResult.Error == null
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

        private void AcceptRemovedConfiguration(List<RemoteConfigurationPath>? paths)
        {
            if (paths == null)
            {
                return;
            }

            _configurationUpdater.AcceptRemoved(paths);
        }

        internal void AddSnapshot(ProbeInfo probe, string snapshot)
        {
            if (IsDisposed)
            {
                return;
            }

            if (!probe.IsFullSnapshot)
            {
                AddLog(probe, snapshot);
                return;
            }

            _snapshotUploader.Add(probe.ProbeId, snapshot);
            SetProbeStatusToEmitting(probe);
        }

        internal void AddLog(ProbeInfo probe, string log)
        {
            if (IsDisposed)
            {
                return;
            }

            _logUploader.Add(probe.ProbeId, log);
            SetProbeStatusToEmitting(probe);
        }

        internal void SetProbeStatusToEmitting(ProbeInfo probe)
        {
            if (IsDisposed)
            {
                return;
            }

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
            if (IsDisposed)
            {
                return;
            }

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

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Successfully sent metric {Metric}. ProbeId={ProbeId}", metricName, probeId);
            }

            SetProbeStatusToEmitting(probe);
        }

        private async Task<bool> WaitForRcmAvailabilityAsync()
        {
            var rcmAvailabilityTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _discoveryService.SubscribeToChanges(DiscoveryCallback);

            try
            {
                var rcmTimeout = TimeSpan.FromMinutes(5);
                var timeoutTask = Task.Delay(rcmTimeout);

                var completedTask = await Task.WhenAny(rcmAvailabilityTcs.Task, timeoutTask, _processExit.Task).ConfigureAwait(false);
                if (completedTask == timeoutTask)
                {
                    Log.Warning("Dynamic Instrumentation could not be enabled because Remote Configuration Management is not available after waiting {Timeout} seconds. Please note that Dynamic Instrumentation is not supported in all environments (e.g. AAS). Ensure that you are using datadog-agent version 7.41.1 or higher, and that Remote Configuration Management is enabled in datadog-agent's yaml configuration file.", rcmTimeout.TotalSeconds);
                    return false;
                }

                return completedTask == rcmAvailabilityTcs.Task;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while waiting for RCM availability");
                return false;
            }
            finally
            {
                _discoveryService.RemoveSubscription(DiscoveryCallback);
            }

            void DiscoveryCallback(AgentConfiguration x)
            {
                var isRcmAvailable = !string.IsNullOrEmpty(x.ConfigurationEndpoint);
                if (isRcmAvailable)
                {
                    rcmAvailabilityTcs.TrySetResult(true);
                }
            }
        }

        public void Dispose()
        {
            // Already disposed
            if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            {
                return;
            }

            if (_processExit.Task.IsCompleted)
            {
                return;
            }

            _processExit.TrySetResult(true);
            AppDomain.CurrentDomain.AssemblyLoad -= CheckUnboundProbes;
            SafeDisposal.New()
                        .Execute(() => _subscriptionManager.Unsubscribe(_subscription), "unsubscribing from RCM")
                        .Add(_snapshotUploader)
                        .Add(_diagnosticsUploader)
                        .Add(_probeStatusPoller)
                        .Add(_dogStats)
                        .DisposeAll();
        }
    }
}
