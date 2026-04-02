using System;
using System.Linq;
using Benchmarks.Trace;

namespace Benchmarks.OpenTelemetry.InstrumentedApi.Setup;

internal class TracerBenchmarkSetup
{
    internal void GlobalSetup()
    {
        TracerHelper.SetGlobalTracer();
        Datadog.Trace.Activity.ActivityListener.Initialize(Datadog.Trace.Tracer.Instance);
    }

    internal void GlobalCleanup()
    {
        TracerHelper.CleanupGlobalTracer();
    }
}
