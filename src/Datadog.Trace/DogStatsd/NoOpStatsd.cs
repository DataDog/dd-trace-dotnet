using System;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd
{
    internal class NoOpStatsd : IBatchStatsd
    {
        public Batch StartBatch(int initialCapacity = 0) => default;

        public string GetCommand<TCommandType, T>(string name, T value, double sampleRate = 1, string[] tags = null)
            where TCommandType : Statsd.Metric
        {
            return string.Empty;
        }

        public string GetIncrementCount(string name, int value = 1, double sampleRate = 1, string[] tags = null)
        {
            return string.Empty;
        }

        public string GetSetGauge(string name, int value, double sampleRate = 1, string[] tags = null)
        {
            return string.Empty;
        }

        public string GetException(Exception exception, string source, string message, string[] tags = null)
        {
            return string.Empty;
        }

        public string GetWarning(string source, string message, string[] tags = null)
        {
            return string.Empty;
        }

        public void Send(string command)
        {
        }
    }
}
