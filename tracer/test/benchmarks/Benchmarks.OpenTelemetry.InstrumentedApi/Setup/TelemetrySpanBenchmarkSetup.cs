using System;
using System.Linq;
using Benchmarks.Trace;

namespace Benchmarks.OpenTelemetry.InstrumentedApi.Setup;

internal class TelemetrySpanBenchmarkSetup
{
    internal void GlobalSetup()
    {
        TracerHelper.SetGlobalTracer();
        Datadog.Trace.Activity.ActivityListener.Initialize();
    }

    internal void GlobalCleanup()
    {
        // Stop the activity listener and its reconciliation timer so the 10s sweep
        // loop started by Initialize() does not keep running for the rest of the process.
        Datadog.Trace.Activity.ActivityListener.StopListeners();
        TracerHelper.CleanupGlobalTracer();
    }
}
