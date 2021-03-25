using Datadog.Trace.Abstractions;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent
{
    internal class DogStatsdMetrics : IMetrics
    {
        private readonly IDogStatsd _dogStatsd;

        public DogStatsdMetrics(IDogStatsd dogStatsd)
        {
            _dogStatsd = dogStatsd;
        }

        public void Increment(string statName, int value = 1, double sampleRate = 1, string[] tags = null)
        {
            _dogStatsd.Increment(statName, value, sampleRate, tags);
        }
    }
}