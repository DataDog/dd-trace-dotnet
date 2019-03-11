using System.Collections.Generic;

namespace Datadog.Trace.Sampling
{
    internal class SimpleSampler : ISampler
    {
        public SimpleSampler(SamplingPriority samplingPriority)
        {
            SamplingPriority = samplingPriority;
        }

        public SamplingPriority SamplingPriority { get; }

        public void SetSampleRates(IEnumerable<KeyValuePair<string, float>> sampleRates)
        {
        }

        public SamplingPriority GetSamplingPriority(string service, string env, ulong traceId)
        {
            return SamplingPriority;
        }
    }
}
