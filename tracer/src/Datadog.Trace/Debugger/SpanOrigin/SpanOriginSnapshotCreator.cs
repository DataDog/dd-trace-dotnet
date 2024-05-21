using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.ExceptionAutoInstrumentation;
using Datadog.Trace.Debugger.Snapshots;

namespace Datadog.Trace.Debugger.SpanOrigin
{
    internal class SpanOriginSnapshotCreator : IDebuggerSnapshotCreator
    {
        public SpanOriginSnapshotCreator(string probeId)
        {
            ProbeId = probeId;
        }

        public string ProbeId { get; }
    }
}
