// <copyright file="FeatureFlagsModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.FeatureFlags.Rcm;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.FeatureFlags
{
    internal sealed class FeatureFlagsModule : IDisposable
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsModule));

        // Activation, disposal and exposure-API creation all mutate the same state from different
        // threads, so they share one lock rather than individual interlocked flags: a flag set
        // before its accompanying setup completes lets a concurrent caller observe a half-activated
        // module, and a disposal interleaved with activation leaks the delivery path it started.
        private readonly object _stateLock = new();

        // Guards the configuration handler and its generation counters. Separate from _stateLock,
        // which covers activation and disposal: a configuration event must not wait on either.
        private readonly object _handlerLock = new();

        // Settings callbacks run under the settings manager's lock. They must not take
        // _stateLock, which is held while activation subscribes and receives settings replay.
        private readonly object _evaluationLock = new();

        private readonly FeatureFlagsSettings _settings;

        // Injectable so tests can assert the onboarding diagnostics without a global log sink.
        private readonly IDatadogLogger _log;

        // The agentless source targets flags by environment, which customers can change in code
        // after startup, so it needs the manager rather than a captured value.
        private readonly TracerSettings.SettingsManager _settingsManager;
        private readonly bool _isRemoteConfigurationAvailable;

        // ExposureApi reads only settings.Manager but takes TracerSettings. Held so the API can be
        // built on the first exposure instead of at startup.
        private readonly TracerSettings _tracerSettings;
        private readonly IDiscoveryService _discoveryService;

        // A factory rather than the static Create, so a test can supply a source that records what the
        // module does with it: whether it is started before activation, and whether it is disposed.
        private readonly Func<FeatureFlagsModule, IFeatureFlagsDeliverySource?> _agentlessSourceFactory;
        private readonly bool _spanEnrichmentEnabled;
        private readonly IRcmSubscriptionManager _rcmSubscriptionManager;
        private readonly TaskCompletionSource<bool> _firstConfigReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ISubscription? _rcmSubscription;

        private Action? _onNewConfigEventHandler;

        // Counts configuration events, and records the last one handed to the current handler. A
        // registration can run at the same time as an arriving configuration, and without these two
        // the registration replay and the delivery path both hand the handler the same configuration.
        private long _configGeneration;
        private long _handlerDeliveredGeneration;

        private FeatureFlagsEvaluator? _evaluator;
        private IFeatureFlagsDeliverySource? _agentlessSource;
        private ExposureApi? _exposureApi;
        private FeatureFlagsEvpTransport? _evaluationTransport;
        private FlagEvaluationWriter? _evaluationWriter;
        private FlagEvaluationWriter? _availableEvaluationWriter;
        private IDisposable? _evaluationSettingsSubscription;
        private Action<AgentConfiguration>? _evaluationDiscoveryCallback;
        private FeatureFlagsEvpTransport? _evpTransport;
        private string? _deliveryUnavailableReason;
        private bool _activated;
        private bool _disposed;
        private bool _deliveryStarted;

        internal FeatureFlagsModule(
            TracerSettings settings,
            IRcmSubscriptionManager rcmSubscriptionManager,
            Func<FeatureFlagsModule, IFeatureFlagsDeliverySource?>? agentlessSourceFactory = null,
            IDiscoveryService? discoveryService = null,
            IDatadogLogger? logger = null)
        {
            _log = logger ?? Log;
            _settings = settings.FeatureFlags;
            _settingsManager = settings.Manager;
            _isRemoteConfigurationAvailable = settings.IsRemoteConfigurationAvailable;
            _spanEnrichmentEnabled = settings.IsSpanEnrichmentEnabled;
            _tracerSettings = settings;
            _agentlessSourceFactory = agentlessSourceFactory
                                   ?? (static module => AgentlessConfigurationSource.Create(module._settings, module._settingsManager, module.ApplyConfiguration, module._log));
            _rcmSubscriptionManager = rcmSubscriptionManager;
            _discoveryService = discoveryService ?? NullDiscoveryService.Instance;

            _log.Debug<FeatureFlagsSource, double>(
                "Feature Flags: enabled with configuration source {Source} (initialization timeout {TimeoutMs}ms)",
                _settings.Source,
                _settings.InitializationTimeout.TotalMilliseconds);
        }

        /// <summary>
        /// Gets a task that completes once configuration has been applied for the first time.
        /// </summary>
        [TestingAndPrivateOnly]
        internal Task FirstConfigReceived => _firstConfigReceived.Task;

        [TestingAndPrivateOnly]
        internal FeatureFlagsSettings Settings => _settings;

        /// <summary>
        /// Gets a value indicating whether configuration is currently held, so evaluations can resolve
        /// flags. Goes back to <c>false</c> when Remote Configuration withdraws it.
        /// </summary>
        internal bool HasConfiguration => Volatile.Read(ref _evaluator) is not null;

        internal FlagEvaluationWriter? EvaluationWriter => Volatile.Read(ref _availableEvaluationWriter);

        internal bool EvaluationEventsEnabled => _settings.EvaluationEventsEnabled;

        public static FeatureFlagsModule? Create(
            TracerSettings settings,
            IRcmSubscriptionManager rcmSubscriptionManager,
            Func<FeatureFlagsModule, IFeatureFlagsDeliverySource?>? agentlessSourceFactory = null,
            IDiscoveryService? discoveryService = null,
            IDatadogLogger? logger = null)
        {
            if (!settings.FeatureFlags.Enabled)
            {
                (logger ?? Log).Debug("Feature Flags: disabled; set DD_FEATURE_FLAGS_ENABLED=true to enable");
                return null;
            }

            var module = new FeatureFlagsModule(settings, rcmSubscriptionManager, agentlessSourceFactory, discoveryService, logger);

            // Subscribing from here rather than the constructor, so the callback can only ever reach
            // a fully constructed module.
            if (settings.FeatureFlags.Source == FeatureFlagsSource.RemoteConfig)
            {
                module.SubscribeToRemoteConfiguration();
            }

            return module;
        }

        public void Dispose() => _ = DisposeAsync();

        internal Task DisposeAsync()
        {
            ISubscription? subscription;
            IFeatureFlagsDeliverySource? agentlessSource;
            ExposureApi? exposureApi;
            FlagEvaluationWriter? writer;
            FeatureFlagsEvpTransport? evaluationTransport;
            IDisposable? settingsSubscription;
            Action<AgentConfiguration>? discoveryCallback;
            FeatureFlagsEvpTransport? evpTransport;

            lock (_stateLock)
            {
                if (_disposed)
                {
                    return _disposeCompletion.Task;
                }

                _disposed = true;

                subscription = _rcmSubscription;
                agentlessSource = _agentlessSource;
                exposureApi = _exposureApi;
                lock (_evaluationLock)
                {
                    writer = _evaluationWriter;
                    evaluationTransport = _evaluationTransport;
                    Volatile.Write(ref _availableEvaluationWriter, null);
                    Volatile.Write(ref _evaluationWriter, null);
                    _evaluationTransport = null;
                }

                settingsSubscription = _evaluationSettingsSubscription;
                discoveryCallback = _evaluationDiscoveryCallback;
                evpTransport = _evpTransport;

                _rcmSubscription = null;
                _agentlessSource = null;
                Volatile.Write(ref _exposureApi, null);
                _evaluationSettingsSubscription = null;
                _evaluationDiscoveryCallback = null;
                _evpTransport = null;
            }

            // Only what stops is reported: the evaluator is kept, so an evaluation that still reaches
            // this module resolves against the last applied configuration.
            _log.Debug("Feature Flags: shutting down; configuration delivery and exposure reporting stop");

            // Close admission synchronously, then perform cleanup and bounded waiting outside the lock.
            var close = writer?.CloseAsync(TimeSpan.FromSeconds(5)) ?? Task.CompletedTask;
            _ = FinishDisposalAsync(close, evaluationTransport, settingsSubscription, discoveryCallback, subscription, agentlessSource, exposureApi, evpTransport);
            return _disposeCompletion.Task;
        }

        // Drain already accepted events even if an Agent change has closed admission.
        internal Task FlushAsync() => Volatile.Read(ref _evaluationWriter)?.FlushAsync() ?? Task.CompletedTask;

        /// <summary>
        /// Signals that application code initialized the provider. Idempotent.
        /// <para>
        /// This is where the agentless source starts, because its requests go straight to Datadog and
        /// are billable, so nothing is sent until the application adopts the provider. The Remote
        /// Configuration source does not wait here: it rides the poll the agent already performs, and
        /// every other tracer subscribes to it at startup.
        /// </para>
        /// </summary>
        internal void Activate()
        {
            // Every evaluation calls this, so the steady state must not take the lock. The flag only
            // ever goes false to true, so a stale read costs one lock acquisition and nothing else:
            // the check inside the lock is what decides.
            if (Volatile.Read(ref _activated))
            {
                return;
            }

            IFeatureFlagsDeliverySource? sourceToStart = null;

            lock (_stateLock)
            {
                if (_activated || _disposed)
                {
                    return;
                }

                SubscribeToEvaluationDelivery();
                Volatile.Write(ref _activated, true);

                switch (_settings.Source)
                {
                    case FeatureFlagsSource.Offline:
                        _log.Debug("Feature Flags: configuration source is offline; no configuration is fetched and evaluations return default values");
                        break;
                    case FeatureFlagsSource.Agentless:
                        // Polling is billable, so it starts here rather than at construction.
                        var source = _agentlessSourceFactory(this);
                        if (source is null)
                        {
                            // Create logs the specific reason, which may name configuration the
                            // message must not repeat back to the application.
                            _deliveryUnavailableReason = "the agentless source could not be started, see the Datadog logs for the reason";
                            break;
                        }

                        _agentlessSource = source;
                        sourceToStart = source;
                        _deliveryStarted = true;
                        break;
                }
            }

            // Outside the lock, so that a source which does any work of its own before returning cannot
            // hold up a Dispose(). A Dispose() that interleaves here has already signalled the source's
            // shutdown, which makes its poll loop exit before the first request.
            sourceToStart?.Start();
        }

        /// <summary>
        /// Activates delivery and waits for the first configuration, so that a provider reported as
        /// ready can resolve flags.
        /// <para>
        /// Returns without throwing when the wait times out: delivery being slow is transient, and
        /// OpenFeature marks the provider ready on a normal return, so evaluations return their
        /// defaults with PROVIDER_NOT_READY until the configuration lands. Throws
        /// <see cref="FeatureFlagsDeliveryUnavailableException"/> when no source could start at all,
        /// which OpenFeature turns into an error status: a provider that can never resolve a flag is
        /// in error, not ready.
        /// </para>
        /// <para>
        /// The wait ends only when a configuration arrives, so a service with no flag configuration
        /// waits the whole timeout. The application is blocked for that long, because OpenFeature
        /// awaits this from <c>SetProviderAsync</c>, so the timeout has to stay below whatever budget
        /// the application's readiness probe allows.
        /// </para>
        /// </summary>
        internal async Task InitializeAsync(CancellationToken cancellationToken)
        {
            Activate();

            if (_firstConfigReceived.Task.IsCompleted)
            {
                _log.Debug("Feature Flags: provider initialized successfully (configuration already received)");
                return;
            }

            // Read under the lock so a concurrent activation is seen through, rather than reporting no
            // delivery because its source had not finished starting.
            bool deliveryStarted;
            string? deliveryUnavailableReason;
            lock (_stateLock)
            {
                deliveryStarted = _deliveryStarted;
                deliveryUnavailableReason = _deliveryUnavailableReason;
            }

            // When no delivery source could start (agentless without an API key, or the Remote
            // Configuration source where Remote Configuration is unavailable), no configuration can
            // ever arrive, so waiting would only delay startup.
            // Returning instead would report the provider as usable while every evaluation keeps
            // returning its default, so the failure is raised: the SDK turns it into an error status
            // and an error event without taking the application down.
            if (!deliveryStarted)
            {
                var exception = new FeatureFlagsDeliveryUnavailableException(deliveryUnavailableReason);

                // Skips telemetry: this is customer configuration, not a tracer bug, and the specific
                // cause has already been logged where it was discovered.
                _log.ErrorSkipTelemetry("Feature Flags: provider failed to initialize: {Reason}", exception.Message);
                throw exception;
            }

            _log.Debug<double>("Feature Flags: waiting up to {TimeoutMs}ms for provider initialization...", _settings.InitializationTimeout.TotalMilliseconds);

            // The caller's cancellation is turned into a completed task rather than passed to anything
            // that throws on it, because an exception on this path can crash buggy runtimes.
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => cancelled.TrySetResult(true));

            // The timeout is cancelled on the way out, so an initialization that completes in
            // milliseconds does not leave a timer armed for the rest of the timeout.
            using var timeoutCancellation = new CancellationTokenSource();
            var timeout = Task.Delay(_settings.InitializationTimeout, timeoutCancellation.Token);
            var completed = await Task.WhenAny(_firstConfigReceived.Task, timeout, cancelled.Task).ConfigureAwait(false);

            if (completed != timeout)
            {
                timeoutCancellation.Cancel();

                if (completed == _firstConfigReceived.Task)
                {
                    _log.Debug("Feature Flags: provider initialized successfully");
                }
                else
                {
                    _log.Debug("Feature Flags: provider initialization was cancelled before configuration arrived");
                }
            }
            else
            {
                _log.Warning<double>(
                    "Feature Flags configuration did not arrive within {TimeoutMs}ms. Evaluations use their default values until it does.",
                    _settings.InitializationTimeout.TotalMilliseconds);
            }
        }

        internal void RegisterOnNewConfigEventHandler(Action? onNewConfig)
        {
            Action? replay;

            lock (_handlerLock)
            {
                if (!ReferenceEquals(_onNewConfigEventHandler, onNewConfig))
                {
                    // A handler that was never called cannot have received a configuration.
                    _handlerDeliveredGeneration = 0;
                }

                _onNewConfigEventHandler = onNewConfig;

                // Configuration can already be held here: the Remote Configuration subscription is
                // live from construction, long before application code builds a provider and registers
                // a handler. The handler only fires on a change, so without this replay it never runs
                // for a configuration that arrived first, and a caller waiting on it waits forever.
                // A withdrawal is not replayed: no configuration is the state a fresh handler assumes.
                replay = onNewConfig is not null
                      && Volatile.Read(ref _evaluator) is not null
                      && _handlerDeliveredGeneration != _configGeneration
                             ? ClaimHandler()
                             : null;
            }

            InvokeConfigurationHandler(replay, "RegisterOnNewConfigEventHandler");
        }

        internal Evaluation Evaluate(string flagKey, ValueType resultType, object? defaultValue, string? targetingKey, IDictionary<string, object?>? attributes)
        {
            var evaluator = Volatile.Read(ref _evaluator);
            if (evaluator is null)
            {
                _log.Debug<string>("FeatureFlagsModule::Evaluate -> Evaluated {FlagKey}: no configuration received yet, returning the default value (PROVIDER_NOT_READY)", flagKey);
                return new Evaluation(
                    flagKey,
                    defaultValue,
                    EvaluationReason.Error,
                    error: "PROVIDER_NOT_READY",
                    metadata: new Dictionary<string, string>
                    {
                        [FeatureFlagMetadataKeys.ObserveFullEvaluationData] = "false"
                    });
            }

            var evaluation = evaluator.Evaluate(flagKey, resultType, defaultValue, new EvaluationContext(targetingKey, attributes));

            // Every evaluation passes through here, so the metadata lookups are skipped unless the line
            // is actually written. The value and the targeting context are left out: the variant
            // identifies the value, and the context may carry personal data. The error code is logged
            // rather than the error, because the error can be an exception message that quotes a
            // context attribute back.
            if (_log.IsEnabled(LogEventLevel.Debug))
            {
                _log.Debug<string, EvaluationReason, string?, string?, string?>(
                    "FeatureFlagsModule::Evaluate -> Evaluated {FlagKey}: reason={Reason}, variant={Variant}, allocation={Allocation}, error={ErrorCode}",
                    flagKey,
                    evaluation.Reason,
                    evaluation.Variant,
                    FeatureFlagsEvaluator.AllocationKey(evaluation),
                    FeatureFlagsEvaluator.ErrorCode(evaluation));
            }

            return evaluation;
        }

        [TestingAndPrivateOnly]
        internal bool ApplyConfiguration(ServerConfiguration configuration)
        {
            try
            {
                Interlocked.Exchange(ref _evaluator, new FeatureFlagsEvaluator(ReportExposure, configuration, _spanEnrichmentEnabled));
                _firstConfigReceived.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "FeatureFlagsModule::ApplyConfiguration -> Error applying configuration");
                return false;
            }

            NotifyNewConfiguration("ApplyConfiguration");

            return true;
        }

        private static Dictionary<string, string> CreateEvaluationContext(MutableSettings settings)
        {
            return new Dictionary<string, string>
            {
                ["service"] = settings.DefaultServiceName,
                ["env"] = settings.Environment ?? "unknown",
                ["version"] = settings.ServiceVersion ?? "unknown",
            };
        }

        private void SubscribeToEvaluationDelivery()
        {
            if (!_settings.EvaluationEventsEnabled)
            {
                return;
            }

            // Each track owns an instance: an evaluation delivery failure must not change
            // exposure routing or disposal. Both instances reuse the same transport policy.
            var transport = new FeatureFlagsEvpTransport(_tracerSettings, _discoveryService);
            _evaluationTransport = transport;
            var exporter = _settingsManager.InitialExporterSettings;
            var context = CreateEvaluationContext(_settingsManager.InitialMutableSettings);
            AgentConfiguration? agent = null;
            AgentConfiguration? pendingAgent = null;
            _evaluationSettingsSubscription = _settingsManager.SubscribeToChanges(changes =>
            {
                lock (_evaluationLock)
                {
                    if (Volatile.Read(ref _disposed))
                    {
                        return;
                    }

                    // Replay supplies the current values even when only one setting changed.
                    exporter = changes.UpdatedExporter ?? changes.PreviousExporter;
                    // Discovery receives settings before this module and can respond first.
                    // Reconcile that response only with its exact exporter snapshot, not
                    // capabilities retained from an earlier visit to the same address.
                    if (ReferenceEquals(pendingAgent?.DiscoverySettings, exporter))
                    {
                        agent = pendingAgent;
                    }
                    else if (!MatchesCurrentAgent(agent))
                    {
                        agent = null;
                    }

                    pendingAgent = null;
                    if (_evaluationWriter is null)
                    {
                        context = CreateEvaluationContext(changes.UpdatedMutable ?? changes.PreviousMutable);
                    }

                    if (_settings.Source == FeatureFlagsSource.RemoteConfig && changes.UpdatedExporter is not null)
                    {
                        Volatile.Write(ref _availableEvaluationWriter, null);
                    }

                    UpdateAvailability();
                }
            });

            if (_settings.Source != FeatureFlagsSource.RemoteConfig)
            {
                // Agentless collection does not wait for a route. The bounded worker sends
                // through the transport's existing discovery/fallback policy.
                lock (_evaluationLock)
                {
                    UpdateAvailability();
                }

                return;
            }

            _evaluationDiscoveryCallback = configuration =>
            {
                lock (_evaluationLock)
                {
                    if (Volatile.Read(ref _disposed))
                    {
                        return;
                    }

                    if (!MatchesCurrentAgent(configuration))
                    {
                        // This may precede its settings callback or be a late old response.
                        // Keep at most one pending capability snapshot; do not change current
                        // admission until the matching settings update confirms it.
                        pendingAgent = configuration;
                        return;
                    }

                    agent = configuration;
                    UpdateAvailability();
                }
            };
            _discoveryService.SubscribeToChanges(_evaluationDiscoveryCallback);

            // Called only under _evaluationLock. Remote Config retains its Agent capability gate.
            // Agentless uses the existing bounded queue while the transport chooses a route.
            void UpdateAvailability()
            {
                if (_settings.Source == FeatureFlagsSource.RemoteConfig
                 && (StringUtil.IsNullOrEmpty(agent?.EventPlatformProxyEndpoint) || !MatchesCurrentAgent(agent)))
                {
                    Volatile.Write(ref _availableEvaluationWriter, null);
                    return;
                }

                if (_evaluationWriter is null)
                {
                    try
                    {
                        // Keep tags for this writer's lifetime; changing the Agent must not
                        // relabel observations that have already been accepted.
                        var writer = new FlagEvaluationWriter(transport.SendFlagEvaluationsAsync, context);
                        Volatile.Write(ref _evaluationWriter, writer);
                    }
                    catch (Exception)
                    {
                        Log.Debug("FeatureFlags flagevaluation writer could not be started.");
                    }
                }

                Volatile.Write(ref _availableEvaluationWriter, _evaluationWriter);
            }

            bool MatchesCurrentAgent(AgentConfiguration? configuration)
            {
                // Returning to the same address creates a new exporter generation. A delayed
                // callback from an earlier visit must not change the current admission state.
                return ReferenceEquals(configuration?.DiscoverySettings, exporter);
            }
        }

        private async Task FinishDisposalAsync(
            Task close,
            FeatureFlagsEvpTransport? evaluationTransport,
            IDisposable? settingsSubscription,
            Action<AgentConfiguration>? discoveryCallback,
            ISubscription? subscription,
            IFeatureFlagsDeliverySource? agentlessSource,
            ExposureApi? exposureApi,
            FeatureFlagsEvpTransport? evpTransport)
        {
            try
            {
                settingsSubscription?.Dispose();
                if (discoveryCallback is not null)
                {
                    _discoveryService.RemoveSubscription(discoveryCallback);
                }

                if (subscription is not null)
                {
                    _rcmSubscriptionManager.Unsubscribe(subscription);
                }

                agentlessSource?.Dispose();
                exposureApi?.Dispose();
            }
            catch (Exception)
            {
                Log.Debug("FeatureFlags cleanup failed; continuing bounded flagevaluation shutdown.");
            }
            finally
            {
                try
                {
                    await close.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    Log.Debug("FeatureFlags flagevaluation shutdown failed.");
                }
                finally
                {
                    // Dispose stops new sends but preserves any request still live after the deadline.
                    evaluationTransport?.Dispose();
                    evpTransport?.Dispose();
                    _disposeCompletion.TrySetResult(true);
                }
            }
        }

        /// <summary>
        /// Hands one configuration event to the registered handler, whether a configuration arrived or
        /// was withdrawn. Claiming the event under the lock and calling the handler outside it keeps
        /// application code off the lock, and stops a concurrent registration from replaying the same
        /// configuration the handler has just been given.
        /// </summary>
        private void NotifyNewConfiguration(string caller)
        {
            Action? handler;

            lock (_handlerLock)
            {
                _configGeneration++;
                handler = _onNewConfigEventHandler is null ? null : ClaimHandler();
            }

            InvokeConfigurationHandler(handler, caller);
        }

        /// <summary>
        /// Records the current configuration as delivered to the current handler and returns that
        /// handler. Callers hold <see cref="_handlerLock"/>.
        /// </summary>
        private Action? ClaimHandler()
        {
            _handlerDeliveredGeneration = _configGeneration;
            return _onNewConfigEventHandler;
        }

        /// <summary>
        /// Calls the handler, which is application code. The agentless source reads the return value of
        /// <see cref="ApplyConfiguration"/> to decide whether to advance its ETag. Reporting a failed
        /// apply because a handler threw would make every later poll re-download the whole payload
        /// instead of getting a 304, and the configuration is applied before the handler runs anyway.
        /// </summary>
        private void InvokeConfigurationHandler(Action? handler, string caller)
        {
            if (handler is null)
            {
                return;
            }

            try
            {
                handler();
            }
            catch (Exception ex)
            {
                _log.Warning<string>(ex, "FeatureFlagsModule::{Caller} -> Error in the configuration event handler", caller);
            }
        }

        /// <summary>
        /// Subscribes to the Remote Configuration product carrying flag configuration. Called at
        /// startup, not on activation: the configuration arrives on the poll the agent already makes,
        /// so subscribing early adds no request, and every other tracer subscribes at startup too.
        /// Deferring it made .NET the one tracer that never advertised the capability until the
        /// application resolved a flag.
        /// </summary>
        private void SubscribeToRemoteConfiguration()
        {
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                if (!_isRemoteConfigurationAvailable)
                {
                    _log.Warning("Feature Flags are configured to use the Remote Configuration source, but Remote Configuration is not available. No flag configuration will be received.");
                    _deliveryUnavailableReason = "the Remote Configuration source is selected, but Remote Configuration is not available in this environment";
                    return;
                }

                var ffeProduct = new FfeProduct(ApplyRemoteConfigurations);
                _rcmSubscription = new Subscription(ffeProduct.UpdateFromRcm, RcmProducts.FfeFlags);
                _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription);
                _rcmSubscriptionManager.SetCapability(RcmCapabilitiesIndices.FfeFlagConfigurationRules, true);
                _deliveryStarted = true;
                _log.Debug("FeatureFlagsModule::SubscribeToRemoteConfiguration -> Starting remote_config configuration source (Agent Remote Configuration)");
            }
        }

        private void ApplyRemoteConfigurations(List<KeyValuePair<string, ServerConfiguration>> list)
        {
            _log.Debug<int>("FeatureFlagsModule::ApplyRemoteConfigurations -> remote_config configuration received ({FileCount} file(s))", list.Count);
            try
            {
                if (list.Count > 0)
                {
                    var selectedConfig = MergeConfigs(list);

                    // Logged only once applied: "applied" must not be reported for a configuration
                    // the evaluator rejected, which ApplyConfiguration already warns about.
                    if (ApplyConfiguration(selectedConfig))
                    {
                        _log.Debug<int, string>(
                            "FeatureFlagsModule::ApplyRemoteConfigurations -> remote_config configuration applied successfully ({FlagCount} flag(s), configuration env={Environment})",
                            selectedConfig.Flags?.Count ?? 0,
                            selectedConfig.Environment?.Name ?? "unset");
                    }
                }
                else
                {
                    // The configuration was withdrawn, so every evaluation returns PROVIDER_NOT_READY
                    // from here on. The handler is notified either way, and reads HasConfiguration to
                    // tell a withdrawal from an update.
                    Interlocked.Exchange(ref _evaluator, null);
                    _log.Debug("FeatureFlagsModule::ApplyRemoteConfigurations -> remote_config configuration removed; evaluations return default values until a new configuration is applied");
                    NotifyNewConfiguration("ApplyRemoteConfigurations");
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "FeatureFlagsModule::ApplyRemoteConfigurations -> Error processing new config");
            }

            static ServerConfiguration MergeConfigs(List<KeyValuePair<string, ServerConfiguration>> list)
            {
                if (list.Count == 1)
                {
                    return list[0].Value;
                }

                // Recompute from the active files, not the previous merged result. Seeding
                // from the first file keeps the configuration's default consent false.
                var res = new ServerConfiguration { ObserveFullEvaluationData = list[0].Value.ObserveFullEvaluationData };
                foreach (var conf in list)
                {
                    res.Merge(conf.Value);
                }

                return res;
            }
        }

        private void ReportExposure(in ExposureEvent exposure)
        {
            // Nothing would ever dispose an API created after disposal, leaving its send loop running
            // for the rest of the process, so exposures are dropped from that point on.
            GetExposureApi()?.SendExposure(exposure);
        }

        // Created on first use because most applications never evaluate a flag, and under the lock
        // because the evaluation path races disposal. Only the very first exposure takes the lock:
        // afterwards the field is read directly, keeping the exposure path lock-free.
        [TestingAndPrivateOnly]
        internal ExposureApi? GetExposureApi()
        {
            var exposureApi = Volatile.Read(ref _exposureApi);
            if (exposureApi is not null)
            {
                return exposureApi;
            }

            lock (_stateLock)
            {
                if (_disposed)
                {
                    return null;
                }

                exposureApi = _exposureApi;
                if (exposureApi is null)
                {
                    // Keep the HTTP client and its settings subscription lazy with the first exposure.
                    _evpTransport = new FeatureFlagsEvpTransport(_tracerSettings, _discoveryService);
                    exposureApi = new ExposureApi(_tracerSettings, _evpTransport);
                    Volatile.Write(ref _exposureApi, exposureApi);
                }

                return exposureApi;
            }
        }
    }
}
