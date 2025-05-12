using System;
using System.Linq;

namespace Benchmarks.OpenTelemetry.InstrumentedApi.Setup;

internal class TelemetrySpanBenchmarkSetup
{
    internal void GlobalSetup()
    {
        Datadog.Trace.Activity.ActivityListener.Initialize();
    }

    internal void GlobalCleanup()
    {
    }
}
