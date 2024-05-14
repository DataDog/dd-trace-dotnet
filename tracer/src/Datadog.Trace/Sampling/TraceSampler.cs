// <copyright file="TraceSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Sampling
{
    internal class TraceSampler : ITraceSampler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceSampler>();

        private readonly IRateLimiter _limiter;
        private readonly AgentSamplingRule _defaultRule = new();
        private readonly List<ISamplingRule> _rules = [];

        public TraceSampler(IRateLimiter limiter)
        {
            _limiter = limiter;
            RegisterRule(_defaultRule);
        }

        public void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates)
        {
            _defaultRule.SetDefaultSampleRates(sampleRates);
        }

        public SamplingDecision MakeSamplingDecision(Span span)
        {
            foreach (var rule in _rules)
            {
                if (rule.IsMatch(span))
                {
                    // Note: GetSamplingRate() can adds tags like "_dd.agent_psr" or "_dd.rule_psr"
                    var sampleRate = rule.GetSamplingRate(span);
                    return MakeSamplingDecision(span, sampleRate, rule.SamplingMechanism);
                }
            }

            // this code is normally unreachable because there should always be a AgentSamplingRule
            // (even before we receive rates from the agent)
            Log.Debug("No sampling rules matched for trace {TraceId}. Using default sampling decision.", span.Context.RawTraceId);
            return SamplingDecision.Default;
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
            // make a sampling decision as a function of traceId and sampling rate.
            var sample = SamplingHelpers.SampleByRate(span.TraceId128, rate);

            var priority = mechanism switch
                           {
                               // default sampling rule based on sampling rates from agent response or from a cold start.
                               // if sampling decision was made automatically without any input from user, use AutoKeep/AutoReject.
                               SamplingMechanism.AgentRate or SamplingMechanism.Default => sample ? SamplingPriorityValues.AutoKeep : SamplingPriorityValues.AutoReject,

                               // sampling rule based on user configuration (DD_TRACE_SAMPLE_RATE, DD_TRACE_SAMPLING_RULES).
                               // if user influenced sampling decision in any way (manually, rules, rates, etc), use UserKeep/UserReject.
                               _ => sample && _limiter.Allowed(span) ? SamplingPriorityValues.UserKeep : SamplingPriorityValues.UserReject
                           };

            return new SamplingDecision(priority, mechanism);
        }
    }
}
