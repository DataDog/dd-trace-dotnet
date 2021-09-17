// <copyright file="CITracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Ci
{
    internal class CITracer : Tracer, ILockedTracer
    {
        public CITracer(TracerSettings settings)
            : base(settings, agentWriter: null, sampler: new CISampler(), scopeManager: null, statsd: null)
        {
        }

        private class CISampler : ISampler
        {
            public SamplingPriority GetSamplingPriority(Span span)
            {
                return SamplingPriority.UserKeep;
            }

            public void RegisterRule(ISamplingRule rule)
            {
            }

            public void SetDefaultSampleRates(IEnumerable<KeyValuePair<string, float>> sampleRates)
            {
            }
        }
    }
}
