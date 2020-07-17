using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.Trace.ClrProfiler
{
    internal class DatadogAgentMessagePackActivityExporter : IActivityExporter
    {
        internal static readonly Func<ActivityCollectorConfiguration, IActivityExporter> Factory = (config) => new DatadogAgentMessagePackActivityExporter();

        public bool IsSendTracesSupported
        {
            get { return false; }
        }

        public bool IsSendActivitiesSupported
        {
            get { return false; }
        }

        public void SendTraces(IReadOnlyCollection<TraceActivitiesContainer> traces)
        {
            if (traces == null || traces.Count == 0)
            {
                return;
            }

            // @ToDo!
            throw new NotImplementedException();
        }

        public void SendActivities(IReadOnlyCollection<Activity> traces)
        {
            throw new NotSupportedException($"{nameof(DatadogAgentMessagePackActivityExporter)} does not support SendActivities(..).");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed state:
                // . . .
            }

            // Free unmanaged resources
            // Set large fields to null
        }

        // Uncomment/Override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~DatadogAgentMessagePackExporter()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
