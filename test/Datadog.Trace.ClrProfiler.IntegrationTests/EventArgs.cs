using System;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class EventArgs<T> : EventArgs
    {
        public EventArgs(T data)
        {
            Data = data;
        }

        public T Data { get; }
    }
}
