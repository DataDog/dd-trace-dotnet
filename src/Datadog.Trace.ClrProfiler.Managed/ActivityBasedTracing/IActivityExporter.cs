using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler
{
    internal interface IActivityExporter : IDisposable
    {
        bool IsSendTracesSupported { get; }

        bool IsSendActivitiesSupported { get; }

        void SendTraces(IReadOnlyCollection<TraceActivitiesContainer> traces);

        void SendActivities(IReadOnlyCollection<Activity> traces);
    }
}
