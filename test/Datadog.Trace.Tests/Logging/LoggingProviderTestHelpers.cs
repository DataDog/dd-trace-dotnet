using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Moq;

namespace Datadog.Trace.Tests.Logging
{
    internal class LoggingProviderTestHelpers
    {
        internal static Tracer InitializeTracer(bool enableLogsInjection)
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            settings.LogsInjectionEnabled = enableLogsInjection;

            return new Tracer(settings, writerMock.Object, samplerMock.Object, null);
        }

        internal static void PerformParentChildScopeSequence(Tracer tracer, ILog logger, out Scope parentScope, out Scope childScope)
        {
            parentScope = tracer.StartActive("parent");
            logger.Log(LogLevel.Info, () => "Started and activated parent scope.");

            childScope = tracer.StartActive("child");
            logger.Log(LogLevel.Info, () => "Started and activated child scope.");

            childScope.Close();
            logger.Log(LogLevel.Info, () => "Closed child scope and reactivated parent scope.");

            parentScope.Close();
            logger.Log(LogLevel.Info, () => "Closed child scope so there is no active scope.");
        }
    }
}
