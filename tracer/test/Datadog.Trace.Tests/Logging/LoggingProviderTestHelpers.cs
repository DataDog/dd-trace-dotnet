// <copyright file="LoggingProviderTestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Moq;

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

        internal static void LogInSpanWithServiceName(ILog logger, Func<string, object, bool, IDisposable> openMappedContext, string service, out IScope scope)
        {
            using (scope = Tracer.Instance.StartActive("span", serviceName: service))
            {
                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    logger.Log(LogLevel.Info, () => $"{LogPrefix}Entered single scope with a different service name.");
                }
            }
        }

        internal static void LogInParentSpan(ILog logger, Func<string, object, bool, IDisposable> openMappedContext, out IScope parentScope, out IScope childScope)
        {
            using (parentScope = Tracer.Instance.StartActive("parent"))
            {
                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    logger.Log(LogLevel.Info, () => $"Started and activated parent scope.");

                    using (childScope = Tracer.Instance.StartActive("child"))
                    {
                        // Empty
                    }

                    logger.Log(LogLevel.Info, () => $"{LogPrefix}Closed child scope and reactivated parent scope.");
                }
            }
        }

        internal static void LogInChildSpan(ILog logger, Func<string, object, bool, IDisposable> openMappedContext, out IScope parentScope, out IScope childScope)
        {
            using (parentScope = Tracer.Instance.StartActive("parent"))
            {
                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    using (childScope = Tracer.Instance.StartActive("child"))
                    {
                        logger.Log(LogLevel.Info, () => $"{LogPrefix}Started and activated child scope.");
                    }
                }
            }
        }

        internal static void LogOutsideSpans(ILog logger, Func<string, object, bool, IDisposable> openMappedContext, out IScope parentScope, out IScope childScope)
        {
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Logged before starting/activating a scope");

            using (parentScope = Tracer.Instance.StartActive("parent"))
            {
                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    using (childScope = Tracer.Instance.StartActive("child"))
                    {
                        // Empty
                    }
                }
            }

            logger.Log(LogLevel.Info, () => $"{LogPrefix}Closed child scope so there is no active scope.");
        }

        internal static void LogEverywhere(ILog logger, Func<string, object, bool, IDisposable> openMappedContext, out IScope parentScope, out IScope childScope)
        {
            logger.Log(LogLevel.Info, () => $"{LogPrefix}Logged before starting/activating a scope");

            using (parentScope = Tracer.Instance.StartActive("parent"))
            {
                logger.Log(LogLevel.Info, () => $"Started and activated parent scope.");

                using (var mappedContext = openMappedContext(CustomPropertyName, CustomPropertyValue, false))
                {
                    using (childScope = Tracer.Instance.StartActive("child"))
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
