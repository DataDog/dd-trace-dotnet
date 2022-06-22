// <copyright file="RuleBasedSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class RuleBasedSampler : ISampler
    {
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RuleBasedSampler>();

        private readonly IRateLimiter _limiter;
        private readonly DefaultSamplingRule _defaultRule = new DefaultSamplingRule();
        private readonly List<ISamplingRule> _rules = new List<ISamplingRule>();

        public RuleBasedSampler(IRateLimiter limiter)
        {
            _limiter = limiter ?? new TracerRateLimiter(null);
            RegisterRule(_defaultRule);
        }

        public void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates)
        {
            _defaultRule.SetDefaultSampleRates(sampleRates);
        }

        public int GetSamplingPriority(Span span)
        {
            var traceId = span.TraceId;

            if (_rules.Count > 0)
            {
                foreach (var rule in _rules)
                {
                    if (rule.IsMatch(span))
                    {
                        var sampleRate = rule.GetSamplingRate(span);

                        Log.Debug(
                            "Matched on rule {RuleName}. Applying rate of {Rate} to trace id {TraceId}",
                            rule.RuleName,
                            sampleRate,
                            traceId);

                        return GetSamplingPriority(span, sampleRate, agentSampling: rule is DefaultSamplingRule);
                    }
                }
            }

            Log.Debug("No rules matched for trace {TraceId}", traceId);

            return SamplingPriorityValues.AutoKeep;
        }

        /// <summary>
        /// Will insert a rule according to how high the Priority field is set.
        /// If the priority is equal to other rules, the new rule will be the last in that priority group.
        /// </summary>
        /// <param name="rule">The new rule being registered.</param>
        public void RegisterRule(ISamplingRule rule)
        {
            for (var i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].Priority < rule.Priority)
                {
                    _rules.Insert(i, rule);
                    return;
                }
            }

            // No items or this is the last priority
            _rules.Add(rule);
        }

        private int GetSamplingPriority(Span span, float rate, bool agentSampling)
        {
            // make a sampling decision as a function of traceId and sampling rate
            var sample = ((span.TraceId * KnuthFactor) % TracerConstants.MaxTraceId) <= (rate * TracerConstants.MaxTraceId);

            // legacy sampling based on data from agent
            if (agentSampling)
            {
                return sample ? SamplingPriorityValues.AutoKeep : SamplingPriorityValues.AutoReject;
            }

            // rules-based sampling + rate limiter
            // NOTE: all tracers are changing this from AutoKeep/AutoReject to UserKeep/UserReject
            // to prevent the agent from overriding user configuration
            return sample && _limiter.Allowed(span) ? SamplingPriorityValues.UserKeep : SamplingPriorityValues.UserReject;
        }
    }
}
