// <copyright file="GlobalSamplingRateRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Sampling
{
    internal sealed class GlobalSamplingRateRule : ISamplingRule
    {
        private readonly float _globalRate;

        public GlobalSamplingRateRule(float rate)
        {
            _globalRate = rate;
        }

        public string SamplingMechanism => Datadog.Trace.Sampling.SamplingMechanism.LocalTraceSamplingRule;

        // Doesn't depend on span at all
        public bool IsResourceBasedSamplingRule => false;

        public bool IsMatch(Span span) => true;

        public float GetSamplingRate(Span span)
        {
            return _globalRate;
        }

        public override string ToString()
        {
            return "GlobalSamplingRate";
        }
    }
}
