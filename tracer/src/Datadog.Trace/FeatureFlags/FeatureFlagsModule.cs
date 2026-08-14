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

        private readonly FeatureFlagsSettings _settings;
        private readonly bool _isRemoteConfigurationAvailable;
        private readonly Lazy<ExposureApi> _exposureApi;
        private readonly bool _spanEnrichmentEnabled;
        private readonly IRcmSubscriptionManager? _rcmSubscriptionManager;
        private readonly TaskCompletionSource<bool> _firstConfigReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ISubscription? _rcmSubscription;

        private Action? _onNewConfigEventHander;
        private FeatureFlagsEvaluator? _evaluator;
        private AgentlessConfigurationSource? _agentlessSource;
        private int _activated;
        private int _disposed;
        private bool _deliveryStarted;

        internal FeatureFlagsModule(TracerSettings settings, IRcmSubscriptionManager rcmSubscriptionManager)
        {
            _settings = settings.FeatureFlags;
            _isRemoteConfigurationAvailable = settings.IsRemoteConfigurationAvailable;
            _spanEnrichmentEnabled = settings.IsSpanEnrichmentEnabled;
            _exposureApi = new Lazy<ExposureApi>(() => new ExposureApi(settings));
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
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            if (_rcmSubscriptionManager is not null && _rcmSubscription is not null)
            {
                _rcmSubscriptionManager.Unsubscribe(_rcmSubscription);
            }

            Interlocked.Exchange(ref _agentlessSource, null)?.Dispose();
            if (_exposureApi.IsValueCreated)
            {
                _exposureApi.Value.Dispose();
            }
        }

        /// <summary>
        /// Signals that application code initialized the provider. Configuration is only requested
        /// from this point on, because those requests are billable. Idempotent.
        /// </summary>
        internal void Activate()
        {
            if (Interlocked.CompareExchange(ref _activated, 1, 0) != 0)
            {
                return;
            }

            switch (_settings.Source)
            {
                case FeatureFlagsSource.RemoteConfig:
                    // Remote Configuration subscription is deferred to activation so that merely
                    // enabling Feature Flags does not start a billed RC subscription.
                    if (!_isRemoteConfigurationAvailable)
                    {
                        Log.Warning("Feature Flags are configured to use the Remote Configuration source, but Remote Configuration is not available. No flag configuration will be received.");
                        break;
                    }

                    var ffeProduct = new FfeProduct(configs => ApplyConfigurations(configs));
                    _rcmSubscription = new Subscription(ffeProduct.UpdateFromRcm, RcmProducts.FfeFlags);
                    _rcmSubscriptionManager!.SubscribeToChanges(_rcmSubscription!);
                    _rcmSubscriptionManager.SetCapability(RcmCapabilitiesIndices.FfeFlagConfigurationRules, true);
                    _deliveryStarted = true;
                    Log.Debug("FeatureFlagsModule::Activate -> Remote Configuration source subscribed");
                    break;
                case FeatureFlagsSource.Agentless:
                    // Polling is billable, so it starts here rather than at construction.
                    var source = AgentlessConfigurationSource.Create(_settings, ApplyConfiguration);
                    if (source is null)
                    {
                        break;
                    }

                    Interlocked.Exchange(ref _agentlessSource, source);
                    if (Volatile.Read(ref _disposed) == 1)
                    {
                        // Disposed while we were creating it.
                        Interlocked.Exchange(ref _agentlessSource, null)?.Dispose();
                        break;
                    }

                    source.Start();
                    _deliveryStarted = true;
                    break;
            }
        }

        /// <summary>
        /// Activates delivery and waits for the first configuration, so that a provider reported as
        /// ready can resolve flags. Never throws on timeout: delivery being slow is transient, while
        /// initialization typically runs at application startup where an exception is fatal.
        /// </summary>
        internal async Task InitializeAsync(CancellationToken cancellationToken)
        {
            Activate();

            if (_firstConfigReceived.Task.IsCompleted)
            {
                return;
            }

            // When activation could not start any delivery source (for example, agentless without
            // an API key), waiting would only delay startup for a configuration that will never
            // arrive. The provider stays not-ready, which is the correct state.
            if (!_deliveryStarted)
            {
                return;
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
            _exposureApi.Value.SendExposure(exposure);
        }
    }
}
