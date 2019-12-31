using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class DefaultSamplingRule : ISamplingRule
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<DefaultSamplingRule>();

        private static Dictionary<string, float> _sampleRates = new Dictionary<string, float>();

        public string RuleName => "default-rule";

        /// <summary>
        /// Gets the lowest possible priority
        /// </summary>
        public int Priority => int.MinValue;

        public bool IsMatch(Span span)
        {
            return true;
        }

        public float GetSamplingRate(Span span)
        {
            Log.Debug("Using the default sampling logic");

            var env = span.GetTag(Tags.Env);
            var service = span.ServiceName;

            var key = $"service:{service},env:{env}";

            if (_sampleRates.TryGetValue(key, out var sampleRate))
            {
                span.SetMetric(Metrics.SamplingAgentDecision, sampleRate);
                return sampleRate;
            }

            Log.Debug("Could not establish sample rate for trace {0}", span.TraceId);

            return 1;
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

            _sampleRates = rates;
        }
    }
}
