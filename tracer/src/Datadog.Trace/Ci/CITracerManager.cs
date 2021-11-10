// <copyright file="CITracerManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Ci
{
    internal class CITracerManager : TracerManager, ILockedTracer
    {
        public CITracerManager(ImmutableTracerSettings settings, IAgentWriter agentWriter, ISampler sampler, IScopeManager scopeManager, IDogStatsd statsd, RuntimeMetricsWriter runtimeMetricsWriter, LibLogScopeEventSubscriber libLogSubscriber, string defaultServiceName)
            : base(settings, agentWriter, sampler, scopeManager, statsd, runtimeMetricsWriter, libLogSubscriber, defaultServiceName)
        {
        }
    }
}
