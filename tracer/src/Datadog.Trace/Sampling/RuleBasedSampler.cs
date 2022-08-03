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

        public SamplingDecision MakeSamplingDecision(Span span)
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

                        return MakeSamplingDecision(span, sampleRate, rule.SamplingMechanism);
                    }
                }
            }

            Log.Debug("No rules matched for trace {TraceId}", traceId);
            return new SamplingDecision(SamplingPriorityValues.AutoKeep, SamplingMechanism.Default);
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

        private SamplingDecision MakeSamplingDecision(Span span, float rate, int mechanism)
        {
            // make a sampling decision as a function of traceId and sampling rate
            var sample = ((span.TraceId * KnuthFactor) % TracerConstants.MaxTraceId) <= (rate * TracerConstants.MaxTraceId);

            var priority = mechanism switch
                           {
                               // default sampling rule based on sampling rates from agent response.
                               // if sampling decision was made automatically without any input from user, use AutoKeep/AutoReject.
                               SamplingMechanism.AgentRate => sample ? SamplingPriorityValues.AutoKeep : SamplingPriorityValues.AutoReject,

                               // sampling rule based on user configuration (DD_TRACE_SAMPLE_RATE, DD_TRACE_SAMPLING_RULES).
                               // if user influenced sampling decision in any way (manually, rules, rates, etc), use UserKeep/UserReject.
                               _ => sample && _limiter.Allowed(span) ? SamplingPriorityValues.UserKeep : SamplingPriorityValues.UserReject
                           };

            return new SamplingDecision(priority, mechanism, rate);
        }
    }
}
