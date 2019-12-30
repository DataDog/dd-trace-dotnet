using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class RuleBasedSampler : ISampler
    {
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<RuleBasedSampler>();
        private static bool _tracingWithoutLimitsEnabled = false;

        private readonly IRateLimiter _limiter;
        private readonly DefaultSamplingRule _defaultRule = new DefaultSamplingRule();
        private readonly List<ISamplingRule> _rules = new List<ISamplingRule>();

        public RuleBasedSampler(IRateLimiter limiter)
        {
            _limiter = limiter ?? new RateLimiter(null);
            RegisterRule(_defaultRule);
        }

        public static void OptInTracingWithoutLimits()
        {
            _tracingWithoutLimitsEnabled = true;
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
                            "Matched on rule {0}. Applying rate of {1} to trace id {2}",
                            rule.RuleName,
                            sampleRate,
                            traceId);

                        return GetSamplingPriority(span, sampleRate);
                    }
                }
            }

            Log.Debug("No rules matched for trace {0}", traceId);

            return SamplingPriority.AutoKeep;
        }

        /// <summary>
        /// Will insert a rule according to how high the Priority field is set.
        /// If the priority is equal to other rules, the new rule will be the last in that priority group.
        /// </summary>
        /// <param name="rule">The new rule being registered.</param>
        public void RegisterRule(ISamplingRule rule)
        {
            OptInTracingWithoutLimits();

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

        private SamplingPriority GetSamplingPriority(Span span, float rate)
        {
            var sample = ((span.TraceId * KnuthFactor) % TracerConstants.MaxTraceId) <= (rate * TracerConstants.MaxTraceId);
            var priority = SamplingPriority.AutoReject;

            if (sample)
            {
                if (_tracingWithoutLimitsEnabled)
                {
                    if (_limiter.Allowed(span))
                    {
                        priority = SamplingPriority.AutoKeep;
                    }
                }
                else
                {
                    priority = SamplingPriority.AutoKeep;
                }
            }

            return priority;
        }
    }
}
