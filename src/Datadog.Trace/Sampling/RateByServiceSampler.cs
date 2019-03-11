using System;
using System.Collections.Generic;

namespace Datadog.Trace.Sampling
{
    internal class RateByServiceSampler : ISampler
    {
        private const ulong MaxTraceId = 9_223_372_036_854_775_807; // 2^63-1
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        // can support multiple readers concurrently, as long as the collection is not modified.
        // start with an empty collection by default, so we can skip the null check in GetSamplingPriority()
        private Dictionary<string, float> _sampleRates = new Dictionary<string, float>();

        public void SetSampleRates(IEnumerable<KeyValuePair<string, float>> sampleRates)
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

        public SamplingPriority GetSamplingPriority(string service, string env, ulong traceId)
        {
            string key = $"service:{service},env:{env}";

            if (_sampleRates.TryGetValue(key, out float sampleRate))
            {
                var sample = ((traceId * KnuthFactor) % MaxTraceId) <= (sampleRate * MaxTraceId);

                return sample
                           ? SamplingPriority.AutoKeep
                           : SamplingPriority.AutoReject;
            }

            return SamplingPriority.AutoKeep;
        }
    }
}
