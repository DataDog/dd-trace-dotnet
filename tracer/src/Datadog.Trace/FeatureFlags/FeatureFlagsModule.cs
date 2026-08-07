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
        private readonly ExposureApi _exposureApi;
        private readonly bool _spanEnrichmentEnabled;

        // Signals that configuration has been applied at least once. Provider initialization awaits
        // this so that "ready" implies flags can actually be resolved.
        private readonly TaskCompletionSource<bool> _firstConfigReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly IRcmSubscriptionManager? _rcmSubscriptionManager;
        private readonly ISubscription? _rcmSubscription;

        private Action? _onNewConfigEventHander;
        private FeatureFlagsEvaluator? _evaluator;
        private int _activated;

        internal FeatureFlagsModule(TracerSettings settings, IRcmSubscriptionManager rcmSubscriptionManager)
        {
            _settings = settings.FeatureFlags;
            _spanEnrichmentEnabled = settings.IsSpanEnrichmentEnabled;
            _exposureApi = new ExposureApi(settings);

            Log.Debug<FeatureFlagsSource>("FeatureFlagsModule ENABLED with source {Source}", _settings.Source);

            if (_settings.Source == FeatureFlagsSource.RemoteConfig)
            {
                if (!settings.IsRemoteConfigurationAvailable)
                {
                    // Selecting Remote Configuration explicitly and then falling back to the agentless
                    // endpoint would start billed requests the customer did not ask for, so the source
                    // stays selected and simply never delivers.
                    Log.Warning(
                        "Feature Flags are configured to use the Remote Configuration source, but Remote Configuration is not available. No flag configuration will be received.");
                }

                // Subscribing advertises the FFE capability and starts a billed Remote Configuration
                // subscription, so it only happens when Remote Configuration is the selected source.
                // The subscription keeps the product alive, so it needs no field of its own.
                var ffeProduct = new FfeProduct(configs => ApplyConfigurations(configs));
                _rcmSubscriptionManager = rcmSubscriptionManager;
                _rcmSubscription = new Subscription(ffeProduct.UpdateFromRcm, RcmProducts.FfeFlags);
                _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription);
                _rcmSubscriptionManager.SetCapability(RcmCapabilitiesIndices.FfeFlagConfigurationRules, true);
            }
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
            if (_rcmSubscriptionManager is not null && _rcmSubscription is not null)
            {
                _rcmSubscriptionManager.Unsubscribe(_rcmSubscription);
            }

            _exposureApi.Dispose();
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
                    // Remote Configuration delivery is driven by the Agent, so the subscription set up
                    // in the constructor is all that is needed; activation only marks intent.
                    Log.Debug("FeatureFlagsModule::Activate -> Remote Configuration source is already subscribed");
                    break;
                case FeatureFlagsSource.Agentless:
                    Log.Debug("FeatureFlagsModule::Activate -> Agentless source is not yet wired up");
                    break;
            }
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

        /// <summary>
        /// Applies a single configuration document. Used by delivery sources that produce one
        /// document at a time, such as the agentless source.
        /// </summary>
        /// <returns><c>true</c> when the configuration was applied.</returns>
        internal bool ApplyConfiguration(ServerConfiguration configuration)
            => ApplyConfigurations([new KeyValuePair<string, ServerConfiguration>(string.Empty, configuration)]);

        /// <summary>
        /// Applies the current set of configuration documents, replacing the evaluator. An empty list
        /// clears it, so evaluations report that the provider is not ready.
        /// </summary>
        /// <returns><c>true</c> when the configurations were applied.</returns>
        internal bool ApplyConfigurations(List<KeyValuePair<string, ServerConfiguration>> list)
        {
            Log.Debug<int>("FeatureFlagsModule::ApplyConfigurations -> New config received. {Count}", list.Count);
            try
            {
                if (list.Count > 0)
                {
                    var selectedConfig = MergeConfigs(list);
                    Interlocked.Exchange(ref _evaluator, new FeatureFlagsEvaluator(ReportExposure, selectedConfig, _spanEnrichmentEnabled));
                    _firstConfigReceived.TrySetResult(true);
                }
                else
                {
                    // RC reset: clear evaluator so Evaluate() returns PROVIDER_NOT_READY
                    Interlocked.Exchange(ref _evaluator, null);
                }

                _onNewConfigEventHander?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FeatureFlagsModule::ApplyConfigurations -> Error processing new config");
                return false;
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
            _exposureApi?.SendExposure(exposure);
        }
    }
}
