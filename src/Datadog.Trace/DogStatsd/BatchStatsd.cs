using System;
using System.Linq;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd
{
    internal class BatchStatsd : IBatchStatsd
    {
        private readonly IStatsdUDP _udp;
        private readonly string _prefix;
        private readonly string[] _constantTags;

        public BatchStatsd(IStatsdUDP udp, string prefix, string[] constantTags)
        {
            _udp = udp;
            _prefix = prefix;
            _constantTags = constantTags;
        }

        public Batch StartBatch(int initialCapacity = 0)
        {
            return new Batch(this, initialCapacity);
        }

        // Method is virtual for unit-tests
        public virtual string GetCommand<TCommandType, T>(string name, T value, double sampleRate = 1.0, string[] tags = null)
            where TCommandType : Statsd.Metric
        {
            return Statsd.Metric.GetCommand<TCommandType, T>(_prefix, name, value, sampleRate, _constantTags, tags);
        }

        public string GetIncrementCount(string name, int value = 1, double sampleRate = 1, string[] tags = null)
        {
            return GetCommand<Statsd.Counting, int>(name, value, sampleRate, tags);
        }

        public string GetSetGauge(string name, int value, double sampleRate = 1, string[] tags = null)
        {
            return GetCommand<Statsd.Gauge, int>(name, value, sampleRate, tags);
        }

        public string GetException(Exception exception, string source, string message, string[] tags = null)
        {
            string[] exceptionTags =
            {
                    $"source:{source}",
                    $"message:{message}",
                    $"exception-type:{exception.GetType().FullName}",
                    $"exception-message:{exception.Message}",
                };

            string[] allTags = exceptionTags.Concat(tags ?? Enumerable.Empty<string>()).ToArray();

            return GetCommand<Statsd.Counting, int>(TracerMetricNames.Health.Exceptions, value: 1, sampleRate: 1, allTags);
        }

        public string GetWarning(string source, string message, string[] tags = null)
        {
            string[] warningTags =
            {
                    $"source:{source}",
                    $"message:{message}"
                };

            string[] allTags = warningTags.Concat(tags ?? Enumerable.Empty<string>()).ToArray();

            return GetCommand<Statsd.Counting, int>(TracerMetricNames.Health.Warnings, value: 1, sampleRate: 1, allTags);
        }

        public void Send(string command)
        {
            _udp.Send(command);
        }
    }
}
