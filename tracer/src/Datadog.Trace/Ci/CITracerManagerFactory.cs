// <copyright file="CITracerManagerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Sampling;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Ci
{
    internal class CITracerManagerFactory : TracerManagerFactory
    {
        private readonly CIVisibilitySettings _settings;
        private readonly IDiscoveryService _discoveryService;
        private readonly bool _enabledEventPlatformProxy;
        private readonly bool _useLockedManager;

        public CITracerManagerFactory(CIVisibilitySettings settings, IDiscoveryService discoveryService, bool enabledEventPlatformProxy = false, bool useLockedManager = true)
        {
            _settings = settings;
            _discoveryService = discoveryService;
            _enabledEventPlatformProxy = enabledEventPlatformProxy;
            _useLockedManager = useLockedManager;
        }

        protected override TracerManager CreateTracerManagerFrom(
            ImmutableTracerSettings settings,
            IAgentWriter agentWriter,
            IScopeManager scopeManager,
            IDogStatsd statsd,
            RuntimeMetricsWriter runtimeMetrics,
            DirectLogSubmissionManager logSubmissionManager,
            ITelemetryController telemetry,
            IDiscoveryService discoveryService,
            DataStreamsManager dataStreamsManager,
            string defaultServiceName,
            IGitMetadataTagsProvider gitMetadataTagsProvider,
            ITraceSampler traceSampler,
            ISpanSampler spanSampler,
            IRemoteConfigurationManager remoteConfigurationManager,
            IDynamicConfigurationManager dynamicConfigurationManager,
            ITracerFlareManager tracerFlareManager)
        {
            if (_useLockedManager)
            {
                return new CITracerManager.LockedManager(settings, agentWriter, scopeManager, statsd, runtimeMetrics, logSubmissionManager, telemetry, discoveryService, dataStreamsManager, defaultServiceName, gitMetadataTagsProvider, traceSampler, spanSampler, remoteConfigurationManager, dynamicConfigurationManager, tracerFlareManager);
            }
            else
            {
                return new CITracerManager(settings, agentWriter, scopeManager, statsd, runtimeMetrics, logSubmissionManager, telemetry, discoveryService, dataStreamsManager, defaultServiceName, gitMetadataTagsProvider, traceSampler, spanSampler, remoteConfigurationManager, dynamicConfigurationManager, tracerFlareManager);
            }
        }

        protected override ITelemetryController CreateTelemetryController(ImmutableTracerSettings settings, IDiscoveryService discoveryService)
        {
            return TelemetryFactory.Instance.CreateCiVisibilityTelemetryController(settings, discoveryService, isAgentAvailable: !_settings.Agentless);
        }

        protected override IGitMetadataTagsProvider GetGitMetadataTagsProvider(ImmutableTracerSettings settings, IScopeManager scopeManager, ITelemetryController telemetry)
        {
            return new CIGitMetadataTagsProvider(telemetry);
        }

        protected override ITraceSampler GetSampler(ImmutableTracerSettings settings)
        {
            return new CISampler();
        }

        protected override bool ShouldEnableRemoteConfiguration(ImmutableTracerSettings settings) => false;

        protected override IAgentWriter GetAgentWriter(ImmutableTracerSettings settings, IDogStatsd statsd, Action<Dictionary<string, float>> updateSampleRates, IDiscoveryService discoveryService)
        {
            // Check for agentless scenario
            if (_settings.Agentless)
            {
                if (!string.IsNullOrEmpty(_settings.ApiKey))
                {
                    return new CIVisibilityProtocolWriter(_settings, new CIWriterHttpSender(CIVisibility.GetRequestFactory(settings)));
                }

                Environment.FailFast("An API key is required in Agentless mode.");
                return null;
            }

            // With agent scenario:
            if (_enabledEventPlatformProxy)
            {
                return new CIVisibilityProtocolWriter(_settings, new CIWriterHttpSender(CIVisibility.GetRequestFactory(settings)));
            }

            // Event platform proxy not found
            // Set the tracer buffer size to the max
            var traceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.
            return new ApmAgentWriter(settings, updateSampleRates, discoveryService, traceBufferSize);
        }

        protected override IDiscoveryService GetDiscoveryService(ImmutableTracerSettings settings)
            => _discoveryService;
    }
}
