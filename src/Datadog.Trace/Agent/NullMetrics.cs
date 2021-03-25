using Datadog.Trace.Abstractions;

namespace Datadog.Trace.Agent
{
    internal class NullMetrics : IMetrics
    {
        public void Increment(string statName, int value = 1, double sampleRate = 1, string[] tags = null)
        {
        }
    }
}