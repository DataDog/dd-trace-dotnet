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
        private readonly Func<ExposureApi> _exposureApiFactory;
        private readonly bool _spanEnrichmentEnabled;
        private readonly IRcmSubscriptionManager _rcmSubscriptionManager;
        private readonly TaskCompletionSource<bool> _firstConfigReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ISubscription? _rcmSubscription;

        private Action? _onNewConfigEventHander;
        private FeatureFlagsEvaluator? _evaluator;
        private AgentlessConfigurationSource? _agentlessSource;
        private ExposureApi? _exposureApi;
        private string? _deliveryUnavailableReason;
        private bool _activated;
        private bool _disposed;
        private bool _deliveryStarted;

        internal FeatureFlagsModule(TracerSettings settings, IRcmSubscriptionManager rcmSubscriptionManager)
        {
            _settings = settings.FeatureFlags;
            _settingsManager = settings.Manager;
            _isRemoteConfigurationAvailable = settings.IsRemoteConfigurationAvailable;
            _spanEnrichmentEnabled = settings.IsSpanEnrichmentEnabled;
            _exposureApiFactory = () => new ExposureApi(settings);
            _rcmSubscriptionManager = rcmSubscriptionManager;

            Log.Debug<FeatureFlagsSource>("FeatureFlagsModule ENABLED with source {Source}", _settings.Source);
        }

        /// <summary>
        /// Gets a task that completes once configuration has been applied for the first time.
        /// </summary>
        internal Task FirstConfigReceived => _firstConfigReceived.Task;

        internal FeatureFlagsSettings Settings => _settings;

        public static FeatureFlagsModule? Create(TracerSettings settings, IRcmSubscriptionManager rcmSubscriptionManager)
        {
            if (!settings.FeatureFlags.Enabled)
            {
                return null;
            }

            return new FeatureFlagsModule(settings, rcmSubscriptionManager);
        }

        public void Dispose()
        {
            ISubscription? subscription;
            AgentlessConfigurationSource? agentlessSource;
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
        /// Signals that application code initialized the provider. Configuration is only requested
        /// from this point on, because those requests are billable. Idempotent.
        /// </summary>
        internal void Activate()
        {
            AgentlessConfigurationSource? sourceToStart = null;

            lock (_stateLock)
            {
                if (_activated || _disposed)
                {
                    return;
                }

                _activated = true;

                switch (_settings.Source)
                {
                    case FeatureFlagsSource.RemoteConfig:
                        // Remote Configuration subscription is deferred to activation so that merely
                        // enabling Feature Flags does not start a billed RC subscription.
                        if (!_isRemoteConfigurationAvailable)
                        {
                            Log.Warning("Feature Flags are configured to use the Remote Configuration source, but Remote Configuration is not available. No flag configuration will be received.");
                            _deliveryUnavailableReason = "the Remote Configuration source is selected, but Remote Configuration is not available in this environment";
                            break;
                        }

                        var ffeProduct = new FfeProduct(configs => ApplyConfigurations(configs));
                        _rcmSubscription = new Subscription(ffeProduct.UpdateFromRcm, RcmProducts.FfeFlags);
                        _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription);
                        _rcmSubscriptionManager.SetCapability(RcmCapabilitiesIndices.FfeFlagConfigurationRules, true);
                        _deliveryStarted = true;
                        Log.Debug("FeatureFlagsModule::Activate -> Remote Configuration source subscribed");
                        break;
                    case FeatureFlagsSource.Agentless:
                        // Polling is billable, so it starts here rather than at construction.
                        var source = AgentlessConfigurationSource.Create(_settings, _settingsManager, ApplyConfiguration);
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

            // Start() issues the first request on the calling thread up to its first await, so it runs
            // outside the lock: holding it across a network request would block Dispose(). A Dispose()
            // that interleaves here has already cancelled the source's shutdown token, which makes the
            // poll loop exit before its first request.
            sourceToStart?.Start();
        }

        /// <summary>
        /// Activates delivery and waits for the first configuration, so that a provider reported as
        /// ready can resolve flags.
        /// <para>
        /// Returns without throwing when the wait times out, because delivery being slow is transient:
        /// the configuration still arrives later and promotes the provider. Throws
        /// <see cref="FeatureFlagsDeliveryUnavailableException"/> when no source could start at all,
        /// which is permanent for the life of the process and must not be reported as a ready provider.
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

            // When activation could not start any delivery source (for example, agentless without an
            // API key), no configuration can ever arrive, so waiting would only delay startup.
            // Returning instead would report the provider as usable while every evaluation keeps
            // returning its default, so the failure is raised: the SDK turns it into an error status
            // and an error event without taking the application down.
            if (!deliveryStarted)
            {
                throw new FeatureFlagsDeliveryUnavailableException(deliveryUnavailableReason);
            }

            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timeout = Task.Delay(_settings.InitializationTimeout, timeoutCancellation.Token);
            var completed = await Task.WhenAny(_firstConfigReceived.Task, timeout).ConfigureAwait(false);

            // Stop the timer, otherwise it holds a callback for the whole initialization timeout.
            timeoutCancellation.Cancel();

            if (completed != timeout)
            {
                return;
            }

            // Evaluations keep returning the caller's default with PROVIDER_NOT_READY until
            // configuration lands, which promotes the provider then.
            Log.Warning<double>(
                "Feature Flags configuration did not arrive within {TimeoutMs}ms. Evaluations use their default values until it does.",
                _settings.InitializationTimeout.TotalMilliseconds);
        }

        internal void RegisterOnNewConfigEventHandler(Action? onNewConfig)
        {
            _onNewConfigEventHander = onNewConfig;
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

        internal bool ApplyConfiguration(ServerConfiguration configuration)
        {
            try
            {
                Interlocked.Exchange(ref _evaluator, new FeatureFlagsEvaluator(ReportExposure, configuration, _spanEnrichmentEnabled));
                _firstConfigReceived.TrySetResult(true);
                _onNewConfigEventHander?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FeatureFlagsModule::ApplyConfiguration -> Error applying configuration");
                return false;
            }
        }

        private void ApplyConfigurations(List<KeyValuePair<string, ServerConfiguration>> list)
        {
            Log.Debug<int>("FeatureFlagsModule::ApplyConfigurations -> New config received. {Count}", list.Count);
            try
            {
                if (list.Count > 0)
                {
                    var selectedConfig = MergeConfigs(list);
                    ApplyConfiguration(selectedConfig);
                }
                else
                {
                    // RC reset: clear evaluator so Evaluate() returns PROVIDER_NOT_READY
                    Interlocked.Exchange(ref _evaluator, null);
                    _onNewConfigEventHander?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FeatureFlagsModule::ApplyConfigurations -> Error processing new config");
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
        // afterwards the field is read directly, keeping the evaluation path lock-free. Internal so
        // tests can assert the disposal behaviour without starting a send loop.
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
                    exposureApi = _exposureApiFactory();
                    Volatile.Write(ref _exposureApi, exposureApi);
                }

                return exposureApi;
            }
        }
    }
}
