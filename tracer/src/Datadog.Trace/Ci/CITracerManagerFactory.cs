// <copyright file="CITracerManagerFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Sampling;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Ci
{
    internal class CITracerManagerFactory : TracerManagerFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CITracerManagerFactory>();
        private CIVisibilitySettings _settings;

        public CITracerManagerFactory(CIVisibilitySettings settings)
        {
            _settings = settings;
        }

        protected override TracerManager CreateTracerManagerFrom(
            ImmutableTracerSettings settings,
            IAgentWriter agentWriter,
            ISampler sampler,
            IScopeManager scopeManager,
            IDogStatsd statsd,
            RuntimeMetricsWriter runtimeMetrics,
            DirectLogSubmissionManager logSubmissionManager,
            string defaultServiceName)
        {
            return new CITracerManager(settings, agentWriter, sampler, scopeManager, statsd, runtimeMetrics, logSubmissionManager, defaultServiceName);
        }

        protected override ISampler GetSampler(ImmutableTracerSettings settings)
        {
            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                return null;
            }

            return new CISampler();
        }

        protected override IAgentWriter GetAgentWriter(ImmutableTracerSettings settings, IDogStatsd statsd, ISampler sampler)
        {
            // Check for agentless scenario
            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                return new CIAgentlessWriter(settings, sampler, new CIWriterHttpSender(GetRequestFactory()));
            }
            else
            {
                // With agent scenario:

                // Set the tracer buffer size to the max
                var traceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.
                return new CIAgentWriter(settings, sampler, traceBufferSize);
            }
        }

        private IApiRequestFactory GetRequestFactory()
        {
#if NETCOREAPP
            Log.Information("Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
            return new HttpClientRequestFactory();
#else
            Log.Information("Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
            return new ApiWebRequestFactory();
#endif
        }
    }
}
