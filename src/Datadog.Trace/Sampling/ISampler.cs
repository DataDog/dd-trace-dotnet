using System.Collections.Generic;

namespace Datadog.Trace.Sampling
{
    internal interface ISampler
    {
        void SetSampleRates(IEnumerable<KeyValuePair<string, float>> sampleRates);

        SamplingPriority GetSamplingPriority(string service, string env, ulong traceId);
    }
}
