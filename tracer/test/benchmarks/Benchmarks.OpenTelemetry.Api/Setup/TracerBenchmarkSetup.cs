using OpenTelemetry.Trace;
using OpenTelemetry;

namespace Benchmarks.OpenTelemetry.Api.Setup
{
    internal class TracerBenchmarkSetup
    {
        private TracerProvider tracerProviderAlwaysOnSample;

        internal void GlobalSetup()
        {
            this.tracerProviderAlwaysOnSample = Sdk.CreateTracerProviderBuilder()
                .AddSource("TracerBenchmark_AlwaysOnSample")
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
