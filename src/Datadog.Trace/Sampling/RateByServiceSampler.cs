using System.Collections.Generic;

namespace Datadog.Trace.Sampling
{
    internal class RateByServiceSampler : ISampler
    {
        private const ulong MaxTraceId = 9_223_372_036_854_775_807; // 2^63-1
        private const ulong KnuthFactor = 1_111_111_111_111_111_111;

        private readonly IDictionary<string, float> _sampleRates = new Dictionary<string, float>();

        public void SetSampleRates(IEnumerable<KeyValuePair<string, float>> sampleRates)
        {
            _sampleRates.Clear();

            foreach (var pair in sampleRates)
            {
                _sampleRates.Add(pair.Key, pair.Value);
            }
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
