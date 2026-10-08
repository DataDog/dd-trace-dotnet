// <copyright file="LoggerFactoryIntegrationCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;

[assembly: DuckTypeCompositeInterface(
    DirectSubmissionLoggerProvider.ProxyInterfaceName,
    "Microsoft.Extensions.Logging.ILoggerProvider, Microsoft.Extensions.Logging.Abstractions",
    "Microsoft.Extensions.Logging.ISupportExternalScope, Microsoft.Extensions.Logging.Abstractions")]

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission
{
    internal static class LoggerFactoryIntegrationCommon<TLoggerFactory>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LoggerFactoryIntegrationCommon<TLoggerFactory>));

        // ReSharper disable once StaticMemberInGenericType
        [TestingAndPrivateOnly]
        internal static readonly Type? ProviderInterfaces;

        static LoggerFactoryIntegrationCommon()
        {
            try
            {
                // The ILoggerProvider type is in a different assembly to the LoggerFactory, so go via the ILogger type
                // returned by CreateLogger
                var loggerFactoryType = typeof(TLoggerFactory);
                var abstractionsAssembly = loggerFactoryType.GetMethod("CreateLogger")?.ReturnType.Assembly
                                        ?? Type.GetType("Microsoft.Extensions.Logging.ILogger, Microsoft.Extensions.Logging.Abstractions", throwOnError: false)!.Assembly;
                var iLoggerProviderType = abstractionsAssembly.GetType("Microsoft.Extensions.Logging.ILoggerProvider");
                var iSupportExternalScopeType = abstractionsAssembly.GetType("Microsoft.Extensions.Logging.ISupportExternalScope");

                if (iSupportExternalScopeType is null)
                {
                    // ISupportExternalScope is only available in v2.1+
                    // We can just duck type ILoggerProvider directly
                    ProviderInterfaces = iLoggerProviderType;
                    return;
                }

                // We need to implement both ILoggerProvider and ISupportExternalScope
                // because LoggerFactory uses pattern matching to check if we implement the latter
                // Duck Typing can currently only implement a single interface, so use
                // an interface that implements both ILoggerProvider and ISupportExternalScope
                // and duck cast to that
                ProviderInterfaces = DuckType.GetCompositeInterface(DirectSubmissionLoggerProvider.ProxyInterfaceName, iLoggerProviderType!, iSupportExternalScopeType);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error loading logger factory types for {LoggerFactoryType}", typeof(TLoggerFactory));
                ProviderInterfaces = null;
            }
        }

        internal static bool TryAddDirectSubmissionLoggerProvider(TLoggerFactory loggerFactory)
            => TryAddDirectSubmissionLoggerProvider(loggerFactory, scopeProvider: null);

        internal static bool TryAddDirectSubmissionLoggerProvider(TLoggerFactory loggerFactory, IExternalScopeProvider? scopeProvider)
        {
            if (ProviderInterfaces is null)
            {
                // there was a problem loading the assembly for some reason
                return false;
            }

            var provider = new DirectSubmissionLoggerProvider(
                TracerManager.Instance.DirectLogSubmission.Sink,
                TracerManager.Instance.DirectLogSubmission.Settings.MinimumLevel,
                scopeProvider);

            return TryAddDirectSubmissionLoggerProvider(loggerFactory, provider);
        }

        [TestingAndPrivateOnly]
        internal static bool TryAddDirectSubmissionLoggerProvider(TLoggerFactory loggerFactory, DirectSubmissionLoggerProvider provider)
        {
            if (ProviderInterfaces is null)
            {
                // there was a problem loading the assembly for some reason
                return false;
            }

            var proxy = provider.DuckImplement(ProviderInterfaces);
            if (loggerFactory is not null)
            {
                var loggerFactoryProxy = loggerFactory.DuckCast<ILoggerFactory>();
                loggerFactoryProxy.AddProvider(proxy);
                Log.Information("Direct log submission via ILogger enabled");
                return true;
            }

            return false;
        }
    }
}
