// <copyright file="LoggerIntegrationCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger
{
    internal static class LoggerIntegrationCommon
    {
        public const string IntegrationName = nameof(Configuration.IntegrationId.ILogger);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.ILogger;

        private static DatadogLoggingScope? _datadogScope;

        public static void AddScope<TAction, TState>(Tracer tracer, TAction callback, TState? state)
        {
            var settings = tracer.CurrentTraceSettings.Settings;
            if (settings.LogsInjectionEnabled
             && settings.IsIntegrationEnabled(IntegrationId)
             && callback is Action<object, TState?> foreachCallback)
            {
                var scope = Volatile.Read(ref _datadogScope);
                if (!ReferenceEquals(scope?.Settings, settings))
                {
                    // mutable settings have changed, create a new scope and update the cached value
                    // This is just best-effort for updating the scope. There's a small risk of
                    // ping-pong if there's a long-lived trace for example, but it's a slim chance
                    scope = new DatadogLoggingScope(tracer, settings);
                    Volatile.Write(ref _datadogScope, scope);
                }

                foreachCallback.Invoke(scope, state);
            }
        }
    }
}
