using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd
{
    internal static class StatsdExtensions
    {
        public static IStatsd AppendIncrementCount(this IStatsd statsd, string name, int value = 1, double sampleRate = 1, string[] tags = null)
        {
            statsd.Add<Statsd.Counting, int>(name, value, sampleRate, tags);
            return statsd;
        }

        public static IStatsd AppendSetGauge(this IStatsd statsd, string name, int value, double sampleRate = 1, string[] tags = null)
        {
            statsd.Add<Statsd.Gauge, int>(name, value, sampleRate, tags);
            return statsd;
        }
    }
}
