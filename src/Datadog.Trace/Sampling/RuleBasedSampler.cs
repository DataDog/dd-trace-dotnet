using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class RuleBasedSampler : ISampler
    {
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<RuleBasedSampler>();

        private readonly IRateLimiter _limiter;
        private readonly List<ISamplingRule> _rules = new List<ISamplingRule>();

        private readonly object _sampleRateGate = new object();

        private Dictionary<string, float> _sampleRates = new Dictionary<string, float>();

        public RuleBasedSampler(IRateLimiter limiter)
        {
            _limiter = limiter ?? new RateLimiter(null);
        }

        public void SetDefaultSampleRates(IEnumerable<KeyValuePair<string, float>> sampleRates)
        {
            // to avoid locking if writers and readers can access the dictionary at the same time,
            // build the new dictionary first, then replace the old one
            var rates = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            if (sampleRates != null)
            {
                foreach (var pair in sampleRates)
                {
                    rates.Add(pair.Key, pair.Value);
                }
            }

            lock (_sampleRateGate)
            {
                _sampleRates = rates;
            }
        }

        public SamplingPriority GetSamplingPriority(Span span)
        {
            float sampleRate;
            var traceId = span.TraceId;

            if (_rules.Count > 0)
            {
                foreach (var rule in _rules)
                {
                    if (rule.IsMatch(span))
                    {
                        sampleRate = rule.GetSamplingRate(span);
                        Log.Debug(
                            "Matched on rule {0}. Applying rate of {1} to trace id {2}",
                            rule.Name,
                            sampleRate,
                            traceId);
                        span.SetMetric(Metrics.SamplingRuleDecision, sampleRate);
                        return GetSamplingPriority(span, sampleRate, withRateLimiter: true);
                    }
                }
            }

            var env = span.GetTag(Tags.Env);
            var service = span.ServiceName;

            var key = $"service:{service},env:{env}";

            bool retrievedRate;
            lock (_sampleRateGate)
            {
                retrievedRate = _sampleRates.TryGetValue(key, out sampleRate);
            }

            if (retrievedRate)
            {
                Log.Debug("Using the default sampling logic for trace {0}", traceId);
                return GetSamplingPriority(span, sampleRate, withRateLimiter: false);
            }

            Log.Debug("Could not establish sample rate for trace {0}", traceId);
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

        private SamplingPriority GetSamplingPriority(Span span, float rate, bool withRateLimiter)
        {
            var sample = ((span.TraceId * KnuthFactor) % TracerConstants.MaxTraceId) <= (rate * TracerConstants.MaxTraceId);
            var priority = SamplingPriority.AutoReject;

            if (sample)
            {
                // Ensure all allowed traces adhere to the global rate limit
                if (withRateLimiter && _limiter.Allowed(span.TraceId))
                {
                    // Always set the sample rate metric whether it was allowed or not
                    // DEV: Setting this allows us to properly compute metrics and debug the
                    //      various sample rates that are getting applied to this span
                    span.SetMetric(Metrics.SamplingLimitDecision, _limiter.GetEffectiveRate());
                    priority = SamplingPriority.AutoKeep;
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
