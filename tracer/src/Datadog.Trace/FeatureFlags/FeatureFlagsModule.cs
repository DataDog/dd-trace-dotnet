// <copyright file="FeatureFlagsModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Exposure;
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
        private ISubscription? _rcmSubscription = null;
        private FfeProduct? _ffeProduct = null;
        private Action? _onNewConfigEventHander = null;
        private ExposureApi? _exposureApi = null;

        private FeatureFlagsEvaluator? _evaluator = null;

        internal FeatureFlagsModule(TracerSettings settings, IRcmSubscriptionManager? rcmSubscriptionManager = null)
        {
            Log.Debug("FeatureFlagsModule ENABLED");
            _rcmSubscriptionManager = rcmSubscriptionManager ?? RcmSubscriptionManager.Instance;
            _exposureApi = ExposureApi.Create(settings);
            _ffeProduct = new FfeProduct(UpdateRemoteConfig);
            if (Interlocked.Exchange(ref _rcmSubscription, new Subscription(_ffeProduct.UpdateFromRcm, RcmProducts.FfeFlags)) == null)
            {
                _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription!);
                _rcmSubscriptionManager.SetCapability(RcmCapabilitiesIndices.FfeFlagConfigurationRules, true);
            }
        }

        public static FeatureFlagsModule? Create(TracerSettings settings, IRcmSubscriptionManager? rcmSubscriptionManager = null)
        {
            if (settings.IsFlaggingProviderEnabled)
            {
                return new FeatureFlagsModule(settings, rcmSubscriptionManager ?? RcmSubscriptionManager.Instance);
            }

            return null;
        }

        public void Dispose()
        {
            _exposureApi?.Dispose();
        }

        internal void RegisterOnNewConfigEventHandler(Action? onNewConfig)
        {
            _onNewConfigEventHander = onNewConfig;
        }

        internal Evaluation Evaluate(string flagKey, ValueType resultType, object? defaultValue, IEvaluationContext? context)
        {
            if (_evaluator is null)
            {
                Log.Debug("FeatureFlagsModule::Evaluate -> Evaluator is null (no config received)");
                return new Evaluation(flagKey, null, EvaluationReason.Error, null, "No config loaded");
            }

            Log.Debug("FeatureFlagsModule::Evaluate -> Returning Evaluation");
            return _evaluator.Evaluate(flagKey, resultType, defaultValue, context);
        }

        private void UpdateRemoteConfig(List<KeyValuePair<string, ServerConfiguration>> list)
        {
            Log.Debug<int>("FeatureFlagsModule::UpdateRemoteConfig -> New config received. {Count}", list.Count);
            try
            {
                // Feed configs to the rules evaluator
                if (list.Count > 0)
                {
                    Interlocked.Exchange(ref _evaluator, new FeatureFlagsEvaluator(ReportExposure, list[0].Value));
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
