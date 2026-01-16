// <copyright file="TestOptimizationTracerManagerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Sampling;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Ci
{
    internal sealed class TestOptimizationTracerManagerFactory : TracerManagerFactory
    {
        private readonly TestOptimizationSettings _settings;
        private readonly bool _enabledEventPlatformProxy;
        private readonly ITestOptimizationTracerManagement _testOptimizationTracerManagement;

        public TestOptimizationTracerManagerFactory(TestOptimizationSettings settings, ITestOptimizationTracerManagement testOptimizationTracerManagement, bool enabledEventPlatformProxy = false, bool useLockedManager = true)
        {
            _settings = settings;
            _enabledEventPlatformProxy = enabledEventPlatformProxy;
            _testOptimizationTracerManagement = testOptimizationTracerManagement;
        }

        protected override TracerManager CreateTracerManagerFrom(
            TracerSettings settings,
            IAgentWriter agentWriter,
            IScopeManager scopeManager,
            IStatsdManager statsd,
            RuntimeMetricsWriter runtimeMetrics,
            DirectLogSubmissionManager logSubmissionManager,
            ITelemetryController telemetry,
            IDiscoveryService discoveryService,
            DataStreamsManager dataStreamsManager,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ITraceSampler traceSampler,
            ISpanSampler spanSampler,
            IRemoteConfigurationManager remoteConfigurationManager,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager,
            ISpanEventsManager spanEventsManager,
            FeatureFlagsModule featureFlags)
        {
            telemetry.RecordTestOptimizationSettings(_settings);
            if (_testOptimizationTracerManagement.UseLockedTracerManager)
            {
                return new TestOptimizationTracerManager.LockedManager(settings, agentWriter, scopeManager, statsd, runtimeMetrics, logSubmissionManager, telemetry, discoveryService, dataStreamsManager, gitMetadataTagsProvider, traceSampler, spanSampler, remoteConfigurationManager, dynamicConfigurationManager, tracerFlareManager, spanEventsManager, featureFlags);
            }

            return new TestOptimizationTracerManager(settings, agentWriter, scopeManager, statsd, runtimeMetrics, logSubmissionManager, telemetry, discoveryService, dataStreamsManager, gitMetadataTagsProvider, traceSampler, spanSampler, remoteConfigurationManager, dynamicConfigurationManager, tracerFlareManager, spanEventsManager, featureFlags);
        }

        protected override TelemetrySettings CreateTelemetrySettings(TracerSettings settings)
        {
            var isAgentAvailable = !_settings.Agentless;
            return TelemetrySettings.FromSource(GlobalConfigurationSource.Instance, TelemetryFactory.Config, settings, isAgentAvailable);
        }

        protected override ITelemetryController CreateTelemetryController(TracerSettings settings, IDiscoveryService discoveryService, TelemetrySettings telemetrySettings)
        {
            return TelemetryFactory.Instance.CreateCiVisibilityTelemetryController(settings, telemetrySettings: telemetrySettings, discoveryService);
        }

        protected override IGitMetadataTagsProvider GetGitMetadataTagsProvider(TracerSettings settings, MutableSettings initialMutableSettings, IScopeManager scopeManager, ITelemetryController telemetry)
        {
            return new CIGitMetadataTagsProvider(telemetry);
        }

        protected override ITraceSampler GetSampler(TracerSettings settings)
        {
            return new CISampler();
        }

        protected override bool ShouldEnableRemoteConfiguration(TracerSettings settings) => false;

        protected override IAgentWriter GetAgentWriter(TracerSettings settings, IStatsdManager statsd, Action<Dictionary<string, float>> updateSampleRates, Action<string> updateConfigHash, IDiscoveryService discoveryService, TelemetrySettings telemetrySettings)
        {
            // Check for agentless scenario
            if (_settings.Agentless)
            {
                if (!string.IsNullOrEmpty(_settings.ApiKey))
                {
                    return new CIVisibilityProtocolWriter(_settings, new CIWriterHttpSender(_testOptimizationTracerManagement.GetRequestFactory(settings)));
                }

                Environment.FailFast("An API key is required in Agentless mode.");
                return null!;
            }

            // With agent scenario:
            if (_enabledEventPlatformProxy)
            {
                return new CIVisibilityProtocolWriter(_settings, new CIWriterHttpSender(_testOptimizationTracerManagement.GetRequestFactory(settings)));
            }

            // Event platform proxy not found
            // Set the tracer buffer size to the max
            var traceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.
            return new ApmAgentWriter(settings, updateSampleRates, updateConfigHash, discoveryService, traceBufferSize);
        }

        internal override IDiscoveryService GetDiscoveryService(TracerSettings settings)
            => _testOptimizationTracerManagement.DiscoveryService;
    }
}
