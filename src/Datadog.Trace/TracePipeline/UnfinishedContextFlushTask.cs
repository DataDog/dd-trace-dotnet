using System;

namespace Datadog.Trace
{
    internal class UnfinishedContextFlushTask
    {
        public DatadogSpanContext UnfinishedContext { get; set; }

        public DateTime FlushAt { get; set; }
    }
}
