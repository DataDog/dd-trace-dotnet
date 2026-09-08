using OpenTelemetry.Trace;
using OpenTelemetry;

namespace Benchmarks.OpenTelemetry.ProfiledApi.Setup
{
    internal class ActivityBenchmarkSetup
    {
        private TracerProvider tracerProvider;

        internal void GlobalSetup()
        {
            ArmGuard.Verify();

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
