// <copyright file="CITracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Sampling;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Ci
{
    internal class CITracer : Tracer, ILockedTracer
    {
        public CITracer(TracerSettings settings)
            : base(settings, agentWriter: new CIAgentWriter(settings), sampler: new CISampler(), scopeManager: null, statsd: null)
        {
        }
    }
}
