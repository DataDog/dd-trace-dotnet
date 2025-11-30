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
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Rcm;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.FeatureFlags
{
    internal class FeatureFlagsModule
    {
        private static FeatureFlagsModule? _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        private readonly TracerSettings _settings;
        private readonly IRcmSubscriptionManager _rcmSubscriptionManager;
        private ISubscription? _rcmSubscription = null;
        private FfeProduct? _ffeProduct = null;

        private FeatureFlagsEvaluator? _evaluator = null;

        static FeatureFlagsModule()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureFlagsModule"/> class with default settings.
        /// </summary>
        public FeatureFlagsModule(IRcmSubscriptionManager? rcmSubscriptionManager = null)
            : this(TracerSettings.FromDefaultSourcesInternal(), rcmSubscriptionManager)
        {
        }

        internal FeatureFlagsModule(TracerSettings settings, IRcmSubscriptionManager? rcmSubscriptionManager = null)
        {
            _settings = settings;
            _rcmSubscriptionManager = rcmSubscriptionManager ?? RcmSubscriptionManager.Instance;

            if (settings.IsFlaggingProviderEnabled)
            {
                _ffeProduct = new FfeProduct(UpdateConfig);
                _rcmSubscription = new Subscription(_ffeProduct.UpdateFromRcm, RcmProducts.FfeFlags);
                _rcmSubscriptionManager.SubscribeToChanges(_rcmSubscription);
                _rcmSubscriptionManager.SetCapability(RcmCapabilitiesIndices.AsmActivation, true);
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

        private void ApplyServerConfigs()
        {
        }

        private void UpdateConfig(List<KeyValuePair<string, ServerConfiguration>> list)
        {
            // Feed configs to the rules evaluator
            if (list.Count > 0)
            {
                _evaluator = new FeatureFlagsEvaluator(ReportExposure, list[0].Value, Timeout);
            }
        }

        private void ReportExposure(ExposureEvent exposure)
        {
        }
    }
}
