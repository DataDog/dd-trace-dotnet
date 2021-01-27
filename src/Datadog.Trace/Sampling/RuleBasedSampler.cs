using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class RuleBasedSampler : ISampler
    {
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<RuleBasedSampler>();

        private readonly IRateLimiter _limiter;
        private readonly DefaultSamplingRule _defaultRule = new DefaultSamplingRule();
        private readonly List<ISamplingRule> _rules = new List<ISamplingRule>();

        public RuleBasedSampler(IRateLimiter limiter)
        {
            _limiter = limiter ?? new RateLimiter(null);
            RegisterRule(_defaultRule);
        }

        public void SetDefaultSampleRates(IEnumerable<KeyValuePair<string, float>> sampleRates)
        {
            _defaultRule.SetDefaultSampleRates(sampleRates);
        }

        public SamplingPriority GetSamplingPriority(Span span)
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

            return SamplingPriority.AutoKeep;
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

        private SamplingPriority GetSamplingPriority(Span span, float rate, bool agentSampling)
        {
            var sample = ((span.TraceId * KnuthFactor) % TracerConstants.MaxTraceId) <= (rate * TracerConstants.MaxTraceId);
            var priority = SamplingPriority.AutoReject;

            if (sample && (agentSampling || _limiter.Allowed(span)))
            {
                priority = SamplingPriority.AutoKeep;
            }

            return priority;
        }
    }
}
