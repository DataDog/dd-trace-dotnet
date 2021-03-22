using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing
{
    internal static class Common
    {
        static Common()
        {
            var settings = TracerSettings.FromDefaultSources();
            settings.TraceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.

            TestTracer = new Tracer(settings);
            ServiceName = TestTracer.DefaultServiceName;

            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
        }

        internal static Tracer TestTracer { get; private set; }

        internal static string ServiceName { get; private set; }
    }
}
