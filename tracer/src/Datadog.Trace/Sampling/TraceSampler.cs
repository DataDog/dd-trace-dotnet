// <copyright file="TraceSampler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Sampling
{
    internal sealed class TraceSampler : ITraceSampler
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TraceSampler>();

        private readonly IRateLimiter _limiter;
        private readonly List<ISamplingRule> _rules;
        private readonly AgentSamplingRule? _agentSamplingRule;

        public TraceSampler(IRateLimiter limiter, List<ISamplingRule> rules, AgentSamplingRule? agentSamplingRule)
        {
            _limiter = limiter;
            _rules = [..rules];
            _agentSamplingRule = agentSamplingRule;
            HasResourceBasedSamplingRule = false;
            foreach (var rule in rules)
            {
                if (rule.IsResourceBasedSamplingRule)
                {
                    HasResourceBasedSamplingRule = true;
                    break;
                }
            }
        }

        public bool HasResourceBasedSamplingRule { get; }

        public void SetDefaultSampleRates(IReadOnlyDictionary<string, float> sampleRates)
        {
            _agentSamplingRule?.SetDefaultSampleRates(sampleRates);
        }

        public SamplingDecision MakeSamplingDecision(Span span)
        {
            foreach (var rule in _rules)
            {
                if (rule.IsMatch(span))
                {
                    var sampleRate = rule.GetSamplingRate(span);
                    return MakeSamplingDecision(span, sampleRate, rule.SamplingMechanism);
                }
            }

            // this code is normally unreachable because there should always be a AgentSamplingRule
            // (even before we receive rates from the agent)
            Log.Debug("No sampling rules matched for trace {TraceId}. Using default sampling decision.", span.Context.RawTraceId);
            return SamplingDecision.Default;
        }

        // used for testing
        internal IReadOnlyList<ISamplingRule> GetRules()
        {
            return _rules;
        }

        private SamplingDecision MakeSamplingDecision(Span span, float rate, string mechanism)
        {
            // make a sampling decision as a function of traceId and sampling rate.
            var sample = SamplingHelpers.SampleByRate(span.TraceId128, rate);
            int priority;
            float? limiterRate = null;

            if (mechanism is SamplingMechanism.AgentRate or SamplingMechanism.Default)
            {
                // default sampling rule based on sampling rates from agent response or from a cold start.
                // if sampling decision was made automatically without any input from user, use AutoKeep/AutoReject.
                priority = sample ? SamplingPriorityValues.AutoKeep : SamplingPriorityValues.AutoReject;
            }
            else
            {
                // sampling rule based on user configuration (DD_TRACE_SAMPLE_RATE, DD_TRACE_SAMPLING_RULES).
                // if user influenced sampling decision in any way (manually, rules, rates, etc), use UserKeep/UserReject.
                if (sample)
                {
                    priority = _limiter.Allowed(span) ? SamplingPriorityValues.UserKeep : SamplingPriorityValues.UserReject;

                    // report the rate limiter's effective rate if the rate limiter is used
                    limiterRate = _limiter.GetEffectiveRate();
                }
                else
                {
                    priority = SamplingPriorityValues.UserReject;
                }
            }

            return new SamplingDecision(priority, mechanism, rate, limiterRate);
        }

        public sealed class Builder(IRateLimiter limiter)
        {
            private readonly IRateLimiter _limiter = limiter;
            private readonly List<ISamplingRule> _rules = [];
            private AgentSamplingRule? _agentSamplingRule;

            public TraceSampler Build()
            {
                return new TraceSampler(_limiter, _rules, _agentSamplingRule);
            }

            /// <summary>
            /// Register a new sampling rule. To register a <see cref="AgentSamplingRule"/>,
            /// use <see cref="RegisterAgentSamplingRule"/> instead.
            /// </summary>
            /// <remarks>
            /// The order that rules are registered is important, as they are evaluated in order.
            /// The first rule that matches will be used to determine the sampling rate.
            /// </remarks>
            public void RegisterRule(ISamplingRule rule)
            {
                _rules.Add(rule);
            }

            /// <summary>
            /// Register new sampling rules. To register a <see cref="AgentSamplingRule"/>,
            /// use <see cref="RegisterAgentSamplingRule"/> instead.
            /// </summary>
            /// <remarks>
            /// The order that rules are registered is important, as they are evaluated in order.
            /// The first rule that matches will be used to determine the sampling rate.
            /// </remarks>
            public void RegisterRules(IEnumerable<ISamplingRule> rules)
            {
                _rules.AddRange(rules);
            }

            /// <summary>
            /// Register a new agent sampling rule. This rule should be registered last,
            /// after any calls to <see cref="RegisterRule"/> or <see cref="RegisterRules"/>.
            /// </summary>
            /// <remarks>
            /// The order that rules are registered is important, as they are evaluated in order.
            /// The first rule that matches will be used to determine the sampling rate.
            /// </remarks>
            public void RegisterAgentSamplingRule(AgentSamplingRule rule)
            {
                // only register the one AgentSamplingRule
                // keep a reference to this rule so we can call SetDefaultSampleRates() later
                // to update the agent sampling rates
                if (Interlocked.Exchange(ref _agentSamplingRule, rule) == null)
                {
                    RegisterRule(rule);
                }
                else
                {
                    Log.Warning("AgentSamplingRule already registered. Ignoring additional registration.");
                }
            }
        }
    }
}
