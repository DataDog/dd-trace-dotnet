// <copyright file="ExtendedLoggerFactoryConstructorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission;

/// <summary>
/// LoggerFactory() calltarget instrumentation for direct log submission
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.Extensions.Telemetry",
    TypeName = "Microsoft.Extensions.Logging.ExtendedLoggerFactory",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = new[] { "System.Collections.Generic.IEnumerable`1[Microsoft.Extensions.Logging.ILoggerProvider]", "System.Collections.Generic.IEnumerable`1[Microsoft.Extensions.Diagnostics.Enrichment.ILogEnricher]", "System.Collections.Generic.IEnumerable`1[Microsoft.Extensions.Diagnostics.Enrichment.IStaticLogEnricher]", "Microsoft.Extensions.Options.IOptionsMonitor`1[Microsoft.Extensions.Logging.LoggerFilterOptions]", "Microsoft.Extensions.Options.IOptions`1[Microsoft.Extensions.Logging.LoggerFactoryOptions]", "Microsoft.Extensions.Logging.IExternalScopeProvider", "Microsoft.Extensions.Options.IOptionsMonitor`1[Microsoft.Extensions.Logging.LoggerEnrichmentOptions]", "Microsoft.Extensions.Options.IOptionsMonitor`1[Microsoft.Extensions.Logging.LoggerRedactionOptions]", "Microsoft.Extensions.Compliance.Redaction.IRedactorProvider" },
    MinimumVersion = "8.0.0",
    MaximumVersion = "8.*.*",
    IntegrationName = LoggerIntegrationCommon.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class ExtendedLoggerFactoryConstructorIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget, TProviders, TEnrichers, TStaticEnrichers, TFilterOptions, TFactoryOptions, TScopeProvider, TEnrichmentOptions, TRedactionOptions, TRedactorProvider>(TTarget instance, TProviders providers, TEnrichers enrichers, TStaticEnrichers staticEnrichers, TFilterOptions filterOptions, TFactoryOptions factoryOptions, TScopeProvider scopeProvider, TEnrichmentOptions enrichmentOptions, TRedactionOptions redactionOptions, TRedactorProvider redactorProvider)
    {
        return new CallTargetState(scope: null, state: scopeProvider);
    }

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
    {
        if (!TracerManager.Instance.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.ILogger))
        {
            return CallTargetReturn.GetDefault();
        }

        if (exception is not null)
        {
            // If there's an exception during the constructor, things aren't going to work anyway
            return CallTargetReturn.GetDefault();
        }

        var scopeProvider = state.State is { } rawScopeProvider
                                ? rawScopeProvider.DuckCast<IExternalScopeProvider>()
                                : instance.DuckCast<ExtendedLoggerFactoryProxy>().ScopeProvider;

        if (LoggerFactoryIntegrationCommon<TTarget>.TryAddDirectSubmissionLoggerProvider(instance, scopeProvider))
        {
            TracerManager.Instance.Telemetry.IntegrationGeneratedSpan(IntegrationId.ILogger);
        }

        return CallTargetReturn.GetDefault();
    }
}
