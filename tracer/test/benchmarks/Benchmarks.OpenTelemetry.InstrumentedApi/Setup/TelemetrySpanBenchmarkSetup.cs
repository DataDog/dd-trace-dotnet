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
        TracerHelper.CleanupGlobalTracer();
    }
}
