using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing
{
    internal static class Common
    {
        internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Common));

        static Common()
        {
            var settings = TracerSettings.FromDefaultSources();
            settings.TraceBufferSize = 1024 * 1024 * 45; // slightly lower than the 50mb payload agent limit.

            TestTracer = new Tracer(settings);
            ServiceName = TestTracer.DefaultServiceName;

            Tracer.Instance = TestTracer;

            // Preload environment variables.
            CIEnvironmentValues.DecorateSpan(null);
        }

        internal static Tracer TestTracer { get; private set; }

        internal static string ServiceName { get; private set; }

        internal static void FlushSpans(IntegrationInfo integrationInfo)
        {
            if (TestTracer.Settings.IsIntegrationEnabled(integrationInfo))
            {
                FlushSpans();
            }
        }

        internal static void FlushSpans()
        {
            SynchronizationContext context = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                InternalFlush().GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }

            static async Task InternalFlush()
            {
                // We have to ensure the flush of the buffer after we finish the tests of an assembly.
                // For some reason, sometimes when all test are finished none of the callbacks to handling the tracer disposal is triggered.
                // So the last spans in buffer aren't send to the agent.
                Log.Debug("Integration flushing spans.");
                await TestTracer.FlushAsync().ConfigureAwait(false);
                // The current agent writer FlushAsync method can return inmediately if a payload is being sent (there is buffer lock)
                // There is not api in the agent writer that guarantees the send has been sucessfully completed.
                // Until we change the behavior of the agentwriter we should at least wait 2 seconds before returning.
                Log.Debug("Waiting 2 seconds to flush.");
                await Task.Delay(2000).ConfigureAwait(false);
                Log.Debug("Integration flushed.");
            }
        }
    }
}
