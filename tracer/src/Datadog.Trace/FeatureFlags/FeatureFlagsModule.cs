// <copyright file="FeatureFlagsModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
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

        private readonly IRcmSubscriptionManager _rcmSubscriptionManager;
        private readonly ISubscription _rcmSubscription;
        private readonly FfeProduct _ffeProduct;
        private readonly ExposureApi _exposureApi;

        private Action? _onNewConfigEventHander = null;
        private FeatureFlagsEvaluator? _evaluator = null;

        internal FeatureFlagsModule(TracerSettings settings, IRcmSubscriptionManager rcmSubscriptionManager)
        {
            Log.Debug("FeatureFlagsModule ENABLED");
            _rcmSubscriptionManager = rcmSubscriptionManager;
            _exposureApi = new ExposureApi(settings);
            _ffeProduct = new FfeProduct(UpdateRemoteConfig);
            _rcmSubscription = new Subscription(_ffeProduct.UpdateFromRcm, RcmProducts.FfeFlags);
            _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription!);
            _rcmSubscriptionManager.SetCapability(RcmCapabilitiesIndices.FfeFlagConfigurationRules, true);
        }

        public static FeatureFlagsModule? Create(TracerSettings settings, IRcmSubscriptionManager rcmSubscriptionManager)
        {
            if (settings.IsFlaggingProviderEnabled)
            {
                return new FeatureFlagsModule(settings, rcmSubscriptionManager);
            }

            return null;
        }

        public void Dispose()
        {
            _exposureApi.Dispose();
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
                return new Evaluation(flagKey, null, EvaluationReason.Error, null, "No config loaded");
            }

            Log.Debug("FeatureFlagsModule::Evaluate -> Returning Evaluation");
            return evaluator.Evaluate(flagKey, resultType, defaultValue, new EvaluationContext(targetingKey, attributes));
        }

        private void UpdateRemoteConfig(List<KeyValuePair<string, ServerConfiguration>> list)
        {
            Log.Debug<int>("FeatureFlagsModule::UpdateRemoteConfig -> New config received. {Count}", list.Count);
            try
            {
                // Feed configs to the rules evaluator (take only the last one)
                if (list.Count > 0)
                {
                    var selectedConfig = list[list.Count - 1].Value;
                    Interlocked.Exchange(ref _evaluator, new FeatureFlagsEvaluator(ReportExposure, selectedConfig));
                    _onNewConfigEventHander?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FeatureFlagsModule::UpdateRemoteConfig -> Error processing new config");
            }
        }

        private void ReportExposure(in ExposureEvent exposure)
        {
            _exposureApi?.SendExposure(exposure);
        }
    }
}
