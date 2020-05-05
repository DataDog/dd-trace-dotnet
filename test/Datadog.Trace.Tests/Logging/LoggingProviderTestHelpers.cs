using System;
using System.Globalization;
using System.IO;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Moq;
using Serilog.Formatting.Display;
using Xunit;

namespace Datadog.Trace.Tests.Logging
{
    internal static class LoggingProviderTestHelpers
    {
        internal static readonly string CustomPropertyName = "custom";
        internal static readonly int CustomPropertyValue = 1;
        internal static readonly string LogPrefix = "[Datadog.Trace.Tests.Logging]";

        internal static Tracer InitializeTracer(bool enableLogsInjection)
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            settings.LogsInjectionEnabled = enableLogsInjection;
            settings.ServiceVersion = "custom-version";
            settings.Environment = "custom-env";

            return new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        }

        internal static void LogInSpanWithServiceName(Tracer tracer, ILog logger, Func<string, object, bool, IDisposable> openMappedContext, string service, out Scope scope)
        {
            using (scope = tracer.StartActive("span", serviceName: service))
            {
                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    logger.Log(LogLevel.Info, () => $"{LogPrefix}Entered single scope with a different service name.");
                }
            }
        }

        internal static void LogInParentSpan(Tracer tracer, ILog logger, Func<string, object, bool, IDisposable> openMappedContext, out Scope parentScope, out Scope childScope)
        {
            using (parentScope = tracer.StartActive("parent"))
            {
                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    logger.Log(LogLevel.Info, () => $"Started and activated parent scope.");

                    using (childScope = tracer.StartActive("child"))
                    {
                        // Empty
                    }

                    logger.Log(LogLevel.Info, () => $"{LogPrefix}Closed child scope and reactivated parent scope.");
                }
            }
        }

        internal static void LogInChildSpan(Tracer tracer, ILog logger, Func<string, object, bool, IDisposable> openMappedContext, out Scope parentScope, out Scope childScope)
        {
            using (parentScope = tracer.StartActive("parent"))
            {
                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    using (childScope = tracer.StartActive("child"))
                    {
                        logger.Log(LogLevel.Info, () => $"{LogPrefix}Started and activated child scope.");
                    }
                }
            }
        }

        internal static void LogOutsideSpans(Tracer tracer, ILog logger, Func<string, object, bool, IDisposable> openMappedContext, out Scope parentScope, out Scope childScope)
        {
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Logged before starting/activating a scope");

            using (parentScope = tracer.StartActive("parent"))
            {
                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    using (childScope = tracer.StartActive("child"))
                    {
                        // Empty
                    }
                }
            }

            logger.Log(LogLevel.Info, () => $"{LogPrefix}Closed child scope so there is no active scope.");
        }

        internal static void LogEverywhere(Tracer tracer, ILog logger, Func<string, object, bool, IDisposable> openMappedContext, out Scope parentScope, out Scope childScope)
        {
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Logged before starting/activating a scope");

            using (parentScope = tracer.StartActive("parent"))
            {
                logger.Log(LogLevel.Info, () => $"Started and activated parent scope.");

                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    using (childScope = tracer.StartActive("child"))
                    {
                        logger.Log(LogLevel.Info, () => $"{LogPrefix}Started and activated child scope.");
                    }
                }

                logger.Log(LogLevel.Info, () => $"{LogPrefix}Closed child scope and reactivated parent scope.");
            }

            logger.Log(LogLevel.Info, () => $"{LogPrefix}Closed child scope so there is no active scope.");
        }
    }
}
