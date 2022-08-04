// <copyright file="CISampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.Ci.Sampling
{
    internal class CISampler : ISampler
    {
        public SamplingDecision MakeSamplingDecision(Span span)
        {
            return new SamplingDecision(SamplingPriorityValues.UserKeep);
        }

        public void RegisterRule(ISamplingRule rule)
        {
        }

        public void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates)
        {
        }
    }
}
