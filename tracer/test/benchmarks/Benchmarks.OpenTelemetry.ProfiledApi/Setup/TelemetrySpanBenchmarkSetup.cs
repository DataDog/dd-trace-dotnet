using OpenTelemetry.Trace;
using OpenTelemetry;

namespace Benchmarks.OpenTelemetry.ProfiledApi.Setup
{
    internal class TelemetrySpanBenchmarkSetup
    {
        private TracerProvider tracerProviderAlwaysOnSample;

        internal void GlobalSetup()
        {
            this.tracerProviderAlwaysOnSample = Sdk.CreateTracerProviderBuilder()
                .AddSource("TelemetrySpanBenchmark_AlwaysOnSample")
                .SetSampler(new AlwaysOnSampler())
                .Build();

            using var traceProviderNoop = Sdk.CreateTracerProviderBuilder().Build();
        }

        internal void GlobalCleanup()
        {
            this.tracerProviderAlwaysOnSample?.Dispose();
        }
    }
}
