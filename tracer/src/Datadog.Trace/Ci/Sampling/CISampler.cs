// <copyright file="CISampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Ci.Sampling
{
    internal class CISampler : ITraceSampler
    {
        // The Ci Sampler keeps all spans, so it doesn't depend on the resource name
        public bool HasResourceBasedSamplingRule => false;

        public SamplingDecision MakeSamplingDecision(in SamplingContext context)
        {
            return new SamplingDecision(SamplingPriorityValues.UserKeep, mechanism: null, rate: null, limiterRate: null);
        }

        public void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates)
        {
        }
    }
}
