using System;

namespace Datadog.Trace.HttpOverStreams
{
    internal class DatadogHttpRequestException : Exception
    {
        public DatadogHttpRequestException(string message)
            : base(message)
        {
        }
    }
}
