// <copyright file="TracerHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.StatsdClient;
using Moq;

namespace Datadog.Trace.TestHelpers
{
    internal static class TracerHelper
    {
        /// <summary>
        /// Create a test instance of the Tracer, that doesn't used any shared instances
        /// </summary>
        public static ScopedTracer Create(
            TracerSettingsInternal settings = null,
            IAgentWriter agentWriter = null,
            ITraceSampler sampler = null,
            IScopeManager scopeManager = null,
            IDogStatsd statsd = null)
        {
            return new ScopedTracer(settings, agentWriter, sampler, scopeManager, statsd);
        }

        /// <summary>
        /// Create a test instance of the Tracer, that doesn't used any shared instances
        /// </summary>
        public static ScopedTracer CreateWithFakeAgent(
            TracerSettingsInternal settings = null)
        {
            return new ScopedTracer(settings, Mock.Of<IAgentWriter>());
        }

        public class ScopedTracer : TracerInternal, IAsyncDisposable
        {
            public ScopedTracer(TracerSettingsInternal settings = null, IAgentWriter agentWriter = null, ITraceSampler sampler = null, IScopeManager scopeManager = null, IDogStatsd statsd = null)
                : base(settings, agentWriter, sampler, scopeManager, statsd)
            {
            }

            public async ValueTask DisposeAsync()
            {
                await TracerManager.ShutdownAsync();
            }
        }
    }
}
