// <copyright file="GlobalSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Sampling
{
    internal class GlobalSamplingRule : ISamplingRule
    {
        private readonly float _globalRate;

        public GlobalSamplingRule(float rate)
        {
            _globalRate = rate;
        }

        /// <summary>
        /// Gets the priority which is one beneath custom rules.
        /// </summary>
        public int Priority => 0;

        public int SamplingMechanism => Datadog.Trace.Sampling.SamplingMechanism.TraceSamplingRule;

        public bool IsMatch(Span span) => true;

        public float GetSamplingRate(Span span)
        {
            span.SetMetric(Metrics.SamplingRuleDecision, _globalRate);
            return _globalRate;
        }

        public override string ToString()
        {
            return "GlobalSamplingRate";
        }
    }
}
