using System;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.DogStatsd
{
    internal interface IBatchStatsd
    {
        Batch StartBatch(int initialCapacity = 0);

        string GetCommand<TCommandType, T>(string name, T value, double sampleRate = 1.0, string[] tags = null)
            where TCommandType : Statsd.Metric;

        string GetIncrementCount(string name, int value = 1, double sampleRate = 1, string[] tags = null);

        string GetSetGauge(string name, int value, double sampleRate = 1, string[] tags = null);

        string GetException(Exception exception, string source, string message, string[] tags = null);

        string GetWarning(string source, string message, string[] tags = null);

        void Send(string command);
    }
}
