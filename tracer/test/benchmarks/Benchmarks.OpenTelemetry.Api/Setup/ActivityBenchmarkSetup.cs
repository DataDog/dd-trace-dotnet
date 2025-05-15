using OpenTelemetry.Trace;
using OpenTelemetry;

namespace Benchmarks.OpenTelemetry.Api.Setup
{
    internal class ActivityBenchmarkSetup
    {
        private TracerProvider tracerProvider;

        internal void GlobalSetup()
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource("ActivityBenchmark")
                .Build();
        }

        internal void GlobalCleanup()
        {
            this.tracerProvider?.Dispose();
        }
    }
}
