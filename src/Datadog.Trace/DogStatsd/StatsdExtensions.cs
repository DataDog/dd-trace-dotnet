using System;
using System.Linq;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd
{
    internal static class StatsdExtensions
    {
        private static readonly object AddLock = new object();

        public static void AppendIncrementCount(this IStatsd statsd, string name, int value = 1, double sampleRate = 1, string[] tags = null)
        {
            lock (AddLock)
            {
                statsd?.Add<Statsd.Counting, int>(name, value, sampleRate, tags);
            }
        }

        public static void AppendSetGauge(this IStatsd statsd, string name, int value, double sampleRate = 1, string[] tags = null)
        {
            lock (AddLock)
            {
                statsd?.Add<Statsd.Gauge, int>(name, value, sampleRate, tags);
            }
        }

        public static void AppendException(this IStatsd statsd, Exception exception, string source, string message, string[] tags = null)
        {
            if (statsd != null)
            {
                string[] exceptionTags =
                {
                    $"source:{source}",
                    $"message:{message}",
                    $"exception-type:{exception.GetType().FullName}",
                    $"exception-message:{exception.Message}",
                };

                string[] allTags = exceptionTags.Concat(tags ?? Enumerable.Empty<string>()).ToArray();

                lock (AddLock)
                {
                    statsd.Add<Statsd.Counting, int>(TracerMetricNames.Health.Exceptions, value: 1, sampleRate: 1, allTags);
                }
            }
        }

        public static void AppendWarning(this IStatsd statsd, string source, string message, string[] tags = null)
        {
            if (statsd != null)
            {
                string[] warningTags =
                {
                    $"source:{source}",
                    $"message:{message}"
                };

                string[] allTags = warningTags.Concat(tags ?? Enumerable.Empty<string>()).ToArray();

                lock (AddLock)
                {
                    statsd.Add<Statsd.Counting, int>(TracerMetricNames.Health.Warnings, value: 1, sampleRate: 1, allTags);
                }
            }
        }
    }
}
