using Datadog.Trace.Ci;

namespace Datadog.Trace.ClrProfiler.Integrations.Testing
{
    internal static class Common
    {
        static Common()
        {
            TestTracer = Tracer.Instance;
            ServiceName = TestTracer.DefaultServiceName;

            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
        }

        internal static Tracer TestTracer { get; private set; }

        internal static string ServiceName { get; private set; }
    }
}
