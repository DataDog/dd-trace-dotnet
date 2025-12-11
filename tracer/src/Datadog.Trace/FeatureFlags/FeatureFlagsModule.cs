// <copyright file="FeatureFlagsModule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Grpc.GrpcDotNet.GrpcAspNetCoreServer;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.FeatureFlags.Rcm;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.FeatureFlags
{
    internal class FeatureFlagsModule
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsModule));

        private static FeatureFlagsModule? _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();
        private static bool _enabled = false;

        private readonly IRcmSubscriptionManager _rcmSubscriptionManager;
        private ISubscription? _rcmSubscription = null;
        private FfeProduct? _ffeProduct = null;
        private Action? _onNewConfigEventHander = null;
        private ExposureApi? _exposureApi = null;

        private FeatureFlagsEvaluator? _evaluator = null;

        static FeatureFlagsModule()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureFlagsModule"/> class with default settings.
        /// </summary>
        public FeatureFlagsModule()
            : this(TracerSettings.FromDefaultSourcesInternal(), null)
        {
        }

        internal FeatureFlagsModule(TracerSettings settings, IRcmSubscriptionManager? rcmSubscriptionManager = null)
        {
            _rcmSubscriptionManager = rcmSubscriptionManager ?? RcmSubscriptionManager.Instance;

            if (settings.IsFlaggingProviderEnabled)
            {
                Log.Debug("FeatureFlagsModule ENABLED");
                _enabled = true;
                _ffeProduct = new FfeProduct(UpdateConfig);
                if (Interlocked.Exchange(ref _rcmSubscription, new Subscription(_ffeProduct.UpdateFromRcm, RcmProducts.FfeFlags)) == null)
                {
                    _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription!);
                    _rcmSubscriptionManager.SetCapability(RcmCapabilitiesIndices.FfeFlagConfigurationRules, true);
                }

                _exposureApi = ExposureApi.Create(settings);
            }
        }

        /// <summary>
        /// Gets or sets the global <see cref="Iast"/> instance.
        /// </summary>
        public static FeatureFlagsModule Instance
        {
            get => LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock)!;

            set
            {
                lock (_globalInstanceLock)
                {
                    _instance = value;
                    _globalInstanceInitialized = true;
                }
            }
        }

        public long Timeout { get; set; } = 1000;

        internal void RegisterOnNewConfigEventHandler(Action? onNewConfig)
        {
            _onNewConfigEventHander = onNewConfig;
        }

        internal Evaluation Evaluate(string key, Type resultType, object? defaultValue, IEvaluationContext context)
        {
            if (!_enabled)
            {
                Log.Debug("FeatureFlagsModule::Evaluate -> FeatureFlagsModule DISABLED");
                return new Evaluation(null, EvaluationReason.ERROR, null, "FeatureFlagsSdk is disabled");
            }

            if (_evaluator is null)
            {
                Log.Debug("FeatureFlagsModule::Evaluate -> Evaluator is null (no config received)");
                return new Evaluation(null, EvaluationReason.ERROR, null, "No config loaded");
            }

            Log.Debug("FeatureFlagsModule::Evaluate -> Returning Evaluation");
            return _evaluator.Evaluate(key, resultType, defaultValue, context);
        }

        private void UpdateConfig(List<KeyValuePair<string, ServerConfiguration>> list)
        {
            Log.Debug<int>("FeatureFlagsModule::UpdateConfig -> New config received. {Count}", list.Count);
            try
            {
                // Feed configs to the rules evaluator
                if (list.Count > 0)
                {
                    Interlocked.Exchange(ref _evaluator, new FeatureFlagsEvaluator(ReportExposure, list[0].Value, Timeout));
                    _onNewConfigEventHander?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "FeatureFlagsModule::UpdateConfig -> Error processing new config");
            }
        }

        private void ReportExposure(ExposureEvent exposure)
        {
            _exposureApi?.SendExposure(exposure);
        }
    }
}
