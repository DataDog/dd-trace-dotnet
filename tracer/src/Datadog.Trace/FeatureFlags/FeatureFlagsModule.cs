// <copyright file="FeatureFlagsModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Agentless;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.FeatureFlags.Rcm;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.SourceGenerators;

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

        private readonly FeatureFlagsSettings _settings;

        // The agentless source targets flags by environment, which customers can change in code
        // after startup, so it needs the manager rather than a captured value.
        private readonly TracerSettings.SettingsManager _settingsManager;
        private readonly bool _isRemoteConfigurationAvailable;

        // ExposureApi reads only settings.Manager but takes TracerSettings. Held so the API can be
        // built on the first exposure instead of at startup.
        private readonly TracerSettings _tracerSettings;

        // A factory rather than the static Create, so a test can supply a source that records what the
        // module does with it: whether it is started before activation, and whether it is disposed.
        private readonly Func<FeatureFlagsModule, IFeatureFlagsDeliverySource?> _agentlessSourceFactory;
        private readonly bool _spanEnrichmentEnabled;
        private readonly IRcmSubscriptionManager _rcmSubscriptionManager;
        private readonly TaskCompletionSource<bool> _firstConfigReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ISubscription? _rcmSubscription;

        private Action? _onNewConfigEventHandler;
        private FeatureFlagsEvaluator? _evaluator;
        private IFeatureFlagsDeliverySource? _agentlessSource;
        private ExposureApi? _exposureApi;
        private string? _deliveryUnavailableReason;
        private bool _activated;
        private bool _disposed;
        private bool _deliveryStarted;

        internal FeatureFlagsModule(
            TracerSettings settings,
            IRcmSubscriptionManager rcmSubscriptionManager,
            Func<FeatureFlagsModule, IFeatureFlagsDeliverySource?>? agentlessSourceFactory = null)
        {
            _settings = settings.FeatureFlags;
            _settingsManager = settings.Manager;
            _isRemoteConfigurationAvailable = settings.IsRemoteConfigurationAvailable;
            _spanEnrichmentEnabled = settings.IsSpanEnrichmentEnabled;
            _tracerSettings = settings;
            _agentlessSourceFactory = agentlessSourceFactory
                                   ?? (static module => AgentlessConfigurationSource.Create(module._settings, module._settingsManager, module.ApplyConfiguration));
            _rcmSubscriptionManager = rcmSubscriptionManager;

            Log.Debug<FeatureFlagsSource>("FeatureFlagsModule ENABLED with source {Source}", _settings.Source);
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

        public static FeatureFlagsModule? Create(
            TracerSettings settings,
            IRcmSubscriptionManager rcmSubscriptionManager,
            Func<FeatureFlagsModule, IFeatureFlagsDeliverySource?>? agentlessSourceFactory = null)
        {
            if (!settings.FeatureFlags.Enabled)
            {
                return null;
            }

            var module = new FeatureFlagsModule(settings, rcmSubscriptionManager, agentlessSourceFactory);

            // Subscribing from here rather than the constructor, so the callback can only ever reach
            // a fully constructed module.
            if (settings.FeatureFlags.Source == FeatureFlagsSource.RemoteConfig)
            {
                module.SubscribeToRemoteConfiguration();
            }

            return module;
        }

        public void Dispose()
        {
            ISubscription? subscription;
            IFeatureFlagsDeliverySource? agentlessSource;
            ExposureApi? exposureApi;

            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                subscription = _rcmSubscription;
                agentlessSource = _agentlessSource;
                exposureApi = _exposureApi;

                _rcmSubscription = null;
                _agentlessSource = null;
                Volatile.Write(ref _exposureApi, null);
            }

            // Released the lock first: disposal is not state mutation, and holding it here would
            // block an activation or an exposure for the duration.
            if (subscription is not null)
            {
                _rcmSubscriptionManager.Unsubscribe(subscription);
            }

            agentlessSource?.Dispose();
            exposureApi?.Dispose();
        }

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

                _activated = true;

                switch (_settings.Source)
                {
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
                throw new FeatureFlagsDeliveryUnavailableException(deliveryUnavailableReason);
            }

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
            }
            else
            {
                Log.Warning<double>(
                    "Feature Flags configuration did not arrive within {TimeoutMs}ms. Evaluations use their default values until it does.",
                    _settings.InitializationTimeout.TotalMilliseconds);
            }
        }

        internal void RegisterOnNewConfigEventHandler(Action? onNewConfig)
        {
            _onNewConfigEventHandler = onNewConfig;
        }

        internal Evaluation Evaluate(string flagKey, ValueType resultType, object? defaultValue, string targetingKey, IDictionary<string, object?>? attributes)
        {
            var evaluator = Volatile.Read(ref _evaluator);
            if (evaluator is null)
            {
                Log.Debug("FeatureFlagsModule::Evaluate -> Evaluator is null (no config received)");
                return new Evaluation(flagKey, null, EvaluationReason.Error, null, "PROVIDER_NOT_READY");
            }

            Log.Debug("FeatureFlagsModule::Evaluate -> Returning Evaluation");
            return evaluator.Evaluate(flagKey, resultType, defaultValue, new EvaluationContext(targetingKey, attributes));
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
                Log.Warning(ex, "FeatureFlagsModule::ApplyConfiguration -> Error applying configuration");
                return false;
            }

            // The handler comes from application code, and the agentless source reads the return value
            // to decide whether to advance its ETag. Reporting a failed apply because a handler threw
            // would make every later poll re-download the whole payload instead of getting a 304, so
            // the configuration is already applied by this point and the handler cannot change that.
            try
            {
                _onNewConfigEventHandler?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FeatureFlagsModule::ApplyConfiguration -> Error in the configuration event handler");
            }

            return true;
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
                    Log.Warning("Feature Flags are configured to use the Remote Configuration source, but Remote Configuration is not available. No flag configuration will be received.");
                    _deliveryUnavailableReason = "the Remote Configuration source is selected, but Remote Configuration is not available in this environment";
                    return;
                }

                var ffeProduct = new FfeProduct(ApplyRemoteConfigurations);
                _rcmSubscription = new Subscription(ffeProduct.UpdateFromRcm, RcmProducts.FfeFlags);
                _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription);
                _rcmSubscriptionManager.SetCapability(RcmCapabilitiesIndices.FfeFlagConfigurationRules, true);
                _deliveryStarted = true;
                Log.Debug("FeatureFlagsModule::SubscribeToRemoteConfiguration -> Remote Configuration source subscribed");
            }
        }

        private void ApplyRemoteConfigurations(List<KeyValuePair<string, ServerConfiguration>> list)
        {
            Log.Debug<int>("FeatureFlagsModule::ApplyRemoteConfigurations -> New config received. {Count}", list.Count);
            try
            {
                if (list.Count > 0)
                {
                    var selectedConfig = MergeConfigs(list);
                    ApplyConfiguration(selectedConfig);
                }
                else
                {
                    // The configuration was withdrawn, so every evaluation returns PROVIDER_NOT_READY
                    // from here on. The handler is notified either way, and reads HasConfiguration to
                    // tell a withdrawal from an update.
                    Interlocked.Exchange(ref _evaluator, null);
                    _onNewConfigEventHandler?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FeatureFlagsModule::ApplyRemoteConfigurations -> Error processing new config");
            }

            static ServerConfiguration MergeConfigs(List<KeyValuePair<string, ServerConfiguration>> list)
            {
                if (list.Count == 1)
                {
                    return list[0].Value;
                }

                var res = new ServerConfiguration();
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
                    exposureApi = new ExposureApi(_tracerSettings);
                    Volatile.Write(ref _exposureApi, exposureApi);
                }

                return exposureApi;
            }
        }
    }
}
