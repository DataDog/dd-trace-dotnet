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
    internal delegate void NativeProbeInstrumentationRequester(
        NativeMethodProbeDefinition[] methodProbes,
        NativeLineProbeDefinition[] lineProbes,
        NativeSpanProbeDefinition[] spanProbes,
        NativeRemoveProbeRequest[] revertProbes);

    internal sealed class DynamicInstrumentation : IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DynamicInstrumentation));

        // Completed when this DI instance is being disposed (runtime disable via remote config, or process shutdown).
        // Used to abort in-flight initialization waits promptly.
        private readonly TaskCompletionSource<bool> _disposalSignal;
        private readonly IDiscoveryService _discoveryService;
        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly ISubscription _subscription;
        private readonly ISnapshotUploader _snapshotUploader;
        private readonly ISnapshotUploader _logUploader;
        private readonly IDebuggerUploader _diagnosticsUploader;
        private readonly ILineProbeResolver _lineProbeResolver;
        private readonly List<ProbeDefinition> _unboundProbes;
        private readonly Dictionary<string, LineProbeResolveErrorKey> _lastReportedUnboundProbeErrors;
        private readonly IProbeStatusPoller _probeStatusPoller;
        private readonly ConfigurationUpdater _configurationUpdater;
        private readonly IDogStatsd _dogStats;
        private readonly MemoryPressureMonitor _memoryPressureMonitor;
        private readonly DebuggerSettings _settings;
        private readonly NativeProbeInstrumentationRequester _instrumentProbes;
        private readonly object _instanceLock = new();
        private int _disposeState;
        private int _initializationState;
        private Task? _initializationTask;

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
            IDogStatsd dogStats,
            IDebuggerGlobalRateLimiter? globalRateLimiter = null,
            MemoryPressureMonitor? memoryPressureMonitor = null,
            NativeProbeInstrumentationRequester? instrumentProbes = null)
        {
            Log.Information("Initializing Dynamic Instrumentation");
            _settings = settings;
            _disposalSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _discoveryService = discoveryService;
            _lineProbeResolver = lineProbeResolver;
            _snapshotUploader = snapshotUploader;
            _logUploader = logUploader;
            _diagnosticsUploader = diagnosticsUploader;
            _probeStatusPoller = probeStatusPoller;
            _subscriptionManager = remoteConfigurationManager;
            _configurationUpdater = configurationUpdater;
            _configurationUpdater.SetProbeInstrumentationHandlers(UpdateAddedProbeInstrumentations, UpdateRemovedProbeInstrumentations);
            _dogStats = dogStats;
            _memoryPressureMonitor = memoryPressureMonitor ?? new MemoryPressureMonitor(MemoryPressureConfig.Default);
            _instrumentProbes = instrumentProbes ?? DebuggerNativeMethods.InstrumentProbes;
            _unboundProbes = new List<ProbeDefinition>();
            _lastReportedUnboundProbeErrors = new Dictionary<string, LineProbeResolveErrorKey>();
            (globalRateLimiter ?? DebuggerGlobalRateLimiter.Instance).Initialize();
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

        public bool IsInitialized => Volatile.Read(ref _initializationState) == 2;

        internal void Initialize()
        {
            if (!_settings.DynamicInstrumentationEnabled || IsDisposed)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _initializationState, 1, 0) != 0)
            {
                return;
            }

            _initializationTask = InitializeAsync();
        }

        /// <summary>
        /// Returns the task representing in-flight initialization (or a completed
        /// task if <see cref="Initialize"/> has not been called yet). DI enablement
        /// is serialized by the owning initialization flow, so callers are expected
        /// to request this task after invoking <see cref="Initialize"/>.
        /// </summary>
        internal Task GetInitializationTask() => _initializationTask ?? Task.CompletedTask;

        private async Task InitializeAsync()
        {
            try
            {
                // Start loading probes from file and checking RCM availability in parallel
                var fileProbesTask = ProbeConfigurationFileLoader.LoadAsync(_settings.ProbeFile);
                var rcmAvailabilityTask = WaitForRcmAvailabilityAsync();

                var hasFileProbes = false;

                // Always attempt to load probes from file, even if RCM is unavailable
                var probeConfiguration = await fileProbesTask.ConfigureAwait(false);
                if (probeConfiguration != null)
                {
                    hasFileProbes = _configurationUpdater.HasAnyEffectiveProbeForFile(probeConfiguration);
                    if (hasFileProbes)
                    {
                        StartRuntimeIfNeeded(subscribeToRcm: false);
                    }

                    _configurationUpdater.AcceptFile(probeConfiguration);
                }

                var isRcmAvailable = await rcmAvailabilityTask.ConfigureAwait(false);
                if (isRcmAvailable)
                {
                    StartRuntimeIfNeeded(subscribeToRcm: true);
                }

                // Start background processing and register the assembly load callback if either:
                // - RCM is available
                // - There are probes from file
                if (IsInitialized)
                {
                    Log.Information("Dynamic Instrumentation initialization completed successfully");
                }
                else
                {
                    Log.Information("Dynamic Instrumentation not initialized because RCM isn't available and no valid probes have loaded from file");
                }
            }
            catch (OperationCanceledException e)
            {
                Log.Debug(e, "Dynamic Instrumentation stopped due task cancellation");
            }
            catch (Exception e)
            {
                Log.Error(e, "Dynamic Instrumentation initialization failed");
            }
            finally
            {
                Interlocked.CompareExchange(ref _initializationState, 0, 1);
            }
        }

        /// <summary>
        /// Starts the runtime (background processing + assembly-load callback) exactly once, and optionally
        /// subscribes to RCM. Idempotent and safe to call from both the file-probe path (<paramref name="subscribeToRcm"/>
        /// false) and the RCM-available path (<paramref name="subscribeToRcm"/> true).
        /// </summary>
        private void StartRuntimeIfNeeded(bool subscribeToRcm)
        {
            if (IsDisposed || (IsInitialized && !subscribeToRcm))
            {
                return;
            }

            lock (_instanceLock)
            {
                if (IsDisposed)
                {
                    return;
                }

                // Start the runtime exactly once.
                if (!IsInitialized)
                {
                    var assemblyLoadSubscribed = false;
                    try
                    {
                        AppDomain.CurrentDomain.AssemblyLoad += CheckUnboundProbes;
                        assemblyLoadSubscribed = true;
                        StartBackgroundProcess();
                        Volatile.Write(ref _initializationState, 2);
                    }
                    catch
                    {
                        if (assemblyLoadSubscribed)
                        {
                            AppDomain.CurrentDomain.AssemblyLoad -= CheckUnboundProbes;
                        }

                        throw;
                    }
                }

                // Subscribe only on the RCM path, and only if a disable hasn't landed in the meantime.
                // Best-effort: correctness against a leaked subscription is guaranteed by Dispose's Unsubscribe
                // running under this same lock.
                if (subscribeToRcm && !IsDisposed)
                {
                    _subscriptionManager.SubscribeToChanges(_subscription);
                }
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
                if (IsDisposed)
                {
                    return [];
                }

                if (addedProbes.Count == 0)
                {
                    return [];
                }

                Log.Information<int>("Dynamic Instrumentation.InstrumentProbes: Request to instrument {Count} probes definitions", addedProbes.Count);
                var result = new List<ConfigurationUpdater.UpdateResult>(addedProbes.Count);

                var methodProbes = new List<NativeMethodProbeDefinition>();
                var lineProbes = new List<NativeLineProbeDefinition>();
                var spanProbes = new List<NativeSpanProbeDefinition>();

                var fetchProbeStatus = new List<FetchProbeStatus>();
                var lineProbeDiagnosticLevel = Log.IsEnabled(LogEventLevel.Debug) ? LineProbeDiagnosticLevel.Full : LineProbeDiagnosticLevel.Minimal;

                foreach (var addedProbe in addedProbes)
                {
                    try
                    {
                        switch (GetProbeLocationType(addedProbe))
                        {
                            case ProbeLocationType.Line:
                                {
                                    var lineProbeResult = _lineProbeResolver.TryResolveLineProbe(addedProbe, out var location, lineProbeDiagnosticLevel);
                                    var status = lineProbeResult.Status;

                                    LogLineProbeResolution(addedProbe.Id, lineProbeResult, "initial resolution");
                                    switch (status)
                                    {
                                        case LiveProbeResolveStatus.Bound:
                                            lineProbes.Add(new NativeLineProbeDefinition(location!.ProbeDefinition.Id, location.Mvid, location.MethodToken, (int)location.BytecodeOffset, location.LineNumber, location.ProbeDefinition.Where.SourceFile));
                                            fetchProbeStatus.Add(new FetchProbeStatus(addedProbe.Id, addedProbe.Version ?? 0));
                                            _lastReportedUnboundProbeErrors.Remove(addedProbe.Id);
                                            ProbeExpressionsProcessor.Instance.AddProbeProcessor(addedProbe);
                                            SetRateLimit(addedProbe);
                                            break;
                                        case LiveProbeResolveStatus.Unbound:
                                            // Unbound line probes remain retryable on future assembly loads. Some retryable
                                            // outcomes are still reported as ERROR so the backend can show actionable user
                                            // feedback while the tracer keeps looking for a later exact/unique match.
                                            Log.Information("ProbeID {ProbeID} is unbound. It will be retried when new assemblies are loaded.", addedProbe.Id);
                                            _unboundProbes.Add(addedProbe);
                                            var unboundProbeStatus = lineProbeResult.ReportError
                                                                         ? new ProbeStatus(addedProbe.Id, Sink.Models.Status.ERROR, errorMessage: GetLineProbeResolveMessage(lineProbeResult))
                                                                         : new ProbeStatus(addedProbe.Id, Sink.Models.Status.RECEIVED, errorMessage: null);
                                            UpdateLastReportedUnboundProbeError(addedProbe.Id, lineProbeResult);
                                            fetchProbeStatus.Add(new FetchProbeStatus(addedProbe.Id, addedProbe.Version ?? 0, unboundProbeStatus));
                                            break;
                                        case LiveProbeResolveStatus.Error:
                                            Log.Warning("ProbeID {ProbeID} error resolving live. Error: {Error}", addedProbe.Id, lineProbeResult.Message);
                                            _lastReportedUnboundProbeErrors.Remove(addedProbe.Id);
                                            fetchProbeStatus.Add(new FetchProbeStatus(addedProbe.Id, addedProbe.Version ?? 0, new ProbeStatus(addedProbe.Id, Sink.Models.Status.ERROR, errorMessage: lineProbeResult.Message)));
                                            break;
                                    }

                                    break;
                                }

                            case ProbeLocationType.Method:
                                {
                                    var signature = SignatureParser.TryParse(addedProbe.Where.Signature, out var s) ? s : null;

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
                if (methodProbes.Count != 0 || lineProbes.Count != 0 || spanProbes.Count != 0)
                {
                    _instrumentProbes(methodProbes.ToArray(), lineProbes.ToArray(), spanProbes.ToArray(), []);
                }

                var probeIds = fetchProbeStatus.Select(fp => fp.ProbeId).ToArray();
                _probeStatusPoller.UpdateProbes(probeIds, fetchProbeStatus.ToArray());

                // This log entry is being checked in integration test
                Log.Information("Dynamic Instrumentation.InstrumentProbes: Request to instrument added probes definitions completed.");

                return result;
            }
        }

        private static LineProbeResolveErrorKey GetLineProbeResolveErrorKey(LineProbeResolveResult result)
        {
            return result.ErrorKey.IsEmpty ? new LineProbeResolveErrorKey(result.Reason) : result.ErrorKey;
        }

        private static string? GetLineProbeResolveMessage(LineProbeResolveResult result)
        {
            if (result.Message is not null)
            {
                return result.Message;
            }

            return result.Reason == LineProbeResolveReason.LoadedAssemblySourceFileMismatch
                       ? LineProbeResolver.BuildLoadedAssemblySourceFileMismatchMessage(GetLineProbeResolveErrorDetails(result))
                       : null;
        }

        private static LineProbeResolveErrorDetails GetLineProbeResolveErrorDetails(LineProbeResolveResult result)
        {
            return result.ErrorDetails.IsEmpty ? new LineProbeResolveErrorDetails(GetLineProbeResolveErrorKey(result)) : result.ErrorDetails;
        }

        private static string? JoinLogValues(string[]? values)
        {
            return values is { Length: > 0 } ? string.Join(" | ", values) : null;
        }

        private void SetRateLimit(ProbeDefinition probe)
        {
            if (_settings.IsSnapshotExplorationTestEnabled)
            {
                ProbeRateLimiter.Instance.TryAddSampler(probe.Id, NopAdaptiveSampler.Instance);
                return;
            }

            switch (probe)
            {
                case LogProbe { Sampling: { } sampling }:
                    ProbeRateLimiter.Instance.SetRate(probe.Id, (int)sampling.SnapshotsPerSecond);
                    break;
                case LogProbe logProbe:
                    ProbeRateLimiter.Instance.SetRate(probe.Id, logProbe.CaptureSnapshot || logProbe.CaptureExpressions is { Length: > 0 } ? 1 : 5000);
                    break;
                case SpanDecorationProbe or MetricProbe:
                    ProbeRateLimiter.Instance.TryAddSampler(probe.Id, NopAdaptiveSampler.Instance);
                    break;
            }
        }

        private void UpdateLastReportedUnboundProbeError(string probeId, LineProbeResolveResult result)
        {
            if (result.ReportError)
            {
                _lastReportedUnboundProbeErrors[probeId] = GetLineProbeResolveErrorKey(result);
            }
            else
            {
                _lastReportedUnboundProbeErrors.Remove(probeId);
            }
        }

        private bool ShouldReportUnboundProbeError(string probeId, LineProbeResolveResult result)
        {
            var errorKey = GetLineProbeResolveErrorKey(result);
            if (_lastReportedUnboundProbeErrors.TryGetValue(probeId, out var lastErrorKey) &&
                lastErrorKey == errorKey)
            {
                return false;
            }

            _lastReportedUnboundProbeErrors[probeId] = errorKey;
            return true;
        }

        internal void UpdateRemovedProbeInstrumentations(string[] removedProbesIds)
        {
            if (IsDisposed)
            {
                return;
            }

            if (removedProbesIds.Length == 0)
            {
                return;
            }

            Log.Information<int>("Dynamic Instrumentation.InstrumentProbes: Request to remove {Length} probes.", removedProbesIds.Length);

            lock (_instanceLock)
            {
                if (IsDisposed)
                {
                    return;
                }

                var boundedProbes = _probeStatusPoller.GetBoundedProbes();
                var probesToRemoveFromNative = new List<string>();
                _probeStatusPoller.RemoveProbes(removedProbesIds);

                for (var i = 0; i < removedProbesIds.Length; i++)
                {
                    var id = removedProbesIds[i];
                    try
                    {
                        _unboundProbes.RemoveAll(pd => pd.Id == id);
                        _lastReportedUnboundProbeErrors.Remove(id);

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

                if (probesToRemoveFromNative.Count != 0)
                {
                    var revertProbes = probesToRemoveFromNative
                       .Select(probeId => new NativeRemoveProbeRequest(probeId));

                    _instrumentProbes([], [], [], revertProbes.ToArray());
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

        private void LogLineProbeResolution(string probeId, LineProbeResolveResult result, string phase)
        {
            var diagnostics = result.Diagnostics;
            Log.Information(
                "Finished resolving line probe for ProbeID {ProbeID} during {Phase}. Result was '{Status}'. Reason was '{Reason}'. Message was: '{Message}'. ProbeFile={ProbeFile} ProbeLine={ProbeLine}",
                [
                    probeId,
                    phase,
                    result.Status,
                    result.Reason,
                    result.Message,
                    diagnostics?.ProbeFile,
                    diagnostics?.ProbeLine
                ]);

            if (!Log.IsEnabled(LogEventLevel.Debug) || diagnostics == null)
            {
                return;
            }

            Log.Debug(
                "Finished resolving line probe for ProbeID {ProbeID} during {Phase}. Result was '{Status}'. Reason was '{Reason}'. Message was: '{Message}'. ProbeFile={ProbeFile} ProbeLine={ProbeLine} RawLines={RawLines} ResolvedSourceFile={ResolvedSourceFile} PathMatchType={PathMatchType} MatchingTrailingSegments={MatchingTrailingSegments} FallbackFailureReason={FallbackFailureReason} QualifiedFallbackMatches={QualifiedFallbackMatches} AssemblyName={AssemblyName} AssemblyLocation={AssemblyLocation} ModuleVersionId={ModuleVersionId} ExceptionType={ExceptionType} LoadedAssemblies={LoadedAssemblies} SymbolicatedAssemblies={SymbolicatedAssemblies} SameFileNameMatches={SameFileNameMatches} SameFileNameExamples={SameFileNameExamples}",
                [
                    probeId,
                    phase,
                    result.Status,
                    result.Reason,
                    result.Message,
                    diagnostics.ProbeFile,
                    diagnostics.ProbeLine,
                    diagnostics.RawLines,
                    diagnostics.ResolvedSourceFile,
                    diagnostics.PathMatchType,
                    diagnostics.MatchingTrailingSegments,
                    diagnostics.FallbackFailureReason,
                    diagnostics.QualifiedFallbackMatchCount,
                    diagnostics.AssemblyName,
                    diagnostics.AssemblyLocation,
                    diagnostics.ModuleVersionId,
                    diagnostics.ExceptionType,
                    diagnostics.LoadedAssemblyCount,
                    diagnostics.SymbolicatedAssemblyCount,
                    diagnostics.SameFileNameMatchCount,
                    JoinLogValues(diagnostics.SameFileNameExamples)
                ]);
        }

        private void LogLineProbeRetryResolution(string probeId, AssemblyLoadEventArgs args, LineProbeResolveResult result)
        {
            if (!Log.IsEnabled(LogEventLevel.Debug))
            {
                return;
            }

            var diagnostics = result.Diagnostics;
            Log.Debug(
                "Rechecked unbound line probe for ProbeID {ProbeId} after assembly load {AssemblyName}. Result was '{Status}'. Reason was '{Reason}'. Message was: '{Message}'. ProbeFile={ProbeFile} ProbeLine={ProbeLine} RawLines={RawLines} ResolvedSourceFile={ResolvedSourceFile} PathMatchType={PathMatchType} MatchingTrailingSegments={MatchingTrailingSegments} FallbackFailureReason={FallbackFailureReason} QualifiedFallbackMatches={QualifiedFallbackMatches} AssemblyName={ResolvedAssemblyName} AssemblyLocation={ResolvedAssemblyLocation} ModuleVersionId={ModuleVersionId} ExceptionType={ExceptionType} LoadedAssemblies={LoadedAssemblies} SymbolicatedAssemblies={SymbolicatedAssemblies} SameFileNameMatches={SameFileNameMatches} SameFileNameExamples={SameFileNameExamples}",
                [
                    probeId,
                    args.LoadedAssembly.GetName().Name,
                    result.Status,
                    result.Reason,
                    result.Message,
                    diagnostics?.ProbeFile,
                    diagnostics?.ProbeLine,
                    diagnostics?.RawLines,
                    diagnostics?.ResolvedSourceFile,
                    diagnostics?.PathMatchType,
                    diagnostics?.MatchingTrailingSegments,
                    diagnostics?.FallbackFailureReason,
                    diagnostics?.QualifiedFallbackMatchCount,
                    diagnostics?.AssemblyName,
                    diagnostics?.AssemblyLocation,
                    diagnostics?.ModuleVersionId,
                    diagnostics?.ExceptionType,
                    diagnostics?.LoadedAssemblyCount,
                    diagnostics?.SymbolicatedAssemblyCount,
                    diagnostics?.SameFileNameMatchCount,
                    JoinLogValues(diagnostics?.SameFileNameExamples)
                ]);
        }

        private void CheckUnboundProbes(object? sender, AssemblyLoadEventArgs args)
        {
            // A new assembly was loaded, so re-examine whether the probe can now be resolved.
            lock (_instanceLock)
            {
                if (IsDisposed)
                {
                    return;
                }

                if (_unboundProbes.Count == 0)
                {
                    return;
                }

                // Initialize these lists only when a retry reaches a terminal state, to reduce unnecessary allocations.
                List<NativeLineProbeDefinition>? lineProbes = null;
                List<ProbeDefinition>? boundProbes = null;
                List<ProbeDefinition>? probesToRemoveFromRetry = null;
                var diagnosticLevel = Log.IsEnabled(LogEventLevel.Debug) ? LineProbeDiagnosticLevel.Full : LineProbeDiagnosticLevel.Minimal;

                foreach (var unboundProbe in _unboundProbes)
                {
                    var result = _lineProbeResolver.TryResolveLineProbe(unboundProbe, out var location, diagnosticLevel);
                    LogLineProbeRetryResolution(unboundProbe.Id, args, result);

                    if (result.Status == LiveProbeResolveStatus.Bound)
                    {
                        lineProbes ??= new List<NativeLineProbeDefinition>();
                        boundProbes ??= new List<ProbeDefinition>();
                        probesToRemoveFromRetry ??= new List<ProbeDefinition>();

                        boundProbes.Add(unboundProbe);
                        probesToRemoveFromRetry.Add(unboundProbe);
                        lineProbes.Add(new NativeLineProbeDefinition(location!.ProbeDefinition.Id, location.Mvid, location.MethodToken, (int)location.BytecodeOffset, location.LineNumber, location.ProbeDefinition.Where.SourceFile));
                    }
                    else if (result.Status == LiveProbeResolveStatus.Unbound)
                    {
                        if (result.ReportError && ShouldReportUnboundProbeError(unboundProbe.Id, result))
                        {
                            var errorMessage = GetLineProbeResolveMessage(result);
                            _probeStatusPoller.UpdateProbe(
                                unboundProbe.Id,
                                new FetchProbeStatus(
                                    unboundProbe.Id,
                                    unboundProbe.Version ?? 0,
                                    new ProbeStatus(unboundProbe.Id, Sink.Models.Status.ERROR, errorMessage: errorMessage)));
                        }
                        else if (!result.ReportError)
                        {
                            _lastReportedUnboundProbeErrors.Remove(unboundProbe.Id);
                        }
                    }
                    else if (result.Status == LiveProbeResolveStatus.Error)
                    {
                        Log.Debug("ProbeID {ProbeID} error resolving during retry. Error: {Error}", unboundProbe.Id, result.Message);
                        probesToRemoveFromRetry ??= new List<ProbeDefinition>();
                        probesToRemoveFromRetry.Add(unboundProbe);
                        _lastReportedUnboundProbeErrors.Remove(unboundProbe.Id);
                        _probeStatusPoller.UpdateProbe(
                            unboundProbe.Id,
                            new FetchProbeStatus(
                                unboundProbe.Id,
                                unboundProbe.Version ?? 0,
                                new ProbeStatus(unboundProbe.Id, Sink.Models.Status.ERROR, errorMessage: result.Message)));
                    }
                }

                if (lineProbes?.Count > 0 && boundProbes != null)
                {
                    Log.Information("Dynamic Instrumentation.CheckUnboundProbes: {Count} unbound probes became bound.", property: boundProbes.Count);

                    // Register processors and samplers BEFORE the native call makes the IL live:
                    // otherwise a probe hit between InstrumentProbes returning and SetRateLimit
                    // running would insert a default-rate sampler via GerOrAddSampler, and the
                    // configured rate would never take effect for that probe.
                    foreach (var boundProbe in boundProbes)
                    {
                        ProbeExpressionsProcessor.Instance.AddProbeProcessor(boundProbe);
                        SetRateLimit(boundProbe);
                    }

                    _instrumentProbes([], lineProbes.ToArray(), [], []);

                    var probeIds = new string[lineProbes.Count];
                    var newProbeStatuses = new FetchProbeStatus[boundProbes.Count];
                    for (var i = 0; i < lineProbes.Count; i++)
                    {
                        probeIds[i] = lineProbes[i].ProbeId;
                        var boundProbe = boundProbes[i];
                        newProbeStatuses[i] = new FetchProbeStatus(boundProbe.Id, boundProbe.Version ?? 0);
                    }

                    _probeStatusPoller.UpdateProbes(probeIds, newProbeStatuses);
                }

                if (probesToRemoveFromRetry != null)
                {
                    foreach (var probeToRemove in probesToRemoveFromRetry)
                    {
                        _unboundProbes.Remove(probeToRemove);
                        _lastReportedUnboundProbeErrors.Remove(probeToRemove.Id);
                    }
                }
            }
        }

        private ApplyDetails[] AcceptAddedConfiguration(List<RemoteConfiguration>? configs)
        {
            if (configs == null || IsDisposed)
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

            var rcmUpdate = new ProbeConfiguration()
            {
                ServiceConfiguration = serviceConfig,
                LogProbes = logs.ToArray(),
                MetricProbes = metrics.ToArray(),
                SpanProbes = spans.ToArray(),
                SpanDecorationProbes = spanDecoration.ToArray()
            };

            try
            {
                List<ConfigurationUpdater.UpdateResult> updateResults;
                lock (_instanceLock)
                {
                    if (IsDisposed)
                    {
                        return [];
                    }

                    updateResults = _configurationUpdater.AcceptAdded(rcmUpdate);
                }

                foreach (var updateResult in updateResults)
                {
                    var config = configs.FirstOrDefault(c => ProbeConfigurationUtils.IsProbeId(c.Path, updateResult.Id));
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
            if (paths == null || IsDisposed)
            {
                return;
            }

            lock (_instanceLock)
            {
                if (IsDisposed)
                {
                    return;
                }

                _configurationUpdater.AcceptRemoved(paths);
            }
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

        internal void RefreshMemoryPressureIfStale()
        {
            if (IsDisposed)
            {
                return;
            }

            _memoryPressureMonitor.RefreshIfStale();
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
            if (_discoveryService is NullDiscoveryService)
            {
                // No agent discovery means no RCM signal. File-probe mode can still initialize without waiting.
                return false;
            }

            var rcmAvailabilityTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _discoveryService.SubscribeToChanges(DiscoveryCallback);

            try
            {
                var rcmTimeout = TimeSpan.FromMinutes(5);
                var timeoutTask = Task.Delay(rcmTimeout);

                var completedTask = await Task.WhenAny(rcmAvailabilityTcs.Task, timeoutTask, _disposalSignal.Task).ConfigureAwait(false);
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
            if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            {
                return;
            }

            _disposalSignal.TrySetResult(true);

            lock (_instanceLock)
            {
                // Must stay under the lock: StartRuntimeIfNeeded subscribes AssemblyLoad under the same
                // lock, so unsubscribing here is what prevents a subscribe-after-dispose handler leak.
                AppDomain.CurrentDomain.AssemblyLoad -= CheckUnboundProbes;

                SafeDisposal.New()
                            .Execute(() => _subscriptionManager.Unsubscribe(_subscription), "unsubscribing from RCM")
                            .Add(_snapshotUploader)
                            .Add(_logUploader)
                            .Add(_diagnosticsUploader)
                            .Add(_probeStatusPoller)
                            .Add(_memoryPressureMonitor)
                            .DisposeAll();
            }

            _dogStats?.DisposeAsync().ContinueWith(
                t => Log.Error(t.Exception, "Error waiting for StatsD disposal"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }
}
