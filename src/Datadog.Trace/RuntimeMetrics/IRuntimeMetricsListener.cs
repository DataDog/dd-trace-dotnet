using System;

namespace Datadog.Trace.RuntimeMetrics
{
    internal interface IRuntimeMetricsListener : IDisposable
    {
        void Refresh();
    }
}
